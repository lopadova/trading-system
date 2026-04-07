# Simple Telegram test script
param(
    [string]$ConfigPath = "$PSScriptRoot\..\src\TradingSupervisorService\bin\Release\net10.0\appsettings.Production.json"
)

Write-Host "=== Telegram Test ===" -ForegroundColor Cyan
Write-Host ""

# Load config
if (-not (Test-Path $ConfigPath)) {
    Write-Host "Config not found: $ConfigPath" -ForegroundColor Red
    exit 1
}

$config = Get-Content $ConfigPath -Raw | ConvertFrom-Json
$botToken = $config.Telegram.BotToken
$chatId = $config.Telegram.ChatId
$enabled = $config.Telegram.Enabled

Write-Host "Enabled: $enabled"
Write-Host "BotToken: $($botToken.Substring(0, 10))..."
Write-Host "ChatId: $chatId"
Write-Host ""

if (-not $enabled) {
    Write-Host "Telegram is disabled" -ForegroundColor Yellow
    exit 0
}

# Send test message
$url = "https://api.telegram.org/bot$botToken/sendMessage"
$text = "Test from Trading System - $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"

$json = @{
    chat_id = [int]$chatId
    text = $text
} | ConvertTo-Json

Write-Host "Sending test message..." -ForegroundColor Cyan

try {
    $result = Invoke-RestMethod -Uri $url -Method Post -Body $json -ContentType "application/json"

    if ($result.ok) {
        Write-Host "SUCCESS! Message sent, ID: $($result.result.message_id)" -ForegroundColor Green
        Write-Host "Check your Telegram chat!" -ForegroundColor Green
        exit 0
    }
    else {
        Write-Host "Failed: $($result | ConvertTo-Json)" -ForegroundColor Red
        exit 1
    }
}
catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red

    if ($_.ErrorDetails) {
        $errorObj = $_.ErrorDetails.Message | ConvertFrom-Json
        Write-Host "Telegram API error: $($errorObj.description)" -ForegroundColor Red
    }

    exit 1
}
