#!/usr/bin/env bash
# scripts/backup-d1.sh
# ---------------------------------------------------------------------------
# Exports a Cloudflare D1 database to a timestamped SQL file and optionally
# uploads the file to Cloudflare R2 if $R2_BUCKET is set.
#
# Usage:
#   ./scripts/backup-d1.sh trading-db
#   ./scripts/backup-d1.sh trading-db-staging
#
# Env:
#   R2_BUCKET     - optional R2 bucket name. If set, the export is also
#                   uploaded to:
#                     d1/<database>/<YYYY>/<MM>/<database>_<YYYY-MM-DDTHHMM>.sql
#   OUTPUT_DIR    - optional override for local output directory.
#                   Default: $REPO_ROOT/backups/d1
#
# Idempotent: each invocation writes a new timestamp-named file and NEVER
# overwrites. Safe to schedule daily. Retention is the operator's job.
# ---------------------------------------------------------------------------
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
WRANGLER_CONFIG="${REPO_ROOT}/infra/cloudflare/worker/wrangler.toml"

db_name="${1:-}"
if [ -z "${db_name}" ]; then
  echo "Usage: $0 <database-name>" >&2
  echo "  e.g. $0 trading-db" >&2
  echo "       $0 trading-db-staging" >&2
  exit 1
fi

# Guardrail — parse the allowed DB names from wrangler.toml so this script
# stays in sync if a future Phase renames or adds a D1 binding. Catches
# both "typo in the arg" and "wrangler.toml renamed but backup wasn't
# updated" failure modes before we hit Cloudflare.
if [ ! -f "${WRANGLER_CONFIG}" ]; then
  echo "ERROR: wrangler.toml not found at ${WRANGLER_CONFIG}" >&2
  exit 1
fi
allowed_db_names="$(awk '
  /^[[:space:]]*database_name[[:space:]]*=/ {
    match($0, /"[^"]*"/); if (RSTART > 0) print substr($0, RSTART+1, RLENGTH-2)
  }' "${WRANGLER_CONFIG}" | sort -u)"
if [ -z "${allowed_db_names}" ]; then
  echo "ERROR: could not parse any database_name from ${WRANGLER_CONFIG}" >&2
  exit 1
fi
if ! printf '%s\n' "${allowed_db_names}" | grep -qx "${db_name}"; then
  echo "ERROR: unknown database '${db_name}'." >&2
  echo "  Allowed (parsed from wrangler.toml):" >&2
  printf '    %s\n' ${allowed_db_names} >&2
  exit 1
fi

# Verify wrangler is reachable. Avoids a cryptic failure deep in the pipeline.
if ! command -v bunx >/dev/null 2>&1; then
  echo "ERROR: 'bunx' not found on PATH. Install Bun: https://bun.sh" >&2
  exit 1
fi

output_dir="${OUTPUT_DIR:-${REPO_ROOT}/backups/d1}"
mkdir -p "${output_dir}"

timestamp="$(date -u +%Y-%m-%dT%H%M)"
file_name="${db_name}_${timestamp}.sql"
output_path="${output_dir}/${file_name}"

echo "=== D1 backup ==="
echo "Database: ${db_name}"
echo "Output:   ${output_path}"
echo

# Run the export. --remote hits the live D1 instance (not the local .wrangler
# cache). We deliberately use the --remote flag — backing up local would
# produce a useless empty SQL file.
(cd "${REPO_ROOT}/infra/cloudflare/worker" && \
  bunx wrangler d1 export "${db_name}" \
    --remote \
    --output="${output_path}")

# Sanity check — if the export is empty or too small (< 1KB), something went
# wrong. An empty SQL file is worse than no file because it fails silently
# during restore.
if [ ! -s "${output_path}" ]; then
  echo "ERROR: export file is empty or missing: ${output_path}" >&2
  exit 2
fi

file_size="$(wc -c < "${output_path}")"
if [ "${file_size}" -lt 1024 ]; then
  echo "WARNING: export file is suspiciously small (${file_size} bytes)." >&2
  echo "         Inspect before trusting as a restore source." >&2
fi

echo "Export OK — ${file_size} bytes."

# Optional R2 upload. We upload AFTER the local write succeeds so a failed
# upload doesn't hide a failed export.
if [ -n "${R2_BUCKET:-}" ]; then
  year="$(date -u +%Y)"
  month="$(date -u +%m)"
  r2_key="d1/${db_name}/${year}/${month}/${file_name}"

  echo
  echo "Uploading to R2: ${R2_BUCKET}/${r2_key}"
  (cd "${REPO_ROOT}/infra/cloudflare/worker" && \
    bunx wrangler r2 object put "${R2_BUCKET}/${r2_key}" \
      --file="${output_path}" \
      --remote)
  echo "R2 upload OK."
fi

echo
echo "Done."
echo "  Local:  ${output_path}"
if [ -n "${R2_BUCKET:-}" ]; then
  echo "  Remote: r2://${R2_BUCKET}/${r2_key}"
fi
