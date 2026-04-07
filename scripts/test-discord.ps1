#!/usr/bin/env pwsh
# Test Discord Bot Configuration
# Tests if Discord bot can send messages to configured channel

param(
    [string]$BotToken,
    [string]$ChannelId
)

# Load from .dev.vars if not provided
if (-not $BotToken -or -not $ChannelId) {
    $devVarsPath = Join-Path $PSScriptRoot ".." "infra" "cloudflare" "worker" ".dev.vars"

    if (Test-Path $devVarsPath) {
        Write-Host "Loading config from $devVarsPath..." -ForegroundColor Cyan

        $content = Get-Content $devVarsPath
        foreach ($line in $content) {
            if ($line -match '^DISCORD_BOT_TOKEN=(.+)$') {
                $BotToken = $matches[1]
            }
            if ($line -match '^DISCORD_CHANNEL_ID=(.+)$') {
                $ChannelId = $matches[1]
            }
        }
    }
}

# Validate inputs
if (-not $BotToken -or $BotToken -eq "REPLACE_WITH_YOUR_BOT_TOKEN") {
    Write-Host "ERROR: Discord Bot Token not configured" -ForegroundColor Red
    Write-Host "Edit infra/cloudflare/worker/.dev.vars and set DISCORD_BOT_TOKEN" -ForegroundColor Yellow
    exit 1
}

if (-not $ChannelId -or $ChannelId -eq "123456789012345678") {
    Write-Host "ERROR: Discord Channel ID not configured" -ForegroundColor Red
    Write-Host "Edit infra/cloudflare/worker/.dev.vars and set DISCORD_CHANNEL_ID" -ForegroundColor Yellow
    exit 1
}

Write-Host "Testing Discord configuration..." -ForegroundColor Cyan
Write-Host "Channel ID: $ChannelId" -ForegroundColor Gray

# Send test message via Discord API
$url = "https://discord.com/api/v10/channels/$ChannelId/messages"
$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
$json = @{
    content = "✅ Test from Trading System - $timestamp"
} | ConvertTo-Json

try {
    # Trim token to remove any whitespace
    $BotToken = $BotToken.Trim()

    $headers = @{
        "Authorization" = "Bot $BotToken"
        "Content-Type" = "application/json"
        "User-Agent" = "TradingSystem/1.0"
    }

    $response = Invoke-WebRequest -Uri $url -Method Post -Headers $headers -Body $json -UseBasicParsing -ErrorAction Stop
    $responseData = $response.Content | ConvertFrom-Json

    Write-Host "SUCCESS! Message sent to Discord" -ForegroundColor Green
    Write-Host "Message ID: $($responseData.id)" -ForegroundColor Green
    Write-Host "Check your Discord channel for the test message." -ForegroundColor Cyan
}
catch {
    Write-Host "FAILED to send message" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red

    if ($_.Exception.Response.StatusCode -eq 401) {
        Write-Host "Bot token is invalid or expired" -ForegroundColor Yellow
    }
    elseif ($_.Exception.Response.StatusCode -eq 403) {
        Write-Host "Bot doesn't have permission to send messages in this channel" -ForegroundColor Yellow
        Write-Host "Make sure bot is invited to the server and has 'Send Messages' permission" -ForegroundColor Yellow
    }
    elseif ($_.Exception.Response.StatusCode -eq 404) {
        Write-Host "Channel ID not found" -ForegroundColor Yellow
        Write-Host "Verify channel ID is correct (right-click channel → Copy Channel ID)" -ForegroundColor Yellow
    }

    exit 1
}
