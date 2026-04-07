# Install OptionsExecutionService as Windows Service
# Run as Administrator

param(
    [string]$ServiceName = "OptionsExecutionService",
    [string]$DisplayName = "Options Execution Service",
    [string]$Description = "Executes options trading strategies via Interactive Brokers",
    [string]$BinPath = "$PSScriptRoot\..\..\src\OptionsExecutionService\bin\Release\net10.0\win-x64\publish\OptionsExecutionService.exe"
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
    Write-Host "  dotnet publish -c Release -r win-x64 --self-contained src\OptionsExecutionService\OptionsExecutionService.csproj"
    exit 1
}

# SAFETY CHECK: Ensure appsettings.json has TradingMode = "paper"
$appSettingsPath = "$PSScriptRoot\..\..\src\OptionsExecutionService\bin\Release\net10.0\win-x64\publish\appsettings.json"
if (Test-Path $appSettingsPath) {
    $config = Get-Content $appSettingsPath | ConvertFrom-Json
    if ($config.TradingMode -ne "paper") {
        Write-Warning "WARNING: TradingMode is set to '$($config.TradingMode)' in appsettings.json"
        Write-Warning "This service will execute REAL TRADES with REAL MONEY!"
        $confirm = Read-Host "Are you ABSOLUTELY SURE you want to proceed? (type 'YES' to confirm)"
        if ($confirm -ne "YES") {
            Write-Host "Installation cancelled."
            exit 0
        }
    }
}

# Create the service
Write-Host "Creating service '$ServiceName'..."
New-Service -Name $ServiceName `
    -BinaryPathName $BinPath `
    -DisplayName $DisplayName `
    -Description $Description `
    -StartupType Manual

# Configure service to restart on failure
sc.exe failure $ServiceName reset= 86400 actions= restart/60000/restart/60000/restart/60000

Write-Host "SUCCESS: Service '$ServiceName' is installed (Manual startup)" -ForegroundColor Green
Write-Host "IMPORTANT: Review configuration before starting the service" -ForegroundColor Yellow
Write-Host "To start: Start-Service -Name $ServiceName" -ForegroundColor Yellow
