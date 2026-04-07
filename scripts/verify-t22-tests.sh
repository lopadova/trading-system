#!/usr/bin/env bash
# scripts/verify-t22-tests.sh
# Executes all integration tests for Task T-22 and reports results

set -euo pipefail

TASK_ID="T-22"
PASSED=0
FAILED=0
RESULTS_FILE="./logs/${TASK_ID}-test-results.txt"

# Create logs directory if it doesn't exist
mkdir -p ./logs

# Clear previous results
> "$RESULTS_FILE"

echo "========================================" | tee -a "$RESULTS_FILE"
echo "Task ${TASK_ID} Integration Test Verification" | tee -a "$RESULTS_FILE"
echo "Timestamp: $(date -Iseconds)" | tee -a "$RESULTS_FILE"
echo "========================================" | tee -a "$RESULTS_FILE"
echo "" | tee -a "$RESULTS_FILE"

# Function to run a test and report result
run_test() {
    local test_id="$1"
    local description="$2"
    local filter="$3"

    echo "Running ${test_id}: ${description}..." | tee -a "$RESULTS_FILE"

    if dotnet test --filter "${filter}" --logger "console;verbosity=minimal" > /dev/null 2>&1; then
        echo "✅ ${test_id}: PASS — ${description}" | tee -a "$RESULTS_FILE"
        ((PASSED++))
    else
        echo "❌ ${test_id}: FAIL — ${description}" | tee -a "$RESULTS_FILE"
        ((FAILED++))
    fi
}

# TradingSupervisorService Program Integration Tests
echo "=== TradingSupervisorService: Program Integration Tests ===" | tee -a "$RESULTS_FILE"
run_test "TEST-22-01" "All required services registered in DI" "TestId=TEST-22-01"
run_test "TEST-22-02" "Service startup validates configuration" "TestId=TEST-22-02"
run_test "TEST-22-03" "IBKR client is singleton" "TestId=TEST-22-03"
run_test "TEST-22-04" "Repository services registered" "TestId=TEST-22-04"
run_test "TEST-22-05" "Metrics collector available" "TestId=TEST-22-05"
run_test "TEST-22-06" "HttpClientFactory registered" "TestId=TEST-22-06"
run_test "TEST-22-07" "TelegramAlerter available" "TestId=TEST-22-07"
run_test "TEST-22-08" "Database connection factory creates valid connections" "TestId=TEST-22-08"
run_test "TEST-22-09" "Positions repository uses separate database" "TestId=TEST-22-09"
run_test "TEST-22-10" "All hosted services registered" "TestId=TEST-22-10"
echo "" | tee -a "$RESULTS_FILE"

# TradingSupervisorService Migration Tests
echo "=== TradingSupervisorService: Migration Integration Tests ===" | tee -a "$RESULTS_FILE"
run_test "TEST-22-11" "All supervisor migrations apply successfully" "TestId=TEST-22-11"
run_test "TEST-22-12" "Migration 001 creates heartbeats table" "TestId=TEST-22-12"
run_test "TEST-22-13" "Migration 001 creates outbox table" "TestId=TEST-22-13"
run_test "TEST-22-14" "Migration 001 creates alerts table" "TestId=TEST-22-14"
run_test "TEST-22-15" "Migration 002 creates ivts_snapshots table" "TestId=TEST-22-15"
echo "" | tee -a "$RESULTS_FILE"

# OptionsExecutionService Migration Tests
echo "=== OptionsExecutionService: Migration Integration Tests ===" | tee -a "$RESULTS_FILE"
run_test "TEST-22-16" "All options migrations apply successfully" "TestId=TEST-22-16"
run_test "TEST-22-17" "Migration 001 creates campaigns table" "TestId=TEST-22-17"
run_test "TEST-22-18" "Migration 001 creates positions table" "TestId=TEST-22-18"
run_test "TEST-22-19" "Migration 002 adds greeks columns" "TestId=TEST-22-19"
run_test "TEST-22-20" "Migration 003 creates order_tracking table" "TestId=TEST-22-20"
echo "" | tee -a "$RESULTS_FILE"

# TradingSupervisorService Worker Lifecycle Tests
echo "=== TradingSupervisorService: Worker Lifecycle Tests ===" | tee -a "$RESULTS_FILE"
run_test "TEST-22-21" "HeartbeatWorker starts and executes cycle" "TestId=TEST-22-21"
run_test "TEST-22-22" "OutboxSyncWorker starts and stops gracefully" "TestId=TEST-22-22"
run_test "TEST-22-23" "TelegramWorker handles cancellation correctly" "TestId=TEST-22-23"
run_test "TEST-22-24" "LogReaderWorker starts with valid configuration" "TestId=TEST-22-24"
run_test "TEST-22-25" "Multiple workers can run concurrently" "TestId=TEST-22-25"
echo "" | tee -a "$RESULTS_FILE"

# TradingSupervisorService Repository Tests
echo "=== TradingSupervisorService: Repository Integration Tests ===" | tee -a "$RESULTS_FILE"
run_test "TEST-22-26" "HeartbeatRepository inserts and retrieves metrics" "TestId=TEST-22-26"
run_test "TEST-22-27" "OutboxRepository enqueues and dequeues events" "TestId=TEST-22-27"
run_test "TEST-22-28" "AlertRepository creates and retrieves alerts" "TestId=TEST-22-28"
run_test "TEST-22-29" "LogReaderStateRepository persists and loads state" "TestId=TEST-22-29"
run_test "TEST-22-30" "IvtsRepository stores and queries snapshots" "TestId=TEST-22-30"
echo "" | tee -a "$RESULTS_FILE"

# OptionsExecutionService Repository Tests
echo "=== OptionsExecutionService: Repository Integration Tests ===" | tee -a "$RESULTS_FILE"
run_test "TEST-22-31" "CampaignRepository creates and retrieves campaigns" "TestId=TEST-22-31"
run_test "TEST-22-32" "CampaignRepository lists active campaigns" "TestId=TEST-22-32"
run_test "TEST-22-33" "CampaignRepository updates campaign status" "TestId=TEST-22-33"
run_test "TEST-22-34" "OrderTrackingRepository creates and tracks orders" "TestId=TEST-22-34"
run_test "TEST-22-35" "OrderTrackingRepository updates order status" "TestId=TEST-22-35"
echo "" | tee -a "$RESULTS_FILE"

# Summary
echo "========================================" | tee -a "$RESULTS_FILE"
echo "Test Results Summary" | tee -a "$RESULTS_FILE"
echo "========================================" | tee -a "$RESULTS_FILE"
echo "Total Passed: ${PASSED}" | tee -a "$RESULTS_FILE"
echo "Total Failed: ${FAILED}" | tee -a "$RESULTS_FILE"
echo "Total Tests:  $((PASSED + FAILED))" | tee -a "$RESULTS_FILE"
echo "" | tee -a "$RESULTS_FILE"

if [ "$FAILED" -gt 0 ]; then
    echo "❌ TASK ${TASK_ID}: FAILED with ${FAILED} failing test(s)" | tee -a "$RESULTS_FILE"
    echo "Check ${RESULTS_FILE} for details" | tee -a "$RESULTS_FILE"
    exit 1
else
    echo "✅ TASK ${TASK_ID}: ALL TESTS PASSED (${PASSED}/${PASSED})" | tee -a "$RESULTS_FILE"
    exit 0
fi
