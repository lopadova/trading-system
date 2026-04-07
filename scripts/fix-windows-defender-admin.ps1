#!/usr/bin/env pwsh
<#
.SYNOPSIS
    MUST BE RUN AS ADMINISTRATOR - Fixes Windows Defender blocking test DLLs

.DESCRIPTION
    This script:
    1. Verifies Administrator privileges
    2. Adds ALL project directories to Windows Defender exclusions
    3. Unblocks ALL DLL files in the project
    4. Clean rebuilds the solution
    5. Runs all tests

    This is the NUCLEAR option when normal exclusions don't work.

.EXAMPLE
    # Right-click PowerShell -> Run as Administrator, then:
    cd C:\Users\lopad\Documents\DocLore\Visual Basic\_NET\Applicazioni\trading-system
    .\scripts\fix-windows-defender-admin.ps1
#>

param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

# ============================================
# STEP 1: Verify Administrator privileges
# ============================================
Write-Host "`n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "🔒 STEP 1: Checking Administrator privileges" -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━`n" -ForegroundColor Cyan

$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Host "❌ ERROR: This script MUST be run as Administrator!" -ForegroundColor Red
    Write-Host "`nTo run as Administrator:" -ForegroundColor Yellow
    Write-Host "  1. Right-click PowerShell" -ForegroundColor White
    Write-Host "  2. Select 'Run as Administrator'" -ForegroundColor White
    Write-Host "  3. cd to project directory" -ForegroundColor White
    Write-Host "  4. Run: .\scripts\fix-windows-defender-admin.ps1`n" -ForegroundColor White
    exit 1
}

Write-Host "✅ Running as Administrator`n" -ForegroundColor Green

# ============================================
# STEP 2: Add Windows Defender Exclusions
# ============================================
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "🛡️  STEP 2: Adding Windows Defender Exclusions" -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━`n" -ForegroundColor Cyan

# Get project root (one level up from scripts/)
$scriptDir = Split-Path -Parent $PSCommandPath
$projectRoot = Split-Path -Parent $scriptDir

Write-Host "Project root: $projectRoot" -ForegroundColor Gray

# Verify TradingSystem.sln exists
$slnFile = Join-Path $projectRoot "TradingSystem.sln"
if (-not (Test-Path $slnFile)) {
    Write-Host "❌ ERROR: TradingSystem.sln not found at: $slnFile" -ForegroundColor Red
    Write-Host "   Current directory: $(Get-Location)" -ForegroundColor Yellow
    Write-Host "   Script directory: $scriptDir" -ForegroundColor Yellow
    exit 1
}

# Directories to exclude (EVERYTHING in the project)
$excludePaths = @(
    $projectRoot,                                    # Root directory
    (Join-Path $projectRoot "src"),                 # All source code
    (Join-Path $projectRoot "tests"),               # All tests
    (Join-Path $projectRoot "bin"),                 # All binaries
    (Join-Path $projectRoot "obj"),                 # All intermediate files
    (Join-Path $projectRoot ".vs"),                 # Visual Studio cache
    (Join-Path $projectRoot "packages")             # NuGet packages
)

$successCount = 0
$failCount = 0

foreach ($path in $excludePaths) {
    if (Test-Path $path) {
        try {
            Write-Host "Adding exclusion: $path" -ForegroundColor Yellow
            Add-MpPreference -ExclusionPath $path -ErrorAction Stop
            Write-Host "  ✅ Success" -ForegroundColor Green
            $successCount++
        }
        catch {
            Write-Host "  ⚠️  Failed: $($_.Exception.Message)" -ForegroundColor Red
            $failCount++
        }
    }
    else {
        Write-Host "Skipping (not found): $path" -ForegroundColor Gray
    }
}

Write-Host "`n📊 Exclusions Summary:" -ForegroundColor Cyan
Write-Host "  ✅ Added: $successCount" -ForegroundColor Green
Write-Host "  ❌ Failed: $failCount" -ForegroundColor $(if ($failCount -eq 0) { "Green" } else { "Red" })

# ============================================
# STEP 3: Unblock ALL files
# ============================================
Write-Host "`n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "🔓 STEP 3: Unblocking ALL DLL files" -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━`n" -ForegroundColor Cyan

# Get ALL DLLs in bin/Debug and bin/Release
$dlls = Get-ChildItem -Path $projectRoot -Filter "*.dll" -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match "\\bin\\(Debug|Release)\\" }

Write-Host "Found $($dlls.Count) DLL files to unblock...`n" -ForegroundColor Yellow

$unblocked = 0
$alreadyUnblocked = 0
$failed = 0

foreach ($dll in $dlls) {
    try {
        # Check if file is blocked
        $zone = Get-Item -Path $dll.FullName -Stream Zone.Identifier -ErrorAction SilentlyContinue

        if ($zone) {
            # File is blocked, unblock it
            Unblock-File -Path $dll.FullName -Confirm:$false -ErrorAction Stop
            Write-Host "  ✅ Unblocked: $($dll.Name)" -ForegroundColor Green
            $unblocked++
        }
        else {
            # File is not blocked
            $alreadyUnblocked++
        }
    }
    catch {
        Write-Host "  ❌ Failed to unblock: $($dll.Name) - $($_.Exception.Message)" -ForegroundColor Red
        $failed++
    }
}

