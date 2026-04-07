#Requires -Version 5.1

<#
.SYNOPSIS
    Adds Windows Defender exclusion and runs tests

.DESCRIPTION
    This script attempts to add Windows Defender exclusion for test binaries
    and then runs all tests. It will prompt for Administrator elevation if needed.

.EXAMPLE
    .\scripts\run-tests-with-exclusion.ps1
#>

param(
    [string]$TestFilter = ""
)

$ErrorActionPreference = "Stop"

# Check if running as Administrator
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Host "⚠️  Not running as Administrator" -ForegroundColor Yellow
    Write-Host "Attempting to restart with elevation..." -ForegroundColor Cyan

    $arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`""
    if ($TestFilter) {
        $arguments += " -TestFilter `"$TestFilter`""
    }

    try {
        Start-Process powershell.exe -ArgumentList $arguments -Verb RunAs -Wait
        exit 0
    }
    catch {
        Write-Host "✗ Failed to elevate. Please run as Administrator manually." -ForegroundColor Red
        exit 1
    }
}

Write-Host "✓ Running as Administrator" -ForegroundColor Green

# Add Windows Defender exclusions
$projectRoot = Split-Path $PSScriptRoot -Parent
$exclusions = @(
    (Join-Path $projectRoot "tests\TradingSupervisorService.Tests\bin"),
    (Join-Path $projectRoot "tests\OptionsExecutionService.Tests\bin"),
    (Join-Path $projectRoot "tests\SharedKernel.Tests\bin")
)

Write-Host "`nAdding Windows Defender exclusions..." -ForegroundColor Yellow

foreach ($path in $exclusions) {
    if (Test-Path $path) {
        try {
            Add-MpPreference -ExclusionPath $path -ErrorAction SilentlyContinue
            Write-Host "✓ Added: $path" -ForegroundColor Green
        }
        catch {
            Write-Host "⚠️  Already excluded or error: $path" -ForegroundColor Yellow
        }
    }
}

Write-Host "`nCleaning and rebuilding..." -ForegroundColor Cyan
Write-Host "=" * 60

# Change to project root
Set-Location $projectRoot

# Clean all projects to remove previously compiled DLLs that were blocked
Write-Host "Cleaning solution..." -ForegroundColor Yellow
dotnet clean TradingSystem.sln --verbosity quiet

# Rebuild to generate fresh DLLs (after exclusions are in place)
Write-Host "Rebuilding solution..." -ForegroundColor Yellow
dotnet build TradingSystem.sln --verbosity quiet

Write-Host "`nRunning tests..." -ForegroundColor Cyan
Write-Host "=" * 60

# Run tests
if ($TestFilter) {
    dotnet test TradingSystem.sln --filter $TestFilter --verbosity normal --no-build
}
else {
    dotnet test TradingSystem.sln --verbosity normal --no-build
}

$exitCode = $LASTEXITCODE

Write-Host "`n" + ("=" * 60)
if ($exitCode -eq 0) {
    Write-Host "✓ All tests passed!" -ForegroundColor Green
}
else {
    Write-Host "⚠️  Some tests failed (exit code: $exitCode)" -ForegroundColor Yellow
}

Write-Host "`nPress any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

exit $exitCode
