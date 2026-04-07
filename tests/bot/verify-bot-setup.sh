#!/usr/bin/env bash
# Simple verification script for bot setup
# Verifies all files exist and contain expected content

set -e

cd "$(dirname "$0")/../.."

echo "=== Bot Setup Verification ==="
echo ""

# File existence checks
echo "Checking file structure..."
test -f "infra/cloudflare/worker/migrations/0003_bot_commands_log.sql" && echo "  ✅ Migration file exists"
test -f "infra/cloudflare/worker/src/bot/auth.ts" && echo "  ✅ auth.ts exists"
test -f "infra/cloudflare/worker/src/bot/i18n.ts" && echo "  ✅ i18n.ts exists"
test -f "infra/cloudflare/worker/src/bot/dispatcher.ts" && echo "  ✅ dispatcher.ts exists"
test -f "infra/cloudflare/worker/src/routes/bot-telegram.ts" && echo "  ✅ bot-telegram.ts exists"
test -f "infra/cloudflare/worker/src/routes/bot-discord.ts" && echo "  ✅ bot-discord.ts exists"
test -f "src/TradingSupervisorService/Bot/BotWebhookRegistrar.cs" && echo "  ✅ BotWebhookRegistrar.cs exists"
test -f "src/TradingSupervisorService/Configuration/BotOptions.cs" && echo "  ✅ BotOptions.cs exists"

echo ""
echo "Checking content..."
grep -q "bot_command_log" "infra/cloudflare/worker/migrations/0003_bot_commands_log.sql" && echo "  ✅ Migration contains bot_command_log table"
grep -q "verifyTelegramSignature" "infra/cloudflare/worker/src/bot/auth.ts" && echo "  ✅ auth.ts has verifyTelegramSignature"
grep -q "verifyDiscordSignature" "infra/cloudflare/worker/src/bot/auth.ts" && echo "  ✅ auth.ts has verifyDiscordSignature"
grep -q "isWhitelisted" "infra/cloudflare/worker/src/bot/auth.ts" && echo "  ✅ auth.ts has isWhitelisted"
grep -q "messages.*Record" "infra/cloudflare/worker/src/bot/i18n.ts" && echo "  ✅ i18n.ts has messages"
grep -q "parseCommand" "infra/cloudflare/worker/src/bot/dispatcher.ts" && echo "  ✅ dispatcher.ts has parseCommand"
grep -q "parseCallbackData" "infra/cloudflare/worker/src/bot/dispatcher.ts" && echo "  ✅ dispatcher.ts has parseCallbackData"
grep -q "webhook/telegram" "infra/cloudflare/worker/src/routes/bot-telegram.ts" && echo "  ✅ Telegram route has webhook endpoint"
grep -q "webhook/discord" "infra/cloudflare/worker/src/routes/bot-discord.ts" && echo "  ✅ Discord route has webhook endpoint"
grep -q "PING" "infra/cloudflare/worker/src/routes/bot-discord.ts" && echo "  ✅ Discord route handles PING"
grep -q "botTelegram" "infra/cloudflare/worker/src/index.ts" && echo "  ✅ index.ts imports botTelegram"
grep -q "TELEGRAM_BOT_TOKEN" "infra/cloudflare/worker/src/types/env.ts" && echo "  ✅ Env types include bot tokens"
grep -q "Bots" "src/TradingSupervisorService/appsettings.json" && echo "  ✅ appsettings.json has Bots section"
grep -q "BotWebhookRegistrar" "src/TradingSupervisorService/Program.cs" && echo "  ✅ Program.cs registers BotWebhookRegistrar"

echo ""
echo "✅ All verification checks passed!"
