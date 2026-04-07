# Add-TestExclusion.ps1
# Run this script as Administrator to add Windows Defender exclusion for test binaries

<#
.SYNOPSIS
    Adds Windows Defender exclusions for trading-system test binaries.

.DESCRIPTION
    Windows Defender Application Control may block test DLLs from loading.
    This script adds the test bin directories to Windows Defender exclusions.

    MUST BE RUN AS ADMINISTRATOR.

.EXAMPLE
    # Run in PowerShell as Administrator:
    .\scripts\Add-TestExclusion.ps1
#>

#Requires -RunAsAdministrator

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path $PSScriptRoot -Parent
$testBinPath = Join-Path $projectRoot "tests\TradingSupervisorService.Tests\bin"

Write-Host "Adding Windows Defender exclusion for test binaries..." -ForegroundColor Yellow
Write-Host "Path: $testBinPath" -ForegroundColor Cyan

try {
    Add-MpPreference -ExclusionPath $testBinPath
    Write-Host "✓ Exclusion added successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "You can now run tests with:" -ForegroundColor White
    Write-Host "  dotnet test tests/TradingSupervisorService.Tests" -ForegroundColor Cyan
}
catch {
    Write-Host "✗ Failed to add exclusion:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ""
    Write-Host "Make sure you're running this script as Administrator." -ForegroundColor Yellow
    exit 1
}