Write-Host "`n📊 Unblock Summary:" -ForegroundColor Cyan
Write-Host "  ✅ Unblocked: $unblocked" -ForegroundColor Green
Write-Host "  ℹ️  Already unblocked: $alreadyUnblocked" -ForegroundColor Gray
Write-Host "  ❌ Failed: $failed" -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Red" })

# ============================================
# STEP 4: Clean and Rebuild
# ============================================
Write-Host "`n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "🔨 STEP 4: Clean and Rebuild Solution" -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━`n" -ForegroundColor Cyan

Push-Location $projectRoot

try {
    Write-Host "Cleaning solution..." -ForegroundColor Yellow
    dotnet clean TradingSystem.sln --verbosity quiet
    Write-Host "✅ Clean complete`n" -ForegroundColor Green

    Write-Host "Building solution..." -ForegroundColor Yellow
    $buildOutput = dotnet build TradingSystem.sln --verbosity minimal 2>&1

    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Build successful`n" -ForegroundColor Green
    }
    else {
        Write-Host "❌ Build failed!`n" -ForegroundColor Red
        Write-Host $buildOutput -ForegroundColor Red
        throw "Build failed with exit code $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}

# ============================================
# STEP 5: Unblock again (files recreated by build)
# ============================================
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "🔓 STEP 5: Unblocking newly built files" -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━`n" -ForegroundColor Cyan

# Get ALL DLLs again (fresh from build)
$newDlls = Get-ChildItem -Path $projectRoot -Filter "*.dll" -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match "\\bin\\(Debug|Release)\\" }

$unblockedNew = 0
foreach ($dll in $newDlls) {
    try {
        Unblock-File -Path $dll.FullName -Confirm:$false -ErrorAction SilentlyContinue
        $unblockedNew++
    }
    catch {
        # Silently continue
    }
}

Write-Host "✅ Unblocked $unblockedNew newly built files`n" -ForegroundColor Green

# ============================================
# STEP 6: Run Tests
# ============================================
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "🧪 STEP 6: Running Tests" -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━`n" -ForegroundColor Cyan

Push-Location $projectRoot

try {
    Write-Host "Running all tests...`n" -ForegroundColor Yellow

    $testOutput = dotnet test TradingSystem.sln --no-build --logger "console;verbosity=normal" 2>&1

    # Extract summary
    $summary = $testOutput | Select-String -Pattern "(Totale test:|Superati:|Non superati:|L'esecuzione)"

    Write-Host "`n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
    Write-Host "📊 Test Summary" -ForegroundColor Cyan
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━`n" -ForegroundColor Cyan

    foreach ($line in $summary) {
        if ($line -match "Non superati:\s+0") {
            Write-Host $line -ForegroundColor Green
        }
        elseif ($line -match "Non superati:") {
            Write-Host $line -ForegroundColor Yellow
        }
        else {
            Write-Host $line -ForegroundColor White
        }
    }

    # Check if OptionsExecutionService tests ran
    $optionsTestsRan = $testOutput | Select-String -Pattern "OptionsExecutionService.Tests"

    if ($optionsTestsRan) {
        Write-Host "`n✅ OptionsExecutionService.Tests is NOW running!" -ForegroundColor Green
    }
    else {
        Write-Host "`n⚠️  OptionsExecutionService.Tests still blocked" -ForegroundColor Yellow
        Write-Host "   You may need to:" -ForegroundColor Yellow
        Write-Host "   1. Open Windows Security" -ForegroundColor White
        Write-Host "   2. Virus & threat protection → Manage settings" -ForegroundColor White
        Write-Host "   3. Add folder exclusion manually" -ForegroundColor White
        Write-Host "   4. OR disable 'Exploit protection' temporarily`n" -ForegroundColor White
    }
}
finally {
    Pop-Location
}

# ============================================
# FINAL STATUS
# ============================================
Write-Host "`n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "✅ Script Complete!" -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━`n" -ForegroundColor Cyan

Write-Host "What was done:" -ForegroundColor Yellow
Write-Host "  ✅ Added $successCount Windows Defender exclusions" -ForegroundColor Green
Write-Host "  ✅ Unblocked $unblocked existing DLL files" -ForegroundColor Green
Write-Host "  ✅ Clean + Rebuild successful" -ForegroundColor Green
Write-Host "  ✅ Unblocked $unblockedNew newly built files" -ForegroundColor Green
Write-Host "  ✅ Test suite executed`n" -ForegroundColor Green

Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Check test results above" -ForegroundColor White
Write-Host "  2. If OptionsExecutionService.Tests still blocked:" -ForegroundColor White
Write-Host "     - See ADD_EXCLUSIONS_MANUAL.md for GUI steps" -ForegroundColor White
Write-Host "  3. Run: dotnet test --no-build`n" -ForegroundColor White
