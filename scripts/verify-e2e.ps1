# verify-e2e.ps1 - E2E Readiness Verification Script (PowerShell)
# Checks system readiness for E2E testing (without requiring IBKR connection)

$ErrorActionPreference = "Stop"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "E2E Readiness Verification" -ForegroundColor Cyan
Write-Host "Trading System - $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

$ReportFile = ".\logs\e2e-verification-report-$(Get-Date -Format 'yyyyMMdd-HHmmss').md"
New-Item -ItemType Directory -Force -Path ".\logs" | Out-Null

# Initialize counters
$Passed = 0
$Failed = 0
$Warnings = 0

# Helper functions
function Pass {
    param([string]$Message)
    Write-Host "✅ PASS: $Message" -ForegroundColor Green
    Add-Content -Path $ReportFile -Value "- ✅ PASS: $Message"
    $script:Passed++
}

function Fail {
    param([string]$Message)
    Write-Host "❌ FAIL: $Message" -ForegroundColor Red
    Add-Content -Path $ReportFile -Value "- ❌ FAIL: $Message"
    $script:Failed++
}

function Warn {
    param([string]$Message)
    Write-Host "⚠️  WARN: $Message" -ForegroundColor Yellow
    Add-Content -Path $ReportFile -Value "- ⚠️  WARN: $Message"
    $script:Warnings++
}

# Start report
@"
# E2E Verification Report

**Date**: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
**Script**: verify-e2e.ps1

---

## Test Results

"@ | Set-Content -Path $ReportFile

Add-Content -Path $ReportFile -Value "`n## 1. Prerequisites Check`n"
Write-Host "## 1. Prerequisites Check`n"

# Check .NET SDK
try {
    $dotnetVersion = & dotnet --version 2>&1
    if ($dotnetVersion -like "10.*") {
        Pass ".NET SDK 10.0 installed (version: $dotnetVersion)"
    } else {
        Fail ".NET SDK 10.0 required (found: $dotnetVersion)"
    }
} catch {
    Fail ".NET SDK not found in PATH"
}

# Check Git
if (Get-Command git -ErrorAction SilentlyContinue) {
    Pass "Git installed"
} else {
    Warn "Git not found (optional for E2E tests)"
}

# Check solution file
if (Test-Path ".\TradingSystem.sln") {
    Pass "Solution file exists"
} else {
    Fail "TradingSystem.sln not found"
}

Add-Content -Path $ReportFile -Value "`n## 2. Build Verification`n"
Write-Host "`n## 2. Build Verification`n"

# Build solution
Write-Host "Building solution..."
try {
    $buildOutput = & dotnet build -c Debug --no-restore 2>&1
    if ($LASTEXITCODE -eq 0) {
        Pass "Solution builds successfully (Debug)"
    } else {
        Fail "Build failed"
        $buildOutput | Out-File -FilePath ".\logs\build-error.log"
    }
} catch {
    Fail "Build command failed: $_"
}

# Run automated tests
Write-Host "Running automated E2E tests..."
if (Test-Path ".\tests\E2E\Automated\E2E.Automated.csproj") {
    try {
        $testOutput = & dotnet test .\tests\E2E\Automated\E2E.Automated.csproj --no-build 2>&1
        if ($LASTEXITCODE -eq 0) {
            Pass "Automated E2E tests passed"
        } else {
            Fail "Automated E2E tests failed"
            $testOutput | Out-File -FilePath ".\logs\test-error.log"
        }
    } catch {
        Fail "Test command failed: $_"
    }
} else {
    Warn "Automated E2E tests not found (skipping)"
}

Add-Content -Path $ReportFile -Value "`n## 3. Database Schema Verification`n"
Write-Host "`n## 3. Database Schema Verification`n"

# Check for migrations
if (Test-Path ".\src\TradingSupervisorService\Data\Migrations") {
    $supervisorMigrations = @(Get-ChildItem -Path ".\src\TradingSupervisorService\Data\Migrations" -Filter "*.cs" -ErrorAction SilentlyContinue).Count
    if ($supervisorMigrations -gt 0) {
        Pass "Supervisor migrations exist ($supervisorMigrations files)"
    } else {
        Warn "No supervisor migration files found"
    }
} else {
    Warn "Supervisor migrations directory not found"
}

if (Test-Path ".\src\OptionsExecutionService\Data\Migrations") {
    $optionsMigrations = @(Get-ChildItem -Path ".\src\OptionsExecutionService\Data\Migrations" -Filter "*.cs" -ErrorAction SilentlyContinue).Count
    if ($optionsMigrations -gt 0) {
        Pass "Options migrations exist ($optionsMigrations files)"
    } else {
        Warn "No options migration files found"
    }
} else {
    Warn "Options migrations directory not found"
}

Add-Content -Path $ReportFile -Value "`n## 4. Configuration Files`n"
Write-Host "`n## 4. Configuration Files`n"

# Check for example config files
if ((Test-Path ".\config\supervisor.example.json") -or (Test-Path ".\src\TradingSupervisorService\appsettings.json")) {
    Pass "Supervisor config template exists"
} else {
    Warn "Supervisor config template not found"
}

if ((Test-Path ".\config\options.example.json") -or (Test-Path ".\src\OptionsExecutionService\appsettings.json")) {
    Pass "Options config template exists"
} else {
    Warn "Options config template not found"
}

Add-Content -Path $ReportFile -Value "`n## 5. E2E Test Files`n"
Write-Host "`n## 5. E2E Test Files`n"

# Check for E2E test markdown files
$e2eCount = @(Get-ChildItem -Path ".\tests\E2E" -Filter "E2E-*.md" -ErrorAction SilentlyContinue).Count

