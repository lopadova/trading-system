#!/usr/bin/env bash
# TEST-BOT-01-XX: Bot Setup Tests
# Tests bot authentication, command parsing, and webhook handling

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

echo "=== TEST-BOT-01: Bot Setup Tests ==="
echo ""

# Test tracking
TESTS_PASSED=0
TESTS_FAILED=0
FAILED_TESTS=()

run_test() {
    local test_id="$1"
    local test_name="$2"
    local test_command="$3"

    echo "Running $test_id: $test_name"

    if eval "$test_command"; then
        echo "  ✅ PASS"
        ((TESTS_PASSED++))
    else
        echo "  ❌ FAIL"
        ((TESTS_FAILED++))
        FAILED_TESTS+=("$test_id: $test_name")
    fi
    echo ""
}

# TEST-BOT-01-01: Verify migration file exists
run_test "TEST-BOT-01-01" "Migration 0003_bot_commands_log.sql exists" \
    "test -f '$PROJECT_ROOT/infra/cloudflare/worker/migrations/0003_bot_commands_log.sql'"

# TEST-BOT-01-02: Verify bot auth module exists
run_test "TEST-BOT-01-02" "Bot auth.ts module exists" \
    "test -f '$PROJECT_ROOT/infra/cloudflare/worker/src/bot/auth.ts'"

# TEST-BOT-01-03: Verify bot i18n module exists
run_test "TEST-BOT-01-03" "Bot i18n.ts module exists" \
    "test -f '$PROJECT_ROOT/infra/cloudflare/worker/src/bot/i18n.ts'"

# TEST-BOT-01-04: Verify bot dispatcher module exists
run_test "TEST-BOT-01-04" "Bot dispatcher.ts module exists" \
    "test -f '$PROJECT_ROOT/infra/cloudflare/worker/src/bot/dispatcher.ts'"

# TEST-BOT-01-05: Verify Telegram bot route exists
run_test "TEST-BOT-01-05" "Telegram bot route exists" \
    "test -f '$PROJECT_ROOT/infra/cloudflare/worker/src/routes/bot-telegram.ts'"

# TEST-BOT-01-06: Verify Discord bot route exists
run_test "TEST-BOT-01-06" "Discord bot route exists" \
    "test -f '$PROJECT_ROOT/infra/cloudflare/worker/src/routes/bot-discord.ts'"

# TEST-BOT-01-07: Verify BotWebhookRegistrar exists
run_test "TEST-BOT-01-07" "BotWebhookRegistrar.cs exists" \
    "test -f '$PROJECT_ROOT/src/TradingSupervisorService/Bot/BotWebhookRegistrar.cs'"

# TEST-BOT-01-08: Verify BotOptions exists
run_test "TEST-BOT-01-08" "BotOptions.cs exists" \
    "test -f '$PROJECT_ROOT/src/TradingSupervisorService/Configuration/BotOptions.cs'"

# TEST-BOT-01-09: Check migration SQL syntax
run_test "TEST-BOT-01-09" "Migration SQL contains bot_command_log table" \
    "grep -q 'CREATE TABLE.*bot_command_log' '$PROJECT_ROOT/infra/cloudflare/worker/migrations/0003_bot_commands_log.sql'"

# TEST-BOT-01-10: Check auth.ts exports verifyTelegramSignature
run_test "TEST-BOT-01-10" "auth.ts exports verifyTelegramSignature" \
    "grep -q 'export.*function verifyTelegramSignature' '$PROJECT_ROOT/infra/cloudflare/worker/src/bot/auth.ts'"

# TEST-BOT-01-11: Check auth.ts exports verifyDiscordSignature
run_test "TEST-BOT-01-11" "auth.ts exports verifyDiscordSignature" \
    "grep -q 'export.*function verifyDiscordSignature' '$PROJECT_ROOT/infra/cloudflare/worker/src/bot/auth.ts'"

# TEST-BOT-01-12: Check auth.ts exports isWhitelisted
run_test "TEST-BOT-01-12" "auth.ts exports isWhitelisted" \
    "grep -q 'export.*function isWhitelisted' '$PROJECT_ROOT/infra/cloudflare/worker/src/bot/auth.ts'"

# TEST-BOT-01-13: Check i18n.ts exports messages
run_test "TEST-BOT-01-13" "i18n.ts exports messages" \
    "grep -q 'export.*messages.*Record' '$PROJECT_ROOT/infra/cloudflare/worker/src/bot/i18n.ts'"

