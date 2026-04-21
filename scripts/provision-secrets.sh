#!/usr/bin/env bash
# scripts/provision-secrets.sh
# ---------------------------------------------------------------------------
# Reads cleartext secrets from secrets/.env.<env> (gitignored) and pushes
# each KEY=VALUE pair to the Cloudflare Worker via `bunx wrangler secret put`.
#
# Usage:
#   ./scripts/provision-secrets.sh production
#   ./scripts/provision-secrets.sh staging
#
# The file MUST live at secrets/.env.<env> relative to the repo root. If it
# does not exist, the script fails early with a pointer to secrets/README.md.
# ---------------------------------------------------------------------------
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
WRANGLER_CONFIG="${REPO_ROOT}/infra/cloudflare/worker/wrangler.toml"

env="${1:-}"
if [ -z "${env}" ]; then
  echo "Usage: $0 <production|staging>" >&2
  exit 1
fi

# Reject anything except the two known environments so we can't accidentally
# push production secrets to a typo'd env that `bunx wrangler` would silently create.
case "${env}" in
  production|staging) ;;
  *)
    echo "ERROR: unknown environment '${env}'. Use 'production' or 'staging'." >&2
    exit 1
    ;;
esac

file="${REPO_ROOT}/secrets/.env.${env}"
if [ ! -f "${file}" ]; then
  echo "ERROR: missing ${file}" >&2
  echo "See secrets/README.md for the template." >&2
  exit 1
fi

if [ ! -f "${WRANGLER_CONFIG}" ]; then
  echo "ERROR: missing wrangler config at ${WRANGLER_CONFIG}" >&2
  exit 1
fi

# Build the wrangler command prefix once so each secret call stays consistent.
# For production we omit --env (default environment); for staging we pass --env staging.
WRANGLER_ENV_FLAG=""
if [ "${env}" = "staging" ]; then
  WRANGLER_ENV_FLAG="--env staging"
fi

echo "Provisioning Cloudflare Worker secrets for env='${env}' from ${file}..."

pushed=0
skipped=0
while IFS='=' read -r key value || [ -n "${key}" ]; do
  # Skip comments (#...) and blank lines — the `|| [ -n "$key" ]` tail covers
  # the final line if it lacks a trailing newline.
  if [[ "${key}" =~ ^[[:space:]]*# ]]; then continue; fi
  if [ -z "${key// /}" ]; then continue; fi

  # Trim surrounding whitespace (defensive; our template has none).
  key="$(echo "${key}" | sed 's/^[[:space:]]*//;s/[[:space:]]*$//')"
  value="$(echo "${value}" | sed 's/^[[:space:]]*//;s/[[:space:]]*$//')"

  if [ -z "${value}" ] || [[ "${value}" == REPLACE_* ]]; then
    echo "  SKIP ${key}: empty / placeholder value"
    skipped=$((skipped + 1))
    continue
  fi

  echo "  PUT  ${key}"
  # Use printf (not echo -n) to avoid the extra newline that Cloudflare would
  # otherwise store as part of the secret — a subtle source of 401s.
  printf '%s' "${value}" | bunx wrangler secret put "${key}" \
    --config "${WRANGLER_CONFIG}" \
    ${WRANGLER_ENV_FLAG}
  pushed=$((pushed + 1))
done < "${file}"

echo ""
echo "Done. Pushed ${pushed} secret(s); skipped ${skipped} placeholder(s)."
echo "Verify with: bunx wrangler secret list ${WRANGLER_ENV_FLAG} --config ${WRANGLER_CONFIG}"
