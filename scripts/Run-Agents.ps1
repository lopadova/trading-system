# Run-Agents.ps1 - Orchestrator for multi-task execution
# Usage:
#   .\Run-Agents.ps1 -FeatureDir "feature-name"           # Auto-detect all tasks
#   .\Run-Agents.ps1 -FeatureDir "feature-name" -StartTask 0 -EndTask 5
#   .\Run-Agents.ps1                                       # Auto-detect in .claude\agents\ (root)

param(
    [string]$FeatureDir = "",
    [int]$StartTask = -1,
    [int]$EndTask = -1
)

$ErrorActionPreference = "Stop"

# Determine agent directory (try docs/ first, fallback to .claude/agents/)
if ($FeatureDir -eq "") {
    $AgentDir = ".claude\agents"
    $FeatureName = "root"
} else {
    # Try docs/trading-system-docs/feature-XXX/ first (preferred)
    if (Test-Path "docs\trading-system-docs\$FeatureDir") {
        $AgentDir = "docs\trading-system-docs\$FeatureDir"
        Write-Host "📁 Using task files from: docs\ (preferred location)" -ForegroundColor Cyan
    }
    # Fallback to .claude/agents/feature-XXX/
    elseif (Test-Path ".claude\agents\$FeatureDir") {
        $AgentDir = ".claude\agents\$FeatureDir"
        Write-Host "📁 Using task files from: .claude\agents\ (legacy location)" -ForegroundColor Cyan
    }
    else {
        Write-Host "❌ Error: Feature directory not found" -ForegroundColor Red
        Write-Host ""
        Write-Host "Tried:"
        Write-Host "  - docs\trading-system-docs\$FeatureDir"
        Write-Host "  - .claude\agents\$FeatureDir"
        Write-Host ""
        Write-Host "Usage:"
        Write-Host "  .\Run-Agents.ps1 -FeatureDir `"feature-name`"         # Auto-detect all tasks"
        Write-Host "  .\Run-Agents.ps1 -FeatureDir `"feature-name`" -StartTask 0 -EndTask 5"
        Write-Host ""
        Write-Host "Example:"
        Write-Host "  .\Run-Agents.ps1 -FeatureDir `"feature-202604-alerts`""
        exit 1
    }
    $FeatureName = $FeatureDir
}

# Auto-detect task range if not specified
if ($StartTask -eq -1 -or $EndTask -eq -1) {
    Write-Host "🔍 Auto-detecting tasks in $AgentDir..." -ForegroundColor Cyan
    Write-Host ""

    # Find all T-XX.md files and extract numbers
    $TaskFiles = Get-ChildItem -Path $AgentDir -Filter "T-*.md" -File -ErrorAction SilentlyContinue | Sort-Object Name

    if ($TaskFiles.Count -eq 0) {
        Write-Host "❌ No task files found in $AgentDir" -ForegroundColor Red
        Write-Host ""
        Write-Host "Expected: $AgentDir\T-00.md, T-01.md, ..."
        exit 1
    }

    # Extract task numbers
    $TaskNumbers = $TaskFiles | ForEach-Object {
        if ($_.Name -match 'T-(\d+)') {
            [int]$matches[1]
        }
    } | Sort-Object

    $StartTask = $TaskNumbers | Select-Object -First 1
    $EndTask = $TaskNumbers | Select-Object -Last 1
    $TaskCount = $TaskNumbers.Count

    Write-Host "✅ Found $TaskCount tasks (T-$($StartTask.ToString('D2')) to T-$($EndTask.ToString('D2')))" -ForegroundColor Green
    Write-Host ""
}

# Auto-populate .agent-state.json if empty or missing
if (-not (Test-Path ".agent-state.json") -or (Get-Content ".agent-state.json" -Raw).Trim() -eq "{}") {
    Write-Host "🔧 Populating .agent-state.json with detected tasks..." -ForegroundColor Yellow

    $state = @{}
    for ($i = $StartTask; $i -le $EndTask; $i++) {
        $taskKey = "T-$($i.ToString('D2'))"
        $state[$taskKey] = "pending"
    }

    $state | ConvertTo-Json | Set-Content ".agent-state.json"
    Write-Host "   ✅ Initialized .agent-state.json with $($state.Count) tasks" -ForegroundColor Green
    Write-Host ""
}

# Display header
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "🚀 Orchestrator Starting" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "Feature: $FeatureName"
Write-Host "Directory: $AgentDir"
Write-Host "Task range: T-$($StartTask.ToString('D2')) to T-$($EndTask.ToString('D2'))"
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# Execute tasks sequentially
$CompletedCount = 0
$FailedCount = 0

for ($i = $StartTask; $i -le $EndTask; $i++) {
    $TaskNum = $i.ToString("D2")
    $TaskFile = Get-ChildItem -Path $AgentDir -Filter "T-$TaskNum*.md" -File -ErrorAction SilentlyContinue | Select-Object -First 1

    if (-not $TaskFile) {
        Write-Host "⚠️  Task T-$TaskNum not found in $AgentDir, skipping" -ForegroundColor Yellow
        Write-Host ""
        continue
    }

    $TaskName = $TaskFile.BaseName

    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Gray
    Write-Host "🚀 Task T-$TaskNum: $TaskName" -ForegroundColor Cyan
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Gray
    Write-Host ""

    # Launch Claude with task context
    claude --file $TaskFile.FullName `
           --file "CLAUDE.md" `
           --file "knowledge\errors-registry.md" `
           --file "knowledge\lessons-learned.md" `
           "Execute this task. Use /mem-search if claude-mem is available."

    Write-Host ""

    # Check if task succeeded
    if (Test-Path ".agent-state.json") {
        $state = Get-Content ".agent-state.json" -Raw | ConvertFrom-Json
        $taskState = $state."T-$TaskNum"

        if ($taskState -eq "done") {
            Write-Host "✅ T-$TaskNum completed successfully" -ForegroundColor Green
            $CompletedCount++
        } else {
            Write-Host ""
            Write-Host "❌ T-$TaskNum failed or not marked as done" -ForegroundColor Red
            Write-Host "   State: $taskState"
            Write-Host "   Check: logs\T-$TaskNum-result.md"
            Write-Host ""
            Write-Host "Stopping orchestrator." -ForegroundColor Red
            $FailedCount++
            exit 1
        }
    } else {
        Write-Host "⚠️  Warning: .agent-state.json not found" -ForegroundColor Yellow
        Write-Host "   Task state unknown"
    }

    Write-Host ""
}

# Summary
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "✅ All Tasks Completed Successfully!" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Summary:"
Write-Host "  - Completed: $CompletedCount tasks" -ForegroundColor Green
Write-Host "  - Failed: $FailedCount tasks"
Write-Host "  - Feature: $FeatureName"
Write-Host ""

# ============================================
# AUTOMATIC SYNC: KB → Rules
# ============================================
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Gray
Write-Host "🔄 Step: Sync Knowledge Base → Rules" -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Gray
Write-Host ""
Write-Host "This extracts critical discoveries and generates"
Write-Host ".claude\rules\ files for auto-loading in next feature."
Write-Host ""

if (Test-Path "scripts\Sync-KBToRules.ps1") {
    & ".\scripts\Sync-KBToRules.ps1"

    Write-Host ""
    Write-Host "✅ Knowledge base synchronized to rules" -ForegroundColor Green
    Write-Host "   (.claude\rules\ updated for next feature)"
} else {
    Write-Host "⚠️  scripts\Sync-KBToRules.ps1 not found" -ForegroundColor Yellow
    Write-Host "   Run manually: .\scripts\Sync-KBToRules.ps1"
}

Write-Host ""
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "🎉 Feature Implementation Complete!" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "📋 Next Steps:" -ForegroundColor Yellow
Write-Host ""
Write-Host "  1. Verify E2E:"
Write-Host "     .\scripts\verify-e2e.ps1"
Write-Host ""
Write-Host "  2. Pre-deployment checklist:"
Write-Host "     .\scripts\pre-deployment-checklist.sh  # (or .ps1 version)"
Write-Host ""
Write-Host "  3. Commit changes:"
Write-Host "     git add ."
Write-Host "     git commit -m `"feat: $FeatureName implementation`""
Write-Host ""
Write-Host "  4. Deploy (if applicable):"
Write-Host "     See DEPLOYMENT.md"
Write-Host ""
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""
