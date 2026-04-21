# Install TradingSupervisorService as Windows Service
# Run as Administrator
#
# Phase 7.5: accepts -Environment (Production|Staging|Development) and sets
# DOTNET_ENVIRONMENT in the Windows Service's environment so
# appsettings.<Environment>.json overrides kick in at startup.

param(
    [string]$ServiceName = "TradingSupervisorService",
    [string]$DisplayName = "Trading Supervisor Service",
    [string]$Description = "Monitors trading system health and IBKR connection status",
    [string]$BinPath = "$PSScriptRoot\..\..\src\TradingSupervisorService\bin\Release\net10.0\win-x64\publish\TradingSupervisorService.exe",

    # Which appsettings.<env>.json layer to activate. Defaults to Production.
    # Staging mirrors prod shape but connects to the staging Worker + D1.
    [ValidateSet("Production", "Staging", "Development")]
    [string]$Environment = "Production"
)

# Verify running as Administrator
if (-NOT ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Error "This script must be run as Administrator"
    exit 1
}

# Environment banner — make it impossible to miss which env this install targets.
# A service silently installed under the wrong DOTNET_ENVIRONMENT is a classic
# Friday-afternoon footgun (appsettings.Production.json applied to the staging
# box and vice versa).
Write-Host ""
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  Installing '$ServiceName'" -ForegroundColor Cyan
Write-Host "  DOTNET_ENVIRONMENT = $Environment" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host ""

# Check if service already exists
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue

if ($existingService) {
    Write-Host "Service '$ServiceName' already exists. Stopping and removing..."
    Stop-Service -Name $ServiceName -Force
    sc.exe delete $ServiceName
    Start-Sleep -Seconds 2
}

# Verify binary exists
if (-NOT (Test-Path $BinPath)) {
    Write-Error "Service binary not found at: $BinPath"
    Write-Host "Please build and publish the service first:"
    Write-Host "  dotnet publish -c Release -r win-x64 --self-contained src\TradingSupervisorService\TradingSupervisorService.csproj"
    exit 1
}

# Create the service
Write-Host "Creating service '$ServiceName'..."
New-Service -Name $ServiceName `
    -BinaryPathName $BinPath `
    -DisplayName $DisplayName `
    -Description $Description `
    -StartupType Automatic

# Inject DOTNET_ENVIRONMENT into the service's environment so the host picks
# up appsettings.<env>.json at startup. The registry key
# HKLM\SYSTEM\CurrentControlSet\Services\<svc>\Environment is read by SCM
# when the service starts — same mechanism used by SetServiceEnvironmentVariables.
$svcRegPath = "HKLM:\SYSTEM\CurrentControlSet\Services\$ServiceName"
# Environment value must be a REG_MULTI_SZ (one string per VAR=VALUE entry).
$envEntries = @("DOTNET_ENVIRONMENT=$Environment", "ASPNETCORE_ENVIRONMENT=$Environment")
New-ItemProperty -Path $svcRegPath -Name 'Environment' -Value $envEntries -PropertyType MultiString -Force | Out-Null
Write-Host "Set DOTNET_ENVIRONMENT=$Environment on service '$ServiceName'"

# Configure service to restart on failure
sc.exe failure $ServiceName reset= 86400 actions= restart/60000/restart/60000/restart/60000

# Start the service
Write-Host "Starting service '$ServiceName'..."
Start-Service -Name $ServiceName

# Verify service is running
$service = Get-Service -Name $ServiceName
if ($service.Status -eq "Running") {
    Write-Host "SUCCESS: Service '$ServiceName' is installed and running (Environment=$Environment)" -ForegroundColor Green
} else {
    Write-Error "Service installed but not running. Status: $($service.Status)"
    Write-Host "Check logs in .\logs\ directory for errors"
    exit 1
}
