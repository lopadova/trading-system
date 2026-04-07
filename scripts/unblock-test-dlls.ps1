#Requires -Version 5.1

<#
.SYNOPSIS
    Unblocks test DLLs that are marked as blocked by Windows

.DESCRIPTION
    Uses Unblock-File to remove the "blocked" mark from compiled test DLLs.
    This is needed when Windows Defender Application Control blocks the files.

.EXAMPLE
    .\scripts\unblock-test-dlls.ps1
#>

$ErrorActionPreference = "Continue"

$projectRoot = Split-Path $PSScriptRoot -Parent

Write-Host "Unblocking DLL files..." -ForegroundColor Yellow

# Find all DLL files in bin directories
$dlls = Get-ChildItem -Path $projectRoot -Filter "*.dll" -Recurse |
        Where-Object { $_.FullName -match "\\bin\\Debug\\net10\.0\\" }

$count = 0
foreach ($dll in $dlls) {
    try {
        Unblock-File -Path $dll.FullName -ErrorAction Stop
        Write-Host "✓ Unblocked: $($dll.Name)" -ForegroundColor Green
        $count++
    }
    catch {
        Write-Host "⚠️  Could not unblock: $($dll.Name)" -ForegroundColor Yellow
    }
}

Write-Host "`nTotal files unblocked: $count" -ForegroundColor Cyan
Write-Host "`nNow run:" -ForegroundColor White
Write-Host "  dotnet test --no-build" -ForegroundColor Cyan
