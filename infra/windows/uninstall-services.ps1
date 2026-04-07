# Uninstall Trading System Windows Services
# Run as Administrator
# Stops and removes both services and optionally cleans up data

param(
    [switch]$KeepData,
    [switch]$KeepLogs,
    [switch]$Force
)

# Verify running as Administrator
if (-NOT ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Error "This script must be run as Administrator"
    exit 1
}

$ErrorActionPreference = "Stop"

Write-Host "`n=== Trading System Services Uninstallation ===" -ForegroundColor Cyan

# Confirm unless Force flag is set
if (-not $Force) {
    Write-Host "`nThis will remove the following services:" -ForegroundColor Yellow
    Write-Host "  - TradingSupervisorService" -ForegroundColor Yellow
    Write-Host "  - OptionsExecutionService" -ForegroundColor Yellow

    if (-not $KeepData) {
        Write-Host "`nWARNING: This will also delete ALL databases (supervisor.db, options.db)" -ForegroundColor Red
    }

    if (-not $KeepLogs) {
        Write-Host "WARNING: This will also delete ALL log files" -ForegroundColor Red
    }

    $confirm = Read-Host "`nAre you sure you want to proceed? (type 'YES' to confirm)"
    if ($confirm -ne "YES") {
        Write-Host "Uninstallation cancelled."
        exit 0
    }
}

# Stop and remove TradingSupervisorService
Write-Host "`nRemoving TradingSupervisorService..." -ForegroundColor Cyan
$supervisorService = Get-Service -Name "TradingSupervisorService" -ErrorAction SilentlyContinue
if ($supervisorService) {
    if ($supervisorService.Status -eq "Running") {
        Write-Host "  Stopping service..."
        Stop-Service -Name "TradingSupervisorService" -Force
        Start-Sleep -Seconds 2
    }
    Write-Host "  Deleting service..."
    sc.exe delete TradingSupervisorService | Out-Null
    Write-Host "  TradingSupervisorService removed successfully" -ForegroundColor Green
} else {
    Write-Host "  TradingSupervisorService not found (already removed)" -ForegroundColor Gray
}

# Stop and remove OptionsExecutionService
Write-Host "`nRemoving OptionsExecutionService..." -ForegroundColor Cyan
$optionsService = Get-Service -Name "OptionsExecutionService" -ErrorAction SilentlyContinue
if ($optionsService) {
    if ($optionsService.Status -eq "Running") {
        Write-Host "  Stopping service..."
        Stop-Service -Name "OptionsExecutionService" -Force
        Start-Sleep -Seconds 2
    }
    Write-Host "  Deleting service..."
    sc.exe delete OptionsExecutionService | Out-Null
    Write-Host "  OptionsExecutionService removed successfully" -ForegroundColor Green
} else {
    Write-Host "  OptionsExecutionService not found (already removed)" -ForegroundColor Gray
}

# Clean up data if requested
if (-not $KeepData) {
    Write-Host "`nCleaning up databases..." -ForegroundColor Cyan
    $dataPath = "$PSScriptRoot\..\..\data"
    if (Test-Path $dataPath) {
        $dbFiles = Get-ChildItem "$dataPath\*.db" -ErrorAction SilentlyContinue
        foreach ($dbFile in $dbFiles) {
            Write-Host "  Deleting $($dbFile.Name)..."
            Remove-Item $dbFile.FullName -Force
        }
        Write-Host "  Database files deleted" -ForegroundColor Green
    } else {
        Write-Host "  Data directory not found" -ForegroundColor Gray
    }
}

# Clean up logs if requested
if (-not $KeepLogs) {
    Write-Host "`nCleaning up logs..." -ForegroundColor Cyan
    $logsPath = "$PSScriptRoot\..\..\logs"
    if (Test-Path $logsPath) {
        $logFiles = Get-ChildItem "$logsPath\*.log" -ErrorAction SilentlyContinue
        foreach ($logFile in $logFiles) {
            Write-Host "  Deleting $($logFile.Name)..."
            Remove-Item $logFile.FullName -Force
        }
        Write-Host "  Log files deleted" -ForegroundColor Green
    } else {
        Write-Host "  Logs directory not found" -ForegroundColor Gray
    }
}

Write-Host "`n=== Uninstallation Complete ===" -ForegroundColor Green
Write-Host ""

if ($KeepData) {
    Write-Host "Note: Database files were preserved in .\data\" -ForegroundColor Yellow
}

if ($KeepLogs) {
    Write-Host "Note: Log files were preserved in .\logs\" -ForegroundColor Yellow
}
