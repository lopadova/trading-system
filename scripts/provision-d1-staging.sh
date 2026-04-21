#!/usr/bin/env bash
# scripts/provision-d1-staging.sh
# ---------------------------------------------------------------------------
# Creates the `trading-db-staging` D1 database via `bunx wrangler d1 create`
# and patches `infra/cloudflare/worker/wrangler.toml` with the resulting
# database_id so `bunx wrangler deploy --env staging` can bind to it.
#
# Idempotent: if the staging DB already exists (or the TBD placeholder has
# already been replaced with a real UUID), the script is a no-op.
#
# Usage:
#   ./scripts/provision-d1-staging.sh
# ---------------------------------------------------------------------------
set -euo pipefail

# Resolve repo root from script location so the script works regardless of cwd.
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
WRANGLER_TOML="${REPO_ROOT}/infra/cloudflare/worker/wrangler.toml"
PLACEHOLDER="TBD-after-wrangler-d1-create"

[ -f "${WRANGLER_TOML}" ] || { echo "ERROR: wrangler.toml not found at ${WRANGLER_TOML}" >&2; exit 1; }

# Early exit: the placeholder is already gone → staging D1 is already wired.
if ! grep -q "${PLACEHOLDER}" "${WRANGLER_TOML}"; then
  echo "Staging D1 database_id already set in wrangler.toml. Nothing to do."
  exit 0
fi

echo "Creating Cloudflare D1 database 'trading-db-staging'..."

# `bunx wrangler d1 create <name>` prints a TOML snippet with the new id.
# We capture the output, extract the database_id and splice it into
# wrangler.toml, replacing the TBD placeholder.
CREATE_OUTPUT="$(cd "${REPO_ROOT}/infra/cloudflare/worker" && bunx wrangler d1 create trading-db-staging 2>&1 || true)"
echo "${CREATE_OUTPUT}"

# Wrangler prints `database_id = "<uuid>"` on a line in its output. Grab it.
NEW_ID="$(echo "${CREATE_OUTPUT}" | grep -oE 'database_id = "[^"]+"' | head -1 | sed -E 's/database_id = "([^"]+)"/\1/')"

if [ -z "${NEW_ID}" ]; then
  # If wrangler output didn't contain a new id, the DB likely already
  # exists under that name — try to discover its id via `d1 list`.
  echo "No new id in create output — checking existing databases..."
  LIST_OUTPUT="$(cd "${REPO_ROOT}/infra/cloudflare/worker" && bunx wrangler d1 list 2>&1 || true)"
  # `d1 list` prints a table; each row contains the id and name. Grab the
  # line containing our DB name and pull out the UUID (8-4-4-4-12 hex).
  NEW_ID="$(echo "${LIST_OUTPUT}" | grep "trading-db-staging" \
    | grep -oE '[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}' \
    | head -1)"
fi

if [ -z "${NEW_ID}" ]; then
  echo "ERROR: failed to determine database_id for trading-db-staging." >&2
  echo "Run 'bunx wrangler d1 list' manually and update wrangler.toml." >&2
  exit 1
fi

echo "Patching wrangler.toml with database_id=${NEW_ID}..."
# Portable in-place sed: write to temp file, then move. Avoids sed -i
# portability issues between GNU sed and BSD sed (mac).
TMP_FILE="$(mktemp)"
sed "s/${PLACEHOLDER}/${NEW_ID}/" "${WRANGLER_TOML}" > "${TMP_FILE}"
mv "${TMP_FILE}" "${WRANGLER_TOML}"

echo "Done. Staging D1 is wired. Next: 'bunx wrangler deploy --env staging'."
