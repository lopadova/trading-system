#!/usr/bin/env pwsh
# deploy-windows-services.ps1
# Deploys TradingSupervisorService and OptionsExecutionService as Windows Services
# Requires: Administrator privileges

#Requires -RunAsAdministrator

param(
    [Parameter()]
    [ValidateSet("Install", "Update", "Uninstall", "Restart")]
    [string]$Action = "Update",

    [Parameter()]
    [string]$InstallPath = "C:\TradingSystem",

    [Parameter()]
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

# Service configuration
$Services = @(
    @{
        Name = "TradingSupervisorService"
        DisplayName = "Trading Supervisor Service"
        Description = "Monitors trading system health, IBKR connection, and collects metrics"
        ExePath = "TradingSupervisorService.exe"
        PublishSource = "publish\TradingSupervisorService"
    },
    @{
        Name = "OptionsExecutionService"
        DisplayName = "Options Execution Service"
        Description = "Executes options trading strategies and manages campaigns"
        ExePath = "OptionsExecutionService.exe"
        PublishSource = "publish\OptionsExecutionService"
    }
)

function Write-Step {
    param([string]$Message)
    Write-Host "`n=== $Message ===" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "✓ $Message" -ForegroundColor Green
}

function Write-Warning {
    param([string]$Message)
    Write-Host "⚠ $Message" -ForegroundColor Yellow
}

function Write-Error {
    param([string]$Message)
    Write-Host "✗ $Message" -ForegroundColor Red
}

function Install-Service {
    param($ServiceConfig)

    $servicePath = Join-Path $InstallPath $ServiceConfig.Name
    $exePath = Join-Path $servicePath $ServiceConfig.ExePath

    # Create service directory
    if (-not (Test-Path $servicePath)) {
        New-Item -ItemType Directory -Path $servicePath -Force | Out-Null
        Write-Success "Created directory: $servicePath"
    }

    # Copy files
    $sourceFiles = Get-ChildItem -Path $ServiceConfig.PublishSource -Recurse
    Copy-Item -Path "$($ServiceConfig.PublishSource)\*" -Destination $servicePath -Recurse -Force
    Write-Success "Copied service files"

    # Create Windows Service
    if ($WhatIf) {
        Write-Host "Would create service: $($ServiceConfig.Name)" -ForegroundColor Yellow
    } else {
        New-Service `
            -Name $ServiceConfig.Name `
            -BinaryPathName $exePath `
            -DisplayName $ServiceConfig.DisplayName `
            -Description $ServiceConfig.Description `
            -StartupType Automatic

        Write-Success "Created Windows Service: $($ServiceConfig.DisplayName)"

        # Start service
        Start-Service -Name $ServiceConfig.Name
        Write-Success "Started service"
    }
}

function Update-Service {
    param($ServiceConfig)

    $service = Get-Service -Name $ServiceConfig.Name -ErrorAction SilentlyContinue

    if (-not $service) {
        Write-Warning "Service not found, installing instead..."
        Install-Service -ServiceConfig $ServiceConfig
        return
    }

    # Stop service
    if ($service.Status -eq "Running") {
        if ($WhatIf) {
            Write-Host "Would stop service: $($ServiceConfig.Name)" -ForegroundColor Yellow
        } else {
            Stop-Service -Name $ServiceConfig.Name -Force
            Write-Success "Stopped service"
        }
    }

    # Update files
    $servicePath = Join-Path $InstallPath $ServiceConfig.Name

    if ($WhatIf) {
        Write-Host "Would update files in: $servicePath" -ForegroundColor Yellow
    } else {
        Copy-Item -Path "$($ServiceConfig.PublishSource)\*" -Destination $servicePath -Recurse -Force
        Write-Success "Updated service files"

        # Start service
        Start-Service -Name $ServiceConfig.Name
        Write-Success "Started service"
    }
}

function Uninstall-Service {
    param($ServiceConfig)

    $service = Get-Service -Name $ServiceConfig.Name -ErrorAction SilentlyContinue

    if (-not $service) {
        Write-Warning "Service not found: $($ServiceConfig.Name)"
        return
    }

    # Stop service
    if ($service.Status -eq "Running") {
        if ($WhatIf) {
            Write-Host "Would stop service: $($ServiceConfig.Name)" -ForegroundColor Yellow
        } else {
            Stop-Service -Name $ServiceConfig.Name -Force
            Write-Success "Stopped service"
        }
    }

    # Remove service
    if ($WhatIf) {
        Write-Host "Would remove service: $($ServiceConfig.Name)" -ForegroundColor Yellow
    } else {
        Remove-Service -Name $ServiceConfig.Name
        Write-Success "Removed Windows Service"
    }

    # Optionally remove files
    $servicePath = Join-Path $InstallPath $ServiceConfig.Name
    if (Test-Path $servicePath) {
        $response = Read-Host "Remove service files from $servicePath? (y/N)"
        if ($response -eq 'y') {
            Remove-Item -Path $servicePath -Recurse -Force
            Write-Success "Removed service files"
        }
    }
}

function Restart-ServiceSafe {
    param($ServiceConfig)

    $service = Get-Service -Name $ServiceConfig.Name -ErrorAction SilentlyContinue

    if (-not $service) {
        Write-Error "Service not found: $($ServiceConfig.Name)"
        return
    }

    if ($WhatIf) {
        Write-Host "Would restart service: $($ServiceConfig.Name)" -ForegroundColor Yellow
    } else {
        Restart-Service -Name $ServiceConfig.Name -Force
        Write-Success "Restarted service: $($ServiceConfig.DisplayName)"
    }
}

# Main execution
Write-Host @"

╔═══════════════════════════════════════════════════╗
║   Trading System - Windows Services Deployment   ║
╚═══════════════════════════════════════════════════╝

"@ -ForegroundColor Cyan

if ($WhatIf) {
    Write-Warning "Running in WhatIf mode - no changes will be made"
}

# Verify publish artifacts exist
foreach ($svc in $Services) {
    if (-not (Test-Path $svc.PublishSource)) {
        Write-Error "Publish artifacts not found: $($svc.PublishSource)"
        Write-Host "Run: dotnet publish first" -ForegroundColor Yellow
        exit 1
    }
}

Write-Success "All publish artifacts found"

# Execute action
switch ($Action) {
    "Install" {
        foreach ($svc in $Services) {
            Write-Step "Installing $($svc.DisplayName)"
            Install-Service -ServiceConfig $svc
        }
    }

    "Update" {
        foreach ($svc in $Services) {
            Write-Step "Updating $($svc.DisplayName)"
            Update-Service -ServiceConfig $svc
        }
    }

    "Uninstall" {
        foreach ($svc in $Services) {
            Write-Step "Uninstalling $($svc.DisplayName)"
            Uninstall-Service -ServiceConfig $svc
        }
    }

    "Restart" {
        foreach ($svc in $Services) {
            Write-Step "Restarting $($svc.DisplayName)"
            Restart-ServiceSafe -ServiceConfig $svc
        }
    }
}

Write-Host "`n"
Write-Success "Deployment complete!"

# Show service status
Write-Step "Current Service Status"
foreach ($svc in $Services) {
    $service = Get-Service -Name $svc.Name -ErrorAction SilentlyContinue
    if ($service) {
        $status = $service.Status
        $color = if ($status -eq "Running") { "Green" } else { "Yellow" }
        Write-Host "  $($svc.DisplayName): " -NoNewline
        Write-Host $status -ForegroundColor $color
    }
}

Write-Host "`nNext steps:" -ForegroundColor Cyan
Write-Host "  1. Check logs in Event Viewer (Windows Logs > Application)"
Write-Host "  2. Verify database files in C:\TradingSystem\data\"
Write-Host "  3. Monitor service health in Services.msc"
Write-Host ""
