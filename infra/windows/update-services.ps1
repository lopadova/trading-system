# Update Trading System Windows Services
# Run as Administrator
# Stops services, updates binaries, and restarts services

param(
    [switch]$SupervisorOnly,
    [switch]$OptionsOnly,
    [switch]$SkipBackup
)

# Verify running as Administrator
if (-NOT ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Error "This script must be run as Administrator"
    exit 1
}

$ErrorActionPreference = "Stop"

Write-Host "`n=== Trading System Services Update ===" -ForegroundColor Cyan

# Determine which services to update
$updateSupervisor = (-not $OptionsOnly)
$updateOptions = (-not $SupervisorOnly)

# Backup current binaries
if (-not $SkipBackup) {
    Write-Host "`nCreating backup of current binaries..." -ForegroundColor Cyan
    $backupPath = "$PSScriptRoot\..\..\backup\$(Get-Date -Format 'yyyy-MM-dd_HHmmss')"
    New-Item -ItemType Directory -Path $backupPath -Force | Out-Null

    if ($updateSupervisor) {
        $supervisorBin = "$PSScriptRoot\..\..\src\TradingSupervisorService\bin\Release\net10.0\win-x64\publish"
        if (Test-Path $supervisorBin) {
            Copy-Item -Path $supervisorBin -Destination "$backupPath\TradingSupervisorService" -Recurse -Force
            Write-Host "  Backed up TradingSupervisorService to $backupPath" -ForegroundColor Green
        }
    }

    if ($updateOptions) {
        $optionsBin = "$PSScriptRoot\..\..\src\OptionsExecutionService\bin\Release\net10.0\win-x64\publish"
        if (Test-Path $optionsBin) {
            Copy-Item -Path $optionsBin -Destination "$backupPath\OptionsExecutionService" -Recurse -Force
            Write-Host "  Backed up OptionsExecutionService to $backupPath" -ForegroundColor Green
        }
    }
}

# Build new binaries
Write-Host "`nBuilding updated binaries..." -ForegroundColor Cyan

if ($updateSupervisor) {
    Write-Host "  Building TradingSupervisorService..."
    $buildResult = & dotnet publish "$PSScriptRoot\..\..\src\TradingSupervisorService\TradingSupervisorService.csproj" `
        -c Release `
        -r win-x64 `
        --self-contained true `
        /p:PublishSingleFile=true `
        2>&1

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to build TradingSupervisorService. Build output: $buildResult"
        exit 1
    }
    Write-Host "  TradingSupervisorService built successfully" -ForegroundColor Green
}

if ($updateOptions) {
    Write-Host "  Building OptionsExecutionService..."
    $buildResult = & dotnet publish "$PSScriptRoot\..\..\src\OptionsExecutionService\OptionsExecutionService.csproj" `
        -c Release `
        -r win-x64 `
        --self-contained true `
        /p:PublishSingleFile=true `
        2>&1

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to build OptionsExecutionService. Build output: $buildResult"
        exit 1
    }
    Write-Host "  OptionsExecutionService built successfully" -ForegroundColor Green
}

# Stop services
Write-Host "`nStopping services..." -ForegroundColor Cyan

if ($updateSupervisor) {
    $supervisorService = Get-Service -Name "TradingSupervisorService" -ErrorAction SilentlyContinue
    if ($supervisorService -and $supervisorService.Status -eq "Running") {
        Write-Host "  Stopping TradingSupervisorService..."
        Stop-Service -Name "TradingSupervisorService" -Force
        Start-Sleep -Seconds 3
        Write-Host "  TradingSupervisorService stopped" -ForegroundColor Green
    } else {
        Write-Host "  TradingSupervisorService not running" -ForegroundColor Gray
    }
}

if ($updateOptions) {
    $optionsService = Get-Service -Name "OptionsExecutionService" -ErrorAction SilentlyContinue
    if ($optionsService -and $optionsService.Status -eq "Running") {
        Write-Host "  Stopping OptionsExecutionService..."
        Stop-Service -Name "OptionsExecutionService" -Force
        Start-Sleep -Seconds 3
        Write-Host "  OptionsExecutionService stopped" -ForegroundColor Green
    } else {
        Write-Host "  OptionsExecutionService not running" -ForegroundColor Gray
    }
}

# Wait for file handles to be released
Write-Host "`nWaiting for file handles to be released..."
Start-Sleep -Seconds 5

# Services are now stopped, binaries have been built
Write-Host "`nUpdate complete. Services have been stopped and new binaries are ready." -ForegroundColor Green
Write-Host ""

# Restart services
Write-Host "Restarting services..." -ForegroundColor Cyan

if ($updateSupervisor) {
    $supervisorService = Get-Service -Name "TradingSupervisorService" -ErrorAction SilentlyContinue
    if ($supervisorService) {
        Write-Host "  Starting TradingSupervisorService..."
        Start-Service -Name "TradingSupervisorService"
        Start-Sleep -Seconds 2
        $status = (Get-Service -Name "TradingSupervisorService").Status
        if ($status -eq "Running") {
            Write-Host "  TradingSupervisorService started successfully" -ForegroundColor Green
        } else {
            Write-Warning "  TradingSupervisorService status: $status (check logs)"
        }
    }
}

if ($updateOptions) {
    # Only restart OptionsExecutionService if it was running before
    $optionsService = Get-Service -Name "OptionsExecutionService" -ErrorAction SilentlyContinue
    if ($optionsService) {
        Write-Host "  OptionsExecutionService remains stopped (Manual startup)"
        Write-Host "  To start: Start-Service -Name OptionsExecutionService" -ForegroundColor Yellow
    }
}

Write-Host "`n=== Update Complete ===" -ForegroundColor Green
Write-Host ""
Write-Host "Run verify-installation.ps1 to confirm the update was successful." -ForegroundColor Cyan
