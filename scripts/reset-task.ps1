# Resetta un task a "pending" per ri-eseguirlo
# Uso: .\scripts\reset-task.ps1 T-05

param(
    [Parameter(Mandatory=$true)]
    [string]$TaskId
)

$ErrorActionPreference = 'Stop'

# Verifica formato task ID
if ($TaskId -notmatch '^T-\d{2}$') {
    Write-Error "Invalid task ID format. Use T-XX (e.g., T-05)"
    exit 1
}

# Verifica esistenza state file
if (-not (Test-Path ".agent-state.json")) {
    Write-Error ".agent-state.json not found. Run run-agents.ps1 first to initialize."
    exit 1
}

# Aggiorna lo state
$state = Get-Content ".agent-state.json" -Raw | ConvertFrom-Json

if (-not $state.PSObject.Properties.Name -contains $TaskId) {
    Write-Error "Task $TaskId not found in state file"
    exit 1
}

$previousState = $state.$TaskId
$state.$TaskId = "pending"

$state | ConvertTo-Json -Depth 10 | Set-Content -Path ".agent-state.json" -Encoding UTF8
Write-Host "Reset $TaskId from '$previousState' to 'pending'"

# Rinomina il report precedente se esiste
$reportFile = "logs\$TaskId-result.md"
if (Test-Path $reportFile) {
    $timestamp = Get-Date -Format "yyyyMMddHHmmss"
    $backupFile = "logs\$TaskId-result.$timestamp.bak.md"

    Move-Item -Path $reportFile -Destination $backupFile -Force
    Write-Host "Previous report archived to: $backupFile"
}

Write-Host ""
Write-Host "Task $TaskId is now ready to be re-run."
