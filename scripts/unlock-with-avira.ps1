#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Test Suite con AVIRA Antivirus

.DESCRIPTION
    Script per eseguire test quando AVIRA Security blocca le DLL.
    AVIRA controlla Smart App Control e blocca DLL non firmate.

.EXAMPLE
    .\unlock-with-avira.ps1

.NOTES
    IMPORTANTE: Aggiungi MANUALMENTE le esclusioni in AVIRA prima di eseguire!
    AVIRA → Settings → Exceptions → Add: C:\Users\lopad\Documents\DocLore\Visual Basic\_NET\Applicazioni\trading-system
#>

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host "`n🔧 $Message" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "✅ $Message" -ForegroundColor Green
}

function Write-Failure {
    param([string]$Message)
    Write-Host "❌ $Message" -ForegroundColor Red
}

function Write-Warning-Custom {
    param([string]$Message)
    Write-Host "⚠️  $Message" -ForegroundColor Yellow
}

try {
    Write-Host @"
╔═══════════════════════════════════════════════════════════╗
║  AVIRA-Aware Test Suite Runner                           ║
╚═══════════════════════════════════════════════════════════╝
"@ -ForegroundColor Magenta

    # Get project root
    $currentDir = Get-Location
    if (Test-Path (Join-Path $currentDir "TradingSystem.sln")) {
        $projectRoot = $currentDir
    }
    elseif (Test-Path (Join-Path (Split-Path -Parent $currentDir) "TradingSystem.sln")) {
        $projectRoot = Split-Path -Parent $currentDir
    }
    else {
        Write-Failure "Cannot find TradingSystem.sln"
        throw "Project root detection failed"
    }

    Write-Host "Project Root: $projectRoot" -ForegroundColor Gray

    # Check if AVIRA is running
    Write-Step "Checking AVIRA Security status..."
    $aviraProcesses = Get-Process | Where-Object { $_.ProcessName -like "Avira*" }
    if ($aviraProcesses) {
        Write-Warning-Custom "AVIRA Security is running ($($aviraProcesses.Count) processes)"
        Write-Host ""
        Write-Host "⚠️  IMPORTANT: Add exceptions in AVIRA BEFORE running tests!" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "How to add exceptions in AVIRA:" -ForegroundColor Cyan
        Write-Host "  1. Open AVIRA (system tray icon)" -ForegroundColor White
        Write-Host "  2. Settings (⚙️) → General → Exceptions" -ForegroundColor White
        Write-Host "  3. Add this folder:" -ForegroundColor White
        Write-Host "     $projectRoot" -ForegroundColor Green
        Write-Host ""

        $response = Read-Host "Have you added the exception in AVIRA? (y/n)"
        if ($response -ne 'y' -and $response -ne 'Y') {
            Write-Warning-Custom "Please add the exception in AVIRA first, then run this script again"
            exit 0
        }
    }
    else {
        Write-Success "AVIRA not detected (or not running)"
    }

    # Clean
    Write-Step "Cleaning solution..."
    Push-Location $projectRoot
    try {
        dotnet clean | Out-Null
        Write-Success "Clean completed"
    }
    finally {
        Pop-Location
    }

    # Build
    Write-Step "Building solution..."
    Push-Location $projectRoot
    try {
        $buildOutput = dotnet build --no-restore 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Success "Build completed"
        }
        else {
            Write-Failure "Build failed"
            Write-Host $buildOutput -ForegroundColor Red
            throw "Build failed"
        }
    }
    finally {
        Pop-Location
    }

    # Unblock DLLs (in case AVIRA marked them)
    Write-Step "Unblocking DLLs..."
    $testDirs = @(
        (Join-Path $projectRoot "tests\OptionsExecutionService.Tests\bin"),
        (Join-Path $projectRoot "tests\TradingSupervisorService.Tests\bin"),
        (Join-Path $projectRoot "tests\SharedKernel.Tests\bin")
    )

    $unblocked = 0
    foreach ($dir in $testDirs) {
        if (Test-Path $dir) {
            Get-ChildItem -Path $dir -Recurse -Filter "*.dll" -ErrorAction SilentlyContinue | ForEach-Object {
                Unblock-File -Path $_.FullName -ErrorAction SilentlyContinue
                $unblocked++
            }
        }
    }
    Write-Success "Unblocked $unblocked DLL files"

    # Run tests
    Write-Step "Running test suite..."
    Push-Location $projectRoot
    try {
        Write-Host "Running: dotnet test --no-build --verbosity normal" -ForegroundColor Gray
        Write-Host ""

        $testOutput = dotnet test --no-build --verbosity normal 2>&1
        $testExitCode = $LASTEXITCODE

        # Display output
        $testOutput | ForEach-Object { Write-Host $_ }

        Write-Host ""

        # Parse results (sum across all projects)
        $allPassed = $testOutput | Select-String "Superati:\s+(\d+)" -AllMatches
        $allFailed = $testOutput | Select-String "Non superati:\s+(\d+)" -AllMatches
        $allTotal = $testOutput | Select-String "Totale:\s+(\d+)" -AllMatches

        if ($allPassed -and $allTotal) {
            $passed = ($allPassed.Matches | ForEach-Object { [int]$_.Groups[1].Value } | Measure-Object -Sum).Sum
            $failed = if ($allFailed) { ($allFailed.Matches | ForEach-Object { [int]$_.Groups[1].Value } | Measure-Object -Sum).Sum } else { 0 }
            $total = ($allTotal.Matches | ForEach-Object { [int]$_.Groups[1].Value } | Measure-Object -Sum).Sum

            $passRate = if ($total -gt 0) { [math]::Round(($passed / $total) * 100, 1) } else { 0 }

            Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
            Write-Host "TEST RESULTS:" -ForegroundColor Cyan
            Write-Host "  Total:  $total" -ForegroundColor White
            Write-Host "  Passed: $passed" -ForegroundColor Green
            Write-Host "  Failed: $failed" -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Red" })
            Write-Host "  Pass Rate: $passRate%" -ForegroundColor $(if ($passRate -ge 99) { "Green" } elseif ($passRate -ge 95) { "Yellow" } else { "Red" })
            Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan

            # Check if OptionsExecutionService.Tests still blocked
            $optionsBlocked = $testOutput | Select-String "OptionsExecutionService.Tests.*0x800711C7"
            if ($optionsBlocked) {
                Write-Host ""
                Write-Warning-Custom "OptionsExecutionService.Tests is STILL BLOCKED by AVIRA!"
                Write-Host ""
                Write-Host "Solutions:" -ForegroundColor Yellow
                Write-Host "  1. Add exception in AVIRA (Settings → General → Exceptions)" -ForegroundColor White
                Write-Host "  2. Disable AVIRA Real-Time Protection for 10 minutes" -ForegroundColor White
                Write-Host "  3. Use GitHub Actions on Linux (no AVIRA there!)" -ForegroundColor White
                Write-Host ""
            }
            elseif ($testExitCode -eq 0) {
                Write-Success "ALL TESTS PASSED! 🎉"
            }
        }
    }
    finally {
        Pop-Location
    }

    Write-Host ""
    Write-Success "Script completed!"
    Write-Host ""

}
catch {
    Write-Failure "Script failed: $($_.Exception.Message)"
    exit 1
}
