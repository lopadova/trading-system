# Install OptionsExecutionService as Windows Service
# Run as Administrator
#
# Phase 7.5: accepts -Environment and sets DOTNET_ENVIRONMENT + ASPNETCORE_ENVIRONMENT
# in the service's registry-backed environment block.

param(
    [string]$ServiceName = "OptionsExecutionService",
    [string]$DisplayName = "Options Execution Service",
    [string]$Description = "Executes options trading strategies via Interactive Brokers",
    [string]$BinPath = "$PSScriptRoot\..\..\src\OptionsExecutionService\bin\Release\net10.0\win-x64\publish\OptionsExecutionService.exe",

    # Which appsettings.<env>.json layer to activate. Defaults to Production.
    # Staging mirrors prod shape but connects to the staging Worker + paper IBKR.
    [ValidateSet("Production", "Staging", "Development")]
    [string]$Environment = "Production"
)

# Verify running as Administrator
if (-NOT ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Error "This script must be run as Administrator"
    exit 1
}

# Environment banner — loud + explicit so an accidental "Production" install on
# the staging box is obvious before the operator hits Enter on the confirmation.
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
    Write-Host "  dotnet publish -c Release -r win-x64 --self-contained src\OptionsExecutionService\OptionsExecutionService.csproj"
    exit 1
}

# SAFETY CHECK: Ensure appsettings.json (or the selected env overlay) has TradingMode = "paper".
# We check BOTH the base settings AND the env-specific overlay because the
# overlay might flip TradingMode to 'live' even when the base file stays 'paper'.
$publishDir = Split-Path -Parent $BinPath
$appSettingsBase = Join-Path $publishDir 'appsettings.json'
$appSettingsEnv = Join-Path $publishDir "appsettings.$Environment.json"

function Read-TradingMode {
    param([string]$Path)
    if (-not (Test-Path $Path)) { return $null }
    try {
        $cfg = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
        return $cfg.TradingMode
    } catch {
        Write-Warning "Failed to parse $Path : $_"
        return $null
    }
}

$baseMode = Read-TradingMode $appSettingsBase
$envMode = Read-TradingMode $appSettingsEnv
# Env overlay wins if present, otherwise fall back to the base setting.
$effectiveMode = if ($envMode) { $envMode } else { $baseMode }

if ($effectiveMode -and $effectiveMode -ne "paper") {
    Write-Warning "WARNING: effective TradingMode is '$effectiveMode' (env=$Environment)"
    Write-Warning "This service will execute REAL TRADES with REAL MONEY!"
    $confirm = Read-Host "Are you ABSOLUTELY SURE you want to proceed? (type 'YES' to confirm)"
    if ($confirm -ne "YES") {
        Write-Host "Installation cancelled."
        exit 0
    }
}

# Create the service
Write-Host "Creating service '$ServiceName'..."
New-Service -Name $ServiceName `
    -BinaryPathName $BinPath `
    -DisplayName $DisplayName `
    -Description $Description `
    -StartupType Manual

# Inject DOTNET_ENVIRONMENT into the service's environment (see install-supervisor.ps1
# for rationale on the registry approach). Both DOTNET_ENVIRONMENT and
# ASPNETCORE_ENVIRONMENT are set — .NET hosts read whichever is present first.
$svcRegPath = "HKLM:\SYSTEM\CurrentControlSet\Services\$ServiceName"
$envEntries = @("DOTNET_ENVIRONMENT=$Environment", "ASPNETCORE_ENVIRONMENT=$Environment")
New-ItemProperty -Path $svcRegPath -Name 'Environment' -Value $envEntries -PropertyType MultiString -Force | Out-Null
Write-Host "Set DOTNET_ENVIRONMENT=$Environment on service '$ServiceName'"

# Configure service to restart on failure
sc.exe failure $ServiceName reset= 86400 actions= restart/60000/restart/60000/restart/60000

Write-Host "SUCCESS: Service '$ServiceName' is installed (Manual startup, Environment=$Environment)" -ForegroundColor Green
Write-Host "IMPORTANT: Review configuration before starting the service" -ForegroundColor Yellow
Write-Host "To start: Start-Service -Name $ServiceName" -ForegroundColor Yellow
