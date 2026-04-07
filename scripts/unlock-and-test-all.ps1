#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Unlock Windows Defender + Run Full Test Suite

.DESCRIPTION
    Script completo per:
    1. Verificare privilegi Administrator
    2. Disabilitare Windows Defender Real-Time Protection
    3. Clean + Build solution
    4. Unblock tutte le DLL di test
    5. Eseguire test suite completa
    6. Riabilitare Windows Defender (sempre, anche se fallisce)

.EXAMPLE
    .\unlock-and-test-all.ps1

.NOTES
    Richiede: PowerShell Administrator
    Windows Defender si riabiliterà automaticamente anche senza -ReenableDefender
#>

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Colors for output
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

# Verify Administrator privileges
function Test-Administrator {
    $currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    return $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

# Main script
try {
    Write-Host @"
╔═══════════════════════════════════════════════════════════╗
║  Windows Defender Unlock + Full Test Suite Runner        ║
╚═══════════════════════════════════════════════════════════╝
"@ -ForegroundColor Magenta

    # Step 1: Verify Administrator
    Write-Step "Verifying Administrator privileges..."
    if (-not (Test-Administrator)) {
        Write-Failure "This script must be run as Administrator!"
        Write-Host ""
        Write-Host "Right-click PowerShell and select 'Run as Administrator', then run:" -ForegroundColor Yellow
        Write-Host "  .\scripts\unlock-and-test-all.ps1" -ForegroundColor White
        exit 1
    }
    Write-Success "Running as Administrator"

    # Get project root - search for TradingSystem.sln
    $currentDir = Get-Location

    # Check if TradingSystem.sln is in current directory
    if (Test-Path (Join-Path $currentDir "TradingSystem.sln")) {
        $projectRoot = $currentDir
    }
    # Check if TradingSystem.sln is in parent directory (we're in scripts/)
    elseif (Test-Path (Join-Path (Split-Path -Parent $currentDir) "TradingSystem.sln")) {
        $projectRoot = Split-Path -Parent $currentDir
    }
    # Check 2 levels up (in case we're in a subdirectory)
    elseif (Test-Path (Join-Path (Split-Path -Parent (Split-Path -Parent $currentDir)) "TradingSystem.sln")) {
        $projectRoot = Split-Path -Parent (Split-Path -Parent $currentDir)
    }
    else {
        Write-Failure "Cannot find TradingSystem.sln"
        Write-Host "Current directory: $currentDir" -ForegroundColor Yellow
        Write-Host "Please run this script from the project root or scripts directory" -ForegroundColor Yellow
        throw "Project root detection failed - TradingSystem.sln not found"
    }

    Write-Host "Project Root: $projectRoot" -ForegroundColor Gray

    # Step 2: Check Windows Defender status BEFORE
    Write-Step "Checking Windows Defender status..."
    try {
        $defenderStatus = Get-MpPreference
        $realtimeMonitoring = $defenderStatus.DisableRealtimeMonitoring
        Write-Host "Real-Time Protection: $(if ($realtimeMonitoring) { 'DISABLED' } else { 'ENABLED' })" -ForegroundColor Gray
    }
    catch {
        Write-Warning-Custom "Could not query Windows Defender status (might not be available)"
    }

    # Step 3: Disable Windows Defender Real-Time Protection
    Write-Step "Disabling Windows Defender Real-Time Protection..."
    $defenderDisabled = $false
    try {
        Set-MpPreference -DisableRealtimeMonitoring $true -ErrorAction Stop
        Start-Sleep -Seconds 2  # Give it time to apply
        Write-Success "Real-Time Protection DISABLED (temporary)"
        Write-Warning-Custom "Windows Defender will automatically re-enable itself in a few minutes for security"
        $defenderDisabled = $true
    }
    catch {
        Write-Warning-Custom "Could not disable Real-Time Protection: $_"
        Write-Warning-Custom "Continuing anyway... tests may fail if DLLs are blocked"
    }

    # Step 4: Add exclusions (belt and suspenders)
    Write-Step "Adding Windows Defender exclusions..."
    $dirsToExclude = @(
        $projectRoot,
        (Join-Path $projectRoot "tests"),
        (Join-Path $projectRoot "tests\OptionsExecutionService.Tests\bin"),
        (Join-Path $projectRoot "tests\TradingSupervisorService.Tests\bin"),
        (Join-Path $projectRoot "tests\SharedKernel.Tests\bin")
    )

    foreach ($dir in $dirsToExclude) {
        if (Test-Path $dir) {
            try {
                Add-MpPreference -ExclusionPath $dir -ErrorAction SilentlyContinue
                Write-Host "  ✓ Excluded: $dir" -ForegroundColor DarkGray
            }
            catch {
                # Ignore errors (might already be excluded)
            }
        }
    }
    Write-Success "Exclusions updated"

    # Step 5: Clean solution
    Write-Step "Cleaning solution..."
    Push-Location $projectRoot
    try {
        $cleanOutput = dotnet clean 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Success "Clean completed"
        }
        else {
            Write-Warning-Custom "Clean had warnings (continuing anyway)"
        }
    }
    finally {
        Pop-Location
    }

    # Step 6: Build solution
    Write-Step "Building solution..."
    Push-Location $projectRoot
    try {
        Write-Host "Running: dotnet build --no-restore" -ForegroundColor Gray
        $buildOutput = dotnet build --no-restore 2>&1

        if ($LASTEXITCODE -eq 0) {
            Write-Success "Build completed successfully"

            # Count warnings
            $warnings = ($buildOutput | Select-String "warning CS").Count
            if ($warnings -gt 0) {
                Write-Host "  ⚠️  $warnings warning(s)" -ForegroundColor Yellow
            }
        }
        else {
            Write-Failure "Build FAILED"
            Write-Host $buildOutput -ForegroundColor Red
            throw "Build failed with exit code $LASTEXITCODE"
        }
    }
    finally {
        Pop-Location
    }

    # Step 7: Unblock ALL DLLs in test directories
    Write-Step "Unblocking test DLLs..."
    $testDirs = @(
        (Join-Path $projectRoot "tests\OptionsExecutionService.Tests\bin"),
        (Join-Path $projectRoot "tests\TradingSupervisorService.Tests\bin"),
        (Join-Path $projectRoot "tests\SharedKernel.Tests\bin")
    )

    $totalUnblocked = 0
    foreach ($testDir in $testDirs) {
        if (Test-Path $testDir) {
            $dlls = Get-ChildItem -Path $testDir -Recurse -Filter "*.dll" -ErrorAction SilentlyContinue
            foreach ($dll in $dlls) {
                try {
                    Unblock-File -Path $dll.FullName -ErrorAction SilentlyContinue
                    $totalUnblocked++
                }
                catch {
                    # Ignore errors (file might not be blocked)
                }
            }
        }
    }
    Write-Success "Unblocked $totalUnblocked DLL files"

    # Step 8: Run full test suite
    Write-Step "Running FULL test suite..."
    Push-Location $projectRoot
    try {
        Write-Host "Running: dotnet test --no-build --verbosity normal" -ForegroundColor Gray
        Write-Host ""

        # Run tests and capture output
        $testOutput = dotnet test --no-build --verbosity normal 2>&1
        $testExitCode = $LASTEXITCODE

        # Display full output
        $testOutput | ForEach-Object { Write-Host $_ }

        Write-Host ""

        # Parse test results (multiple test projects - sum them all)
        $allPassed = $testOutput | Select-String "Superati:\s+(\d+)" -AllMatches
        $allFailed = $testOutput | Select-String "Non superati:\s+(\d+)" -AllMatches
        $allTotal = $testOutput | Select-String "Totale:\s+(\d+)" -AllMatches

        if ($allPassed -and $allTotal) {
            # Sum all test results across all projects
            $passed = ($allPassed.Matches | ForEach-Object { [int]$_.Groups[1].Value } | Measure-Object -Sum).Sum
            $failed = ($allFailed.Matches | ForEach-Object { [int]$_.Groups[1].Value } | Measure-Object -Sum).Sum
            $total = ($allTotal.Matches | ForEach-Object { [int]$_.Groups[1].Value } | Measure-Object -Sum).Sum

            $passRate = if ($total -gt 0) { [math]::Round(($passed / $total) * 100, 1) } else { 0 }

            Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
            Write-Host "TEST RESULTS:" -ForegroundColor Cyan
            Write-Host "  Total:  $total" -ForegroundColor White
            Write-Host "  Passed: $passed" -ForegroundColor Green
            Write-Host "  Failed: $failed" -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Red" })
            Write-Host "  Pass Rate: $passRate%" -ForegroundColor $(if ($passRate -ge 95) { "Green" } elseif ($passRate -ge 90) { "Yellow" } else { "Red" })
            Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan

            if ($testExitCode -eq 0) {
                Write-Success "ALL TESTS PASSED! 🎉"
            }
            else {
                Write-Warning-Custom "Some tests failed (see details above)"
            }
        }
        else {
            Write-Warning-Custom "Could not parse test results"
        }
    }
    finally {
        Pop-Location
    }

    # Step 9: Re-enable Windows Defender (ALWAYS)
    if ($defenderDisabled) {
        Write-Step "Re-enabling Windows Defender Real-Time Protection..."
        try {
            Set-MpPreference -DisableRealtimeMonitoring $false -ErrorAction Stop
            Write-Success "Real-Time Protection RE-ENABLED"
        }
        catch {
            Write-Warning-Custom "Could not re-enable Real-Time Protection: $_"
            Write-Warning-Custom "It will re-enable automatically soon"
        }
    }

    Write-Host ""
    Write-Success "Script completed successfully!"
    Write-Host ""

}
catch {
    Write-Failure "Script failed with error:"
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor DarkGray

    # CRITICAL: Always try to re-enable Windows Defender on error
    if ($defenderDisabled) {
        Write-Host ""
        Write-Step "Emergency: Re-enabling Windows Defender..."
        try {
            Set-MpPreference -DisableRealtimeMonitoring $false -ErrorAction SilentlyContinue
            Write-Success "Real-Time Protection RE-ENABLED"
        }
        catch {
            Write-Warning-Custom "Auto re-enable will happen in a few minutes"
        }
    }

    exit 1
}
finally {
    # Final safety net: ensure Defender is re-enabled
    try {
        Set-MpPreference -DisableRealtimeMonitoring $false -ErrorAction SilentlyContinue
    }
    catch {
        # Silent fail - Defender will re-enable itself
    }
}
