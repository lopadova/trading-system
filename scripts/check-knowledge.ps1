# Mostra stato della knowledge base accumulata dagli agenti

$ErrorActionPreference = 'Continue'

Write-Host "=== KNOWLEDGE BASE STATUS ==="
Write-Host ""

# Conta errori registrati
Write-Host "Errors registry:"
$errorsFile = "knowledge\errors-registry.md"
if (Test-Path $errorsFile) {
    $errorCount = (Select-String -Path $errorsFile -Pattern '^## ERR-' -AllMatches).Count
    Write-Host "  $errorCount errori registrati"
} else {
    Write-Host "  0 errori registrati (file non esiste)"
}

# Conta lezioni apprese
Write-Host "Lessons learned:"
$lessonsFile = "knowledge\lessons-learned.md"
if (Test-Path $lessonsFile) {
    $lessonsCount = (Select-String -Path $lessonsFile -Pattern '^## LL-' -AllMatches).Count
    Write-Host "  $lessonsCount lezioni"
} else {
    Write-Host "  0 lezioni (file non esiste)"
}

# Conta aggiornamenti skill
Write-Host "Skill updates:"
$changelogFile = "knowledge\skill-changelog.md"
if (Test-Path $changelogFile) {
    $updatesCount = (Select-String -Path $changelogFile -Pattern '^## 20' -AllMatches).Count
    Write-Host "  $updatesCount aggiornamenti skill"
} else {
    Write-Host "  0 aggiornamenti skill (file non esiste)"
}

# Conta correzioni task
Write-Host "Task corrections:"
$correctionsFile = "knowledge\task-corrections.md"
if (Test-Path $correctionsFile) {
    $correctionsCount = (Select-String -Path $correctionsFile -Pattern '^## CORR-' -AllMatches).Count
    Write-Host "  $correctionsCount correzioni"
} else {
    Write-Host "  0 correzioni (file non esiste)"
}

Write-Host ""
Write-Host "=== AGENT STATE ==="

if (Test-Path ".agent-state.json") {
    $state = Get-Content ".agent-state.json" -Raw | ConvertFrom-Json

    # Raggruppa per status
    $statuses = @("done", "running", "failed", "pending")
    foreach ($status in $statuses) {
        $tasks = @()
        $state.PSObject.Properties | ForEach-Object {
            if ($_.Value -eq $status) {
                $tasks += $_.Name
            }
        }

        if ($tasks.Count -gt 0) {
            $tasksList = $tasks -join " "
            Write-Host "  $($status.ToUpper()): $($tasks.Count) — $tasksList"
        }
    }
} else {
    Write-Host "  (no state file)"
}

Write-Host ""
