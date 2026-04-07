# Verify Trading System Windows Services Installation
# Run after installation to ensure everything is properly configured

param(
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"
$script:FailureCount = 0

function Write-TestResult {
    param(
        [string]$Name,
        [bool]$Success,
        [string]$Message = ""
    )

    if ($Success) {
        Write-Host "[PASS] $Name" -ForegroundColor Green
    } else {
        Write-Host "[FAIL] $Name" -ForegroundColor Red
        if ($Message) {
            Write-Host "       $Message" -ForegroundColor Yellow
        }
        $script:FailureCount++
    }
}

Write-Host "`n=== Trading System Installation Verification ===" -ForegroundColor Cyan
Write-Host ""

# 1. Check .NET 10 SDK
Write-Host "Checking .NET 10 SDK..." -ForegroundColor Cyan
try {
    $dotnetVersion = & dotnet --version 2>&1
    $hasDotnet10 = $dotnetVersion -match "^10\."
    Write-TestResult ".NET 10 SDK Installed" $hasDotnet10 "Found: $dotnetVersion"
} catch {
    Write-TestResult ".NET 10 SDK Installed" $false "dotnet command not found"
}

# 2. Check SQLite database files
Write-Host "`nChecking SQLite databases..." -ForegroundColor Cyan
$dbPath = "$PSScriptRoot\..\..\data"
$supervisorDbExists = Test-Path "$dbPath\supervisor.db"
$optionsDbExists = Test-Path "$dbPath\options.db"

Write-TestResult "Supervisor Database Exists" $supervisorDbExists "$dbPath\supervisor.db"
Write-TestResult "Options Database Exists" $optionsDbExists "$dbPath\options.db"

# 3. Check Windows Services
Write-Host "`nChecking Windows Services..." -ForegroundColor Cyan
$supervisorService = Get-Service -Name "TradingSupervisorService" -ErrorAction SilentlyContinue
$optionsService = Get-Service -Name "OptionsExecutionService" -ErrorAction SilentlyContinue

Write-TestResult "TradingSupervisorService Installed" ($null -ne $supervisorService)
Write-TestResult "OptionsExecutionService Installed" ($null -ne $optionsService)

if ($supervisorService) {
    $supervisorRunning = $supervisorService.Status -eq "Running"
    Write-TestResult "TradingSupervisorService Running" $supervisorRunning "Status: $($supervisorService.Status)"
}

if ($optionsService) {
    $optionsRunning = $optionsService.Status -eq "Running"
    Write-TestResult "OptionsExecutionService Running" $optionsRunning "Status: $($optionsService.Status)"

    if ($optionsService.StartType -eq "Manual") {
        Write-Host "       Note: OptionsExecutionService is set to Manual startup (expected)" -ForegroundColor Gray
    }
}

# 4. Check Configuration Files
Write-Host "`nChecking Configuration..." -ForegroundColor Cyan
$supervisorConfig = "$PSScriptRoot\..\..\src\TradingSupervisorService\bin\Release\net10.0\win-x64\publish\appsettings.json"
$optionsConfig = "$PSScriptRoot\..\..\src\OptionsExecutionService\bin\Release\net10.0\win-x64\publish\appsettings.json"

if (Test-Path $supervisorConfig) {
    Write-TestResult "TradingSupervisorService Config Exists" $true
    try {
        $supervisorSettings = Get-Content $supervisorConfig | ConvertFrom-Json
        # Validate key settings
        $hasDbPath = $null -ne $supervisorSettings.DatabasePath
        Write-TestResult "  DatabasePath Configured" $hasDbPath
    } catch {
        Write-TestResult "  Configuration Valid JSON" $false $_.Exception.Message
    }
} else {
    Write-TestResult "TradingSupervisorService Config Exists" $false
}

if (Test-Path $optionsConfig) {
    Write-TestResult "OptionsExecutionService Config Exists" $true
    try {
        $optionsSettings = Get-Content $optionsConfig | ConvertFrom-Json
        # Validate CRITICAL: TradingMode must be "paper" by default
        $isPaperMode = $optionsSettings.TradingMode -eq "paper"
        Write-TestResult "  TradingMode = paper (SAFETY)" $isPaperMode "Current: $($optionsSettings.TradingMode)"

        $hasIbkrPort = $null -ne $optionsSettings.IBKR.Port
        Write-TestResult "  IBKR Port Configured" $hasIbkrPort
    } catch {
        Write-TestResult "  Configuration Valid JSON" $false $_.Exception.Message
    }
} else {
    Write-TestResult "OptionsExecutionService Config Exists" $false
}

# 5. Check Log Files
Write-Host "`nChecking Logs..." -ForegroundColor Cyan
$logsPath = "$PSScriptRoot\..\..\logs"
$logsExist = Test-Path $logsPath
Write-TestResult "Logs Directory Exists" $logsExist $logsPath

if ($logsExist) {
    $supervisorLogs = Get-ChildItem "$logsPath\supervisor-*.log" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    $optionsLogs = Get-ChildItem "$logsPath\options-*.log" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1

    if ($supervisorLogs) {
        Write-TestResult "TradingSupervisorService Logs Present" $true "Latest: $($supervisorLogs.Name)"
        if ($Verbose) {
            Write-Host "`n--- Last 10 lines of Supervisor log ---" -ForegroundColor Gray
            Get-Content $supervisorLogs.FullName -Tail 10 | ForEach-Object { Write-Host "    $_" -ForegroundColor Gray }
        }
    }

    if ($optionsLogs) {
        Write-TestResult "OptionsExecutionService Logs Present" $true "Latest: $($optionsLogs.Name)"
        if ($Verbose) {
            Write-Host "`n--- Last 10 lines of Options log ---" -ForegroundColor Gray
            Get-Content $optionsLogs.FullName -Tail 10 | ForEach-Object { Write-Host "    $_" -ForegroundColor Gray }
        }
    }
}

# 6. Check Database Schema (if SQLite CLI available)
Write-Host "`nChecking Database Schema..." -ForegroundColor Cyan
$sqliteCmd = Get-Command sqlite3 -ErrorAction SilentlyContinue
if ($sqliteCmd -and $supervisorDbExists) {
    try {
        $tables = & sqlite3 "$dbPath\supervisor.db" "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;" 2>&1
        if ($LASTEXITCODE -eq 0) {
            $tableCount = ($tables | Measure-Object).Count
            Write-TestResult "Supervisor DB Schema Initialized" ($tableCount -gt 0) "Tables: $tableCount"
        }
    } catch {
        Write-Host "       Could not query database schema" -ForegroundColor Gray
    }
}

if ($sqliteCmd -and $optionsDbExists) {
    try {
        $tables = & sqlite3 "$dbPath\options.db" "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;" 2>&1
        if ($LASTEXITCODE -eq 0) {
            $tableCount = ($tables | Measure-Object).Count
            Write-TestResult "Options DB Schema Initialized" ($tableCount -gt 0) "Tables: $tableCount"
        }
    } catch {
        Write-Host "       Could not query database schema" -ForegroundColor Gray
    }
}

# Summary
Write-Host "`n=== Verification Summary ===" -ForegroundColor Cyan
if ($script:FailureCount -eq 0) {
    Write-Host "All checks passed! ✓" -ForegroundColor Green
    exit 0
} else {
    Write-Host "$($script:FailureCount) check(s) failed! ✗" -ForegroundColor Red
    Write-Host "Review the failures above and fix them before proceeding." -ForegroundColor Yellow
    exit 1
}