if ($e2eCount -eq 10) {
    Pass "All 10 E2E test checklists present"
} elseif ($e2eCount -gt 0) {
    Warn "Only $e2eCount/10 E2E test checklists found"
} else {
    Fail "No E2E test checklists found in tests\E2E\"
}

# Check README
if (Test-Path ".\tests\E2E\README.md") {
    Pass "E2E README exists"
} else {
    Warn "E2E README not found"
}

Add-Content -Path $ReportFile -Value "`n## 6. Strategy Files`n"
Write-Host "`n## 6. Strategy Files`n"

# Check strategies directory
if (Test-Path ".\strategies") {
    Pass "Strategies directory exists"

    # Check for .gitkeep in private
    if (Test-Path ".\strategies\private\.gitkeep") {
        Pass "strategies\private\.gitkeep exists"
    } else {
        Warn "strategies\private\.gitkeep missing"
    }

    # Verify strategies/private is gitignored
    try {
        $gitCheck = & git check-ignore "strategies\private\test.json" 2>&1
        if ($LASTEXITCODE -eq 0) {
            Pass "strategies\private\ is gitignored"
        } else {
            Fail "strategies\private\ is NOT gitignored (security risk!)"
        }
    } catch {
        Warn "Could not verify gitignore (git not available)"
    }
} else {
    Fail "Strategies directory not found"
}

Add-Content -Path $ReportFile -Value "`n## 7. Scripts`n"
Write-Host "`n## 7. Scripts`n"

# Check for essential scripts
$scripts = @(
    "check-knowledge.ps1",
    "verify-e2e.ps1",
    "pre-deployment-checklist.sh"
)

foreach ($script in $scripts) {
    if (Test-Path ".\scripts\$script") {
        Pass "Script exists: $script"
    } else {
        Warn "Script not found: $script"
    }
}

Add-Content -Path $ReportFile -Value "`n## 8. Documentation`n"
Write-Host "`n## 8. Documentation`n"

# Check for key documentation files
$docs = @(
    "docs\GETTING_STARTED.md",
    "docs\ARCHITECTURE.md",
    "docs\CONFIGURATION.md",
    "docs\TROUBLESHOOTING.md"
)

foreach ($doc in $docs) {
    if (Test-Path ".\$doc") {
        Pass "Documentation exists: $doc"
    } else {
        Warn "Documentation missing: $doc"
    }
}

Add-Content -Path $ReportFile -Value "`n## 9. Knowledge Base`n"
Write-Host "`n## 9. Knowledge Base`n"

# Check knowledge directory
if (Test-Path ".\knowledge") {
    Pass "Knowledge directory exists"

    $kbFiles = @(
        "errors-registry.md",
        "lessons-learned.md",
        "skill-changelog.md"
    )

    foreach ($kb in $kbFiles) {
        if (Test-Path ".\knowledge\$kb") {
            Pass "Knowledge file exists: $kb"
        } else {
            Warn "Knowledge file missing: $kb"
        }
    }
} else {
    Warn "Knowledge directory not found"
}

Add-Content -Path $ReportFile -Value "`n## 10. Service Readiness`n"
Write-Host "`n## 10. Service Readiness (Dry Run)`n"

# Check if services build
Write-Host "Testing TradingSupervisorService build..."
if ((Test-Path ".\src\TradingSupervisorService\bin\Debug\net10.0\TradingSupervisorService.dll") -or
    ((& dotnet build .\src\TradingSupervisorService\TradingSupervisorService.csproj --no-restore 2>&1) -and ($LASTEXITCODE -eq 0))) {
    Pass "TradingSupervisorService builds successfully"
} else {
    Fail "TradingSupervisorService build failed"
}

Write-Host "Testing OptionsExecutionService build..."
if ((Test-Path ".\src\OptionsExecutionService\bin\Debug\net10.0\OptionsExecutionService.dll") -or
    ((& dotnet build .\src\OptionsExecutionService\OptionsExecutionService.csproj --no-restore 2>&1) -and ($LASTEXITCODE -eq 0))) {
    Pass "OptionsExecutionService builds successfully"
} else {
    Fail "OptionsExecutionService build failed"
}

# Summary
Write-Host "`n==========================================" -ForegroundColor Cyan
Write-Host "SUMMARY" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

@"

---

## Summary

- ✅ Passed: $Passed
- ❌ Failed: $Failed
- ⚠️  Warnings: $Warnings

"@ | Add-Content -Path $ReportFile

Write-Host "✅ Passed:   $Passed" -ForegroundColor Green
Write-Host "❌ Failed:   $Failed" -ForegroundColor Red
Write-Host "⚠️  Warnings: $Warnings" -ForegroundColor Yellow
Write-Host ""

# Overall assessment
if ($Failed -eq 0) {
    if ($Warnings -eq 0) {
        Write-Host "✅ RESULT: READY FOR E2E TESTING" -ForegroundColor Green
        Add-Content -Path $ReportFile -Value "**RESULT**: ✅ **READY FOR E2E TESTING**"
        exit 0
    } else {
        Write-Host "⚠️  RESULT: READY WITH WARNINGS (review before testing)" -ForegroundColor Yellow
        Add-Content -Path $ReportFile -Value "**RESULT**: ⚠️  **READY WITH WARNINGS** (review before testing)"
        exit 0
    }
} else {
    Write-Host "❌ RESULT: NOT READY (fix failures before E2E testing)" -ForegroundColor Red
    Add-Content -Path $ReportFile -Value "**RESULT**: ❌ **NOT READY** (fix failures before E2E testing)"
    exit 1
}

Write-Host ""
Write-Host "Report saved to: $ReportFile" -ForegroundColor Cyan
