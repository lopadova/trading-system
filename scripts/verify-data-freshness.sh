#!/usr/bin/env bash
# scripts/verify-data-freshness.sh
# ---------------------------------------------------------------------------
# Phase 7.6 — Verifies that the 5 critical D1 tables receiving the Trading
# System's market-data ingest are not stale. Intended for a nightly CI run
# after the US market close, but can be invoked manually at any time.
#
# Per-table staleness thresholds are hard-coded below with sensible
# defaults and overridable via CLI flags:
#
#   --heartbeats-minutes <N>     default 2      (any service)
#   --equity-days <N>            default 1      (after last US market close)
#   --quotes-days <N>            default 1      (date column, not hour-granular)
#   --vix-days <N>               default 1      (date column, not hour-granular)
#   --benchmark-days <N>         default 1
#
# NOTE on quotes/vix: the underlying tables store a DATE column (not a full
# timestamp), so sub-day granularity is not expressible today. The flag is
# intentionally days-based. If we ever need hour-level freshness we must
# first add a timestamp column (e.g. 'last_updated_at') to market_quotes_daily
# and vix_term_structure.
#
# Target Worker:
#   --env production | staging   default production
#   --db-name <NAME>             default parsed from wrangler.toml for --env
#
# Exits non-zero on any stale table and prints a concise table.
# ---------------------------------------------------------------------------
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
WRANGLER_DIR="${REPO_ROOT}/infra/cloudflare/worker"
WRANGLER_CONFIG="${WRANGLER_DIR}/wrangler.toml"

# ----------------------------------------------------------------------------
# Defaults
# ----------------------------------------------------------------------------
ENV="production"
DB_NAME=""           # if unset, parsed from wrangler.toml after env parse
HEARTBEATS_MIN=2
EQUITY_DAYS=1
QUOTES_DAYS=1
VIX_DAYS=1
BENCHMARK_DAYS=1

usage() {
  cat <<EOF
Usage: $(basename "$0") [options]

Verifies freshness of 5 D1 tables populated by the Trading System outbox.

Options:
  --env <production|staging>   Target Worker env. Default: production.
  --db-name <NAME>             D1 database name. Default: parsed from wrangler.toml.
  --heartbeats-minutes <N>     service_heartbeats stale threshold. Default: ${HEARTBEATS_MIN}.
  --equity-days <N>            account_equity_daily stale threshold. Default: ${EQUITY_DAYS}.
  --quotes-days <N>            market_quotes_daily stale threshold. Default: ${QUOTES_DAYS}.
  --vix-days <N>               vix_term_structure stale threshold. Default: ${VIX_DAYS}.
  --benchmark-days <N>         benchmark_series stale threshold. Default: ${BENCHMARK_DAYS}.
  -h | --help                  Show this help.

Exit codes:
   0   All tables fresh.
   1   At least one table stale.
   2   Invocation / environment error (wrangler missing, bad flags, etc.).
EOF
}

# ----------------------------------------------------------------------------
# Argument parsing (GNU-getopt-avoided for portability — plain shift loop)
# ----------------------------------------------------------------------------
while [ $# -gt 0 ]; do
  case "$1" in
    --env)
      ENV="$2"; shift 2;;
    --db-name)
      DB_NAME="$2"; shift 2;;
    --heartbeats-minutes)
      HEARTBEATS_MIN="$2"; shift 2;;
    --equity-days)
      EQUITY_DAYS="$2"; shift 2;;
    --quotes-days)
      QUOTES_DAYS="$2"; shift 2;;
    --vix-days)
      VIX_DAYS="$2"; shift 2;;
    --benchmark-days)
      BENCHMARK_DAYS="$2"; shift 2;;
    -h|--help)
      usage; exit 0;;
    *)
      echo "ERROR: unknown flag '$1'" >&2
      usage >&2
      exit 2;;
  esac
done

case "${ENV}" in
  production|staging) ;;
  *)
    echo "ERROR: --env must be 'production' or 'staging', got '${ENV}'" >&2
    exit 2;;
esac

