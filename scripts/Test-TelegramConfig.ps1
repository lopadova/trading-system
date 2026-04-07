#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Test Telegram Bot configuration and send a test message.

.DESCRIPTION
    This script:
    1. Reads Telegram configuration from appsettings.Production.json
    2. Validates the configuration
    3. Sends a test message to the configured Telegram chat
    4. Verifies the message was sent successfully

.PARAMETER ConfigPath
    Path to appsettings.Production.json file.
    Default: ../src/TradingSupervisorService/bin/Release/net10.0/appsettings.Production.json

.EXAMPLE
    .\Test-TelegramConfig.ps1

.EXAMPLE
    .\Test-TelegramConfig.ps1 -ConfigPath "C:\path\to\appsettings.Production.json"
#>

param(
    [string]$ConfigPath = "$PSScriptRoot\..\src\TradingSupervisorService\bin\Release\net10.0\appsettings.Production.json"
)

Write-Host "=== Telegram Configuration Test ===" -ForegroundColor Cyan
Write-Host ""

# 1. Check if config file exists
if (-not (Test-Path $ConfigPath)) {
    Write-Host "❌ Configuration file not found: $ConfigPath" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please ensure appsettings.Production.json exists with Telegram configuration." -ForegroundColor Yellow
    exit 1
}

Write-Host "✅ Loading configuration from: $ConfigPath" -ForegroundColor Green

# 2. Load configuration
try {
    $config = Get-Content $ConfigPath -Raw | ConvertFrom-Json
} catch {
    Write-Host "❌ Failed to parse configuration file: $_" -ForegroundColor Red
    exit 1
}

# 3. Extract Telegram configuration
$telegram = $config.Telegram

if ($null -eq $telegram) {
    Write-Host "❌ Telegram section not found in configuration" -ForegroundColor Red
    exit 1
}

$enabled = $telegram.Enabled
$botToken = $telegram.BotToken
$chatId = $telegram.ChatId

Write-Host ""
Write-Host "📋 Configuration loaded:" -ForegroundColor Cyan
Write-Host "   Enabled: $enabled" -ForegroundColor White
if ([string]::IsNullOrEmpty($botToken)) {
    Write-Host "   BotToken: ❌ NOT SET" -ForegroundColor Red
} else {
    Write-Host "   BotToken: ✅ SET (length: $($botToken.Length))" -ForegroundColor Green
}
Write-Host "   ChatId: $chatId" -ForegroundColor White
Write-Host ""

# 4. Validate configuration
if (-not $enabled) {
    Write-Host "⚠️  Telegram is DISABLED in configuration." -ForegroundColor Yellow
    Write-Host "   Set Telegram:Enabled to true to enable alerting." -ForegroundColor Yellow
    exit 0
}

if ([string]::IsNullOrEmpty($botToken)) {
    Write-Host "❌ BotToken is NOT SET in configuration." -ForegroundColor Red
    Write-Host "   Please configure Telegram:BotToken in appsettings.Production.json" -ForegroundColor Yellow
    exit 1
}

if ($chatId -eq 0) {
    Write-Host "❌ ChatId is NOT SET in configuration." -ForegroundColor Red
    Write-Host "   Please configure Telegram:ChatId in appsettings.Production.json" -ForegroundColor Yellow
    exit 1
}

# 5. Prepare test message
$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
$message = "🧪 *Test Alert from Trading System*`n`n" +
           "This is a test message sent at $timestamp UTC.`n`n" +
           "If you received this, your Telegram configuration is working correctly! ✅`n`n" +
           "📋 Configuration verified:`n" +
           "• BotToken: Valid`n" +
           "• ChatId: $chatId`n" +
           "• Network: OK`n" +
           "• Telegram Bot API: Responding`n`n" +
           "Service: TradingSupervisorService`n" +
           "Environment: Production"

Write-Host "📨 Preparing test message..." -ForegroundColor Cyan
Write-Host ""

# 6. Send test message via Telegram Bot API
Write-Host "🚀 Sending test message to Telegram..." -ForegroundColor Cyan
Write-Host "   (This may take a few seconds)" -ForegroundColor Gray
Write-Host ""

$apiUrl = "https://api.telegram.org/bot$botToken/sendMessage"

$body = @{
    chat_id = $chatId
    text = $message
    parse_mode = "Markdown"
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri $apiUrl -Method Post -Body $body -ContentType "application/json"

    if ($response.ok) {
        $messageId = $response.result.message_id

        Write-Host "✅ SUCCESS! Test message sent to Telegram!" -ForegroundColor Green
        Write-Host ""
        Write-Host "📬 Message ID: $messageId" -ForegroundColor White
        Write-Host ""
        Write-Host "🎉 Check your Telegram chat - you should see the test message!" -ForegroundColor Green
        Write-Host ""
        Write-Host "Configuration verification PASSED:" -ForegroundColor Green
        Write-Host "   ✅ BotToken is valid" -ForegroundColor Green
        Write-Host "   ✅ ChatId is correct" -ForegroundColor Green
        Write-Host "   ✅ Network connectivity OK" -ForegroundColor Green
        Write-Host "   ✅ Telegram Bot API responding" -ForegroundColor Green
        Write-Host ""

        exit 0
    } else {
        Write-Host "❌ FAILED! Telegram API returned ok=false" -ForegroundColor Red
        Write-Host ""
        Write-Host "Response:" -ForegroundColor Yellow
        Write-Host ($response | ConvertTo-Json -Depth 10) -ForegroundColor Gray
        exit 1
    }

} catch {
    Write-Host "❌ EXCEPTION occurred while sending message:" -ForegroundColor Red
    Write-Host ""
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red

    if ($_.ErrorDetails) {
        Write-Host ""
        Write-Host "Details:" -ForegroundColor Yellow
        try {
            $errorJson = $_.ErrorDetails.Message | ConvertFrom-Json
            Write-Host "   Error Code: $($errorJson.error_code)" -ForegroundColor Gray
            Write-Host "   Description: $($errorJson.description)" -ForegroundColor Gray
        } catch {
            Write-Host $_.ErrorDetails.Message -ForegroundColor Gray
        }
    }

    Write-Host ""
    Write-Host "Possible causes:" -ForegroundColor Yellow
    Write-Host "   - Invalid BotToken format or token was revoked" -ForegroundColor Gray
    Write-Host "   - Network firewall blocking Telegram API (api.telegram.org)" -ForegroundColor Gray
    Write-Host "   - Bot was deleted or blocked by user" -ForegroundColor Gray
    Write-Host "   - ChatId is invalid or bot doesn't have access to the chat" -ForegroundColor Gray
    Write-Host ""

    exit 1
}