# TEST-BOT-01-14: Check i18n.ts has IT translations
run_test "TEST-BOT-01-14" "i18n.ts has IT translations" \
    "grep -q \"it:\" '$PROJECT_ROOT/infra/cloudflare/worker/src/bot/i18n.ts'"

# TEST-BOT-01-15: Check i18n.ts has EN translations
run_test "TEST-BOT-01-15" "i18n.ts has EN translations" \
    "grep -q \"en:\" '$PROJECT_ROOT/infra/cloudflare/worker/src/bot/i18n.ts'"

# TEST-BOT-01-16: Check dispatcher.ts exports parseCommand
run_test "TEST-BOT-01-16" "dispatcher.ts exports parseCommand" \
    "grep -q 'export.*function parseCommand' '$PROJECT_ROOT/infra/cloudflare/worker/src/bot/dispatcher.ts'"

# TEST-BOT-01-17: Check dispatcher.ts exports parseCallbackData
run_test "TEST-BOT-01-17" "dispatcher.ts exports parseCallbackData" \
    "grep -q 'export.*function parseCallbackData' '$PROJECT_ROOT/infra/cloudflare/worker/src/bot/dispatcher.ts'"

# TEST-BOT-01-18: Check dispatcher.ts exports dispatchCommand
run_test "TEST-BOT-01-18" "dispatcher.ts exports dispatchCommand" \
    "grep -q 'export.*function dispatchCommand' '$PROJECT_ROOT/infra/cloudflare/worker/src/bot/dispatcher.ts'"

# TEST-BOT-01-19: Check Telegram route handles webhook
run_test "TEST-BOT-01-19" "Telegram route has webhook endpoint" \
    "grep -q '/webhook/telegram' '$PROJECT_ROOT/infra/cloudflare/worker/src/routes/bot-telegram.ts'"

# TEST-BOT-01-20: Check Discord route handles webhook
run_test "TEST-BOT-01-20" "Discord route has webhook endpoint" \
    "grep -q '/webhook/discord' '$PROJECT_ROOT/infra/cloudflare/worker/src/routes/bot-discord.ts'"

# TEST-BOT-01-21: Check Discord route handles PING
run_test "TEST-BOT-01-21" "Discord route handles PING interaction" \
    "grep -q 'PING.*PONG' '$PROJECT_ROOT/infra/cloudflare/worker/src/routes/bot-discord.ts'"

# TEST-BOT-01-22: Check index.ts imports bot routes
run_test "TEST-BOT-01-22" "index.ts imports bot routes" \
    "grep -q 'botTelegram\|botDiscord' '$PROJECT_ROOT/infra/cloudflare/worker/src/index.ts'"

# TEST-BOT-01-23: Check Env types include bot secrets
run_test "TEST-BOT-01-23" "Env types include TELEGRAM_BOT_TOKEN" \
    "grep -q 'TELEGRAM_BOT_TOKEN' '$PROJECT_ROOT/infra/cloudflare/worker/src/types/env.ts'"

# TEST-BOT-01-24: Check appsettings.json has Bots section
run_test "TEST-BOT-01-24" "appsettings.json has Bots section" \
    "grep -q '\"Bots\"' '$PROJECT_ROOT/src/TradingSupervisorService/appsettings.json'"

# TEST-BOT-01-25: Check Program.cs registers BotWebhookRegistrar
run_test "TEST-BOT-01-25" "Program.cs registers BotWebhookRegistrar" \
    "grep -q 'AddHostedService.*BotWebhookRegistrar' '$PROJECT_ROOT/src/TradingSupervisorService/Program.cs'"

# TEST-BOT-01-26: Check BotOptions has ActiveBot property
run_test "TEST-BOT-01-26" "BotOptions has ActiveBot property" \
    "grep -q 'ActiveBot' '$PROJECT_ROOT/src/TradingSupervisorService/Configuration/BotOptions.cs'"

# TEST-BOT-01-27: Check BotWebhookRegistrar implements IHostedService
run_test "TEST-BOT-01-27" "BotWebhookRegistrar implements IHostedService" \
    "grep -q 'IHostedService' '$PROJECT_ROOT/src/TradingSupervisorService/Bot/BotWebhookRegistrar.cs'"

# Summary
echo "========================================"
echo "Test Summary:"
echo "  Passed: $TESTS_PASSED"
echo "  Failed: $TESTS_FAILED"
echo "========================================"

if [ $TESTS_FAILED -gt 0 ]; then
    echo ""
    echo "Failed tests:"
    for test in "${FAILED_TESTS[@]}"; do
        echo "  - $test"
    done
    echo ""
    exit 1
fi

echo ""
echo "✅ All tests passed!"
exit 0
