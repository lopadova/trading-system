#!/usr/bin/env bash
# Verify SQLite schema for supervisor.db and options.db
# Run this after migrations to confirm schema is correct

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
DATA_DIR="$REPO_ROOT/data"

echo "=== SQLite Schema Verification ==="
echo ""

# Create data directory if it doesn't exist
mkdir -p "$DATA_DIR"

# Check if sqlite3 is available
if ! command -v sqlite3 &> /dev/null; then
    echo "ERROR: sqlite3 command not found. Please install SQLite."
    exit 1
fi

# Function to verify PRAGMA settings
verify_pragma() {
    local db_path=$1
    local db_name=$2

    echo "--- Verifying PRAGMA settings for $db_name ---"

    journal_mode=$(sqlite3 "$db_path" "PRAGMA journal_mode;")
    echo "journal_mode: $journal_mode (expected: wal)"

    foreign_keys=$(sqlite3 "$db_path" "PRAGMA foreign_keys;")
    echo "foreign_keys: $foreign_keys (expected: 1)"

    echo ""
}

# Function to verify table schema
verify_tables() {
    local db_path=$1
    local db_name=$2

    echo "--- Tables in $db_name ---"
    sqlite3 "$db_path" "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;"
    echo ""

    echo "--- Indexes in $db_name ---"
    sqlite3 "$db_path" "SELECT name, tbl_name FROM sqlite_master WHERE type='index' ORDER BY tbl_name, name;"
    echo ""
}

# Verify supervisor.db (if exists)
SUPERVISOR_DB="$DATA_DIR/supervisor.db"
if [ -f "$SUPERVISOR_DB" ]; then
    echo "=== supervisor.db ==="
    verify_pragma "$SUPERVISOR_DB" "supervisor.db"
    verify_tables "$SUPERVISOR_DB" "supervisor.db"
else
    echo "supervisor.db not found at $SUPERVISOR_DB (run migrations first)"
    echo ""
fi

# Verify options.db (if exists)
OPTIONS_DB="$DATA_DIR/options.db"
if [ -f "$OPTIONS_DB" ]; then
    echo "=== options.db ==="
    verify_pragma "$OPTIONS_DB" "options.db"
    verify_tables "$OPTIONS_DB" "options.db"
else
    echo "options.db not found at $OPTIONS_DB (run migrations first)"
    echo ""
fi

echo "=== Verification Complete ==="
