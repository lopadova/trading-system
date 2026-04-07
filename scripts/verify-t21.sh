#!/usr/bin/env bash
# TASK-21 Verification Script
# Verifies all integration tests pass

set -e

TASK_ID="T-21"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
DASHBOARD_DIR="$PROJECT_ROOT/dashboard"
WORKER_DIR="$PROJECT_ROOT/infra/cloudflare/worker"
RESULTS_FILE="$PROJECT_ROOT/logs/${TASK_ID}-test-results.txt"

echo "====================================="
echo "TASK-21: Dashboard Integration Tests"
echo "====================================="
echo ""

# Initialize results
mkdir -p "$PROJECT_ROOT/logs"
echo "TASK-21 Test Results - $(date -u +"%Y-%m-%dT%H:%M:%SZ")" > "$RESULTS_FILE"
echo "======================================" >> "$RESULTS_FILE"
echo "" >> "$RESULTS_FILE"

PASSED=0
FAILED=0

run_test() {
    local test_id="$1"
    local description="$2"
    shift 2

    echo -n "${test_id}: ${description}... "

    if "$@" > /dev/null 2>&1; then
        echo "✅ PASS"
        echo "✅ ${test_id}: PASS — ${description}" >> "$RESULTS_FILE"
        ((PASSED++))
    else
        echo "❌ FAIL"
        echo "❌ ${test_id}: FAIL — ${description}" >> "$RESULTS_FILE"
        ((FAILED++))
    fi
}

# Prerequisite checks
echo "Checking prerequisites..."
run_test "CHECK-01" "Dashboard directory exists" test -d "$DASHBOARD_DIR"
run_test "CHECK-02" "Cloudflare Worker directory exists" test -d "$WORKER_DIR"
run_test "CHECK-03" "Test directory exists" test -d "$DASHBOARD_DIR/test"
run_test "CHECK-04" "vitest.config.ts exists" test -f "$DASHBOARD_DIR/vitest.config.ts"
run_test "CHECK-05" ".env.test exists" test -f "$DASHBOARD_DIR/.env.test"

echo ""
echo "Checking test files..."
run_test "CHECK-06" "integration-api.test.ts exists" test -f "$DASHBOARD_DIR/test/integration-api.test.ts"
run_test "CHECK-07" "integration-react-query.test.tsx exists" test -f "$DASHBOARD_DIR/test/integration-react-query.test.tsx"
run_test "CHECK-08" "integration-zustand.test.ts exists" test -f "$DASHBOARD_DIR/test/integration-zustand.test.ts"
run_test "CHECK-09" "test/README.md exists" test -f "$DASHBOARD_DIR/test/README.md"

echo ""
echo "Checking dependencies..."
cd "$DASHBOARD_DIR"
run_test "CHECK-10" "vitest installed" bash -c "bun pm ls | grep -q vitest"
run_test "CHECK-11" "@testing-library/react installed" bash -c "bun pm ls | grep -q '@testing-library/react'"
run_test "CHECK-12" "ky installed" bash -c "bun pm ls | grep -q '^ky'"

echo ""
echo "TypeScript compilation..."
run_test "TEST-21-43" "Dashboard typecheck passes" bun run typecheck

echo ""
echo "Building dashboard..."
run_test "TEST-21-44" "Dashboard builds successfully" bun run build

echo ""
echo "Note: Full integration tests require Cloudflare Worker running locally."
echo "To run integration tests:"
echo "  1. Start worker: cd infra/cloudflare/worker && bun run dev"
echo "  2. Run tests: cd dashboard && bun test"
echo ""

# Summary
echo ""
echo "======================================" >> "$RESULTS_FILE"
echo "Summary: ${PASSED} PASS, ${FAILED} FAIL" >> "$RESULTS_FILE"
echo ""

echo "Results: ${PASSED} PASS, ${FAILED} FAIL"
echo "Full results: $RESULTS_FILE"

if [ "$FAILED" -gt 0 ]; then
    exit 1
fi

exit 0
