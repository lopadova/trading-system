# Install TradingSupervisorService as Windows Service
# Run as Administrator

param(
    [string]$ServiceName = "TradingSupervisorService",
    [string]$DisplayName = "Trading Supervisor Service",
    [string]$Description = "Monitors trading system health and IBKR connection status",
    [string]$BinPath = "$PSScriptRoot\..\..\src\TradingSupervisorService\bin\Release\net10.0\win-x64\publish\TradingSupervisorService.exe"
)

# Verify running as Administrator
if (-NOT ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Error "This script must be run as Administrator"
    exit 1
}

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

# Configure service to restart on failure
sc.exe failure $ServiceName reset= 86400 actions= restart/60000/restart/60000/restart/60000

# Start the service
Write-Host "Starting service '$ServiceName'..."
Start-Service -Name $ServiceName

# Verify service is running
$service = Get-Service -Name $ServiceName
if ($service.Status -eq "Running") {
    Write-Host "SUCCESS: Service '$ServiceName' is installed and running" -ForegroundColor Green
} else {
    Write-Error "Service installed but not running. Status: $($service.Status)"
    Write-Host "Check logs in .\logs\ directory for errors"
    exit 1
}