# ----------------------------------------------------------------------------
# DB name resolution — parse from wrangler.toml unless --db-name was given.
# Production lives under the top-level [[d1_databases]] block; staging lives
# under [env.staging.[[d1_databases]]]. We tolerate either ordering and
# whitespace around '=' and the quoted value.
# ----------------------------------------------------------------------------
if [ -z "${DB_NAME}" ]; then
  if [ ! -f "${WRANGLER_CONFIG}" ]; then
    echo "ERROR: wrangler.toml not found at ${WRANGLER_CONFIG}; pass --db-name explicitly." >&2
    exit 2
  fi
  if [ "${ENV}" = "staging" ]; then
    # First database_name AFTER the staging section marker. Section marker
    # can be either [env.staging.*] (single-bracket table) or
    # [[env.staging.d1_databases]] (double-bracket array-of-tables); match both.
    DB_NAME=$(awk '
      /^\[+env\.staging\./              { in_staging=1; next }
      /^\[+/ && !/^\[+env\.staging\./   { in_staging=0 }
      in_staging && /^[[:space:]]*database_name[[:space:]]*=/ {
        match($0, /"[^"]*"/); print substr($0, RSTART+1, RLENGTH-2); exit
      }' "${WRANGLER_CONFIG}")
  else
    # Production: first database_name BEFORE any [env.*] section marker.
    DB_NAME=$(awk '
      /^\[env\./ { exit }
      /^[[:space:]]*database_name[[:space:]]*=/ {
        match($0, /"[^"]*"/); print substr($0, RSTART+1, RLENGTH-2); exit
      }' "${WRANGLER_CONFIG}")
  fi
  if [ -z "${DB_NAME}" ]; then
    echo "ERROR: could not parse database_name for env='${ENV}' from ${WRANGLER_CONFIG}. Pass --db-name explicitly." >&2
    exit 2
  fi
  echo "Using D1 database: ${DB_NAME} (parsed from wrangler.toml)"
fi

# ----------------------------------------------------------------------------
# Prerequisites
# ----------------------------------------------------------------------------
if ! command -v jq >/dev/null 2>&1; then
  echo "ERROR: jq is required but not installed. Install via 'apt-get install jq' or 'brew install jq'." >&2
  exit 2
fi

# wrangler is invoked via npx from the worker dir so we use the repo's
# pinned wrangler version rather than whatever is on PATH.
if [ ! -f "${WRANGLER_DIR}/package.json" ]; then
  echo "ERROR: wrangler dir not found at ${WRANGLER_DIR}" >&2
  exit 2
fi

WRANGLER_ENV_FLAG=""
if [ "${ENV}" = "staging" ]; then
  WRANGLER_ENV_FLAG="--env staging"
fi

# ----------------------------------------------------------------------------
# Helpers
# ----------------------------------------------------------------------------
# Runs a read-only SQL statement against the Worker's D1 via wrangler and
# returns the first result value. wrangler returns an array-of-arrays JSON
# shape; we ask for the first row's first column.
run_d1_scalar() {
  local sql="$1"
  local raw
  # shellcheck disable=SC2086 # WRANGLER_ENV_FLAG is intentionally unquoted
  raw=$(cd "${WRANGLER_DIR}" && npx wrangler d1 execute "${DB_NAME}" ${WRANGLER_ENV_FLAG} \
    --remote --json --command "${sql}" 2>/dev/null) || return 1
  # Shape: [ { "results": [ { "col": "..." } ] } ]. Extract first value.
  echo "${raw}" | jq -r '.[0].results[0] | to_entries[0].value // empty'
}

# ISO8601 datetime of "now minus N minutes/hours/days". GNU date syntax.
# Falls back gracefully on BSD date (macOS) using -v flags.
iso_offset() {
  local unit="$1"    # minutes | hours | days
  local count="$2"
  if date --version >/dev/null 2>&1; then
    # GNU date
    date -u -d "-${count} ${unit}" '+%Y-%m-%dT%H:%M:%SZ'
  else
    # BSD date
    case "${unit}" in
      minutes) date -u -v "-${count}M" '+%Y-%m-%dT%H:%M:%SZ';;
      hours)   date -u -v "-${count}H" '+%Y-%m-%dT%H:%M:%SZ';;
      days)    date -u -v "-${count}d" '+%Y-%m-%dT%H:%M:%SZ';;
    esac
  fi
}

# ----------------------------------------------------------------------------
# Checks — one per table. Each sets STATUS (fresh|stale|error) and LATEST.
# ----------------------------------------------------------------------------
declare -a ROWS=()
STALE_COUNT=0
ERROR_COUNT=0

check_heartbeats() {
  local threshold_iso latest sql
  threshold_iso=$(iso_offset minutes "${HEARTBEATS_MIN}")
  sql="SELECT MAX(last_seen_at) FROM service_heartbeats"
  if ! latest=$(run_d1_scalar "${sql}"); then
    ROWS+=("service_heartbeats|error|<query failed>|<${HEARTBEATS_MIN} min")
    ERROR_COUNT=$((ERROR_COUNT + 1))
    return
  fi
  if [ -z "${latest}" ] || [ "${latest}" = "null" ]; then
    ROWS+=("service_heartbeats|stale|<empty table>|<${HEARTBEATS_MIN} min")
    STALE_COUNT=$((STALE_COUNT + 1))
    return
  fi
  if [[ "${latest}" < "${threshold_iso}" ]]; then
    ROWS+=("service_heartbeats|stale|${latest}|>${HEARTBEATS_MIN} min behind")
    STALE_COUNT=$((STALE_COUNT + 1))
  else
    ROWS+=("service_heartbeats|fresh|${latest}|within ${HEARTBEATS_MIN} min")
  fi
}

# For equity/quotes/vix/benchmarks we compare against N days/hours ago.
# This is NOT market-aware (it does not skip weekends); operators running
# the script on a Monday should either pass --equity-days 3 or rely on the
# workflow's cron schedule being Mon-Fri.
check_daily_by_days() {
  local table="$1"
  local column="$2"     # typically 'date'
  local days="$3"
  local latest sql threshold
  threshold=$(date -u -d "-${days} days" '+%Y-%m-%d' 2>/dev/null || date -u -v "-${days}d" '+%Y-%m-%d')
  sql="SELECT MAX(${column}) FROM ${table}"
  if ! latest=$(run_d1_scalar "${sql}"); then
    ROWS+=("${table}|error|<query failed>|<${days} day")
    ERROR_COUNT=$((ERROR_COUNT + 1))
    return
  fi
  if [ -z "${latest}" ] || [ "${latest}" = "null" ]; then
    ROWS+=("${table}|stale|<empty table>|<${days} day")
    STALE_COUNT=$((STALE_COUNT + 1))
    return
  fi
  if [[ "${latest}" < "${threshold}" ]]; then
    ROWS+=("${table}|stale|${latest}|>${days} day(s) behind")
    STALE_COUNT=$((STALE_COUNT + 1))
  else
    ROWS+=("${table}|fresh|${latest}|within ${days} day(s)")
  fi
}

# ----------------------------------------------------------------------------
# Run
# ----------------------------------------------------------------------------
echo "Running data-freshness checks against env='${ENV}'..."
check_heartbeats
check_daily_by_days "account_equity_daily" "date" "${EQUITY_DAYS}"
check_daily_by_days "market_quotes_daily" "date" "${QUOTES_DAYS}"
check_daily_by_days "vix_term_structure" "date" "${VIX_DAYS}"
check_daily_by_days "benchmark_series" "date" "${BENCHMARK_DAYS}"

# ----------------------------------------------------------------------------
# Report
# ----------------------------------------------------------------------------
printf '\n%-24s %-7s %-26s %-s\n' "Table" "Status" "Latest" "Threshold"
printf '%s\n' "--------------------------------------------------------------------------------------"
for row in "${ROWS[@]}"; do
  IFS='|' read -r t s l th <<<"${row}"
  printf '%-24s %-7s %-26s %-s\n' "${t}" "${s}" "${l}" "${th}"
done
echo ""

if [ "${ERROR_COUNT}" -gt 0 ]; then
  echo "ERROR: ${ERROR_COUNT} check(s) failed to execute. Inspect wrangler logs." >&2
  exit 2
fi

if [ "${STALE_COUNT}" -gt 0 ]; then
  echo "STALE: ${STALE_COUNT} table(s) are out-of-date. See above." >&2
  exit 1
fi

echo "OK: all ${#ROWS[@]} tables are fresh."
exit 0
