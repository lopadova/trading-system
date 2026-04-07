# Start-NewFeature.ps1
# Prepara ambiente per nuova feature (post-brainstorming)

param(
    [Parameter(Mandatory=$true)]
    [string]$FeatureName
)

$ErrorActionPreference = "Stop"

# Converti nome in slug
$FeatureSlug = $FeatureName.ToLower() -replace ' ', '-'
$FeatureDir = "feature-$(Get-Date -Format 'yyyyMM')-$FeatureSlug"
$ArchiveDir = "docs\archive\$(Get-Date -Format 'yyyy-MM')-build"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "🚀 Starting New Feature: $FeatureName" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# ====================
# 1. Archive Previous
# ====================
Write-Host "📦 Step 1: Archiving previous build..." -ForegroundColor Yellow
New-Item -ItemType Directory -Force -Path "$ArchiveDir\logs" | Out-Null

if (Test-Path "IMPLEMENTATION_REPORT.md") {
    Move-Item "IMPLEMENTATION_REPORT.md" "$ArchiveDir\" -Force
    Write-Host "   ✅ Archived IMPLEMENTATION_REPORT.md" -ForegroundColor Green
}

if (Test-Path "knowledge\SUMMARY.md") {
    Move-Item "knowledge\SUMMARY.md" "$ArchiveDir\" -Force
    Write-Host "   ✅ Archived knowledge\SUMMARY.md" -ForegroundColor Green
}

if (Test-Path "knowledge\task-corrections.md") {
    Move-Item "knowledge\task-corrections.md" "$ArchiveDir\" -Force
    Write-Host "   ✅ Archived task-corrections.md" -ForegroundColor Green
}

$taskLogs = Get-ChildItem "logs\T-*.md" -ErrorAction SilentlyContinue
if ($taskLogs) {
    $taskLogs | Move-Item -Destination "$ArchiveDir\logs\" -Force
    Write-Host "   ✅ Archived task logs" -ForegroundColor Green
}

Write-Host "   📂 Archive location: $ArchiveDir" -ForegroundColor Cyan

# ====================
# 2. Reset State
# ====================
Write-Host ""
Write-Host "🔄 Step 2: Resetting task state..." -ForegroundColor Yellow
'{}' | Set-Content ".agent-state.json"
Write-Host "   ✅ .agent-state.json reset" -ForegroundColor Green

# ====================
# 3. Create Structure
# ====================
Write-Host ""
Write-Host "📁 Step 3: Creating feature structure..." -ForegroundColor Yellow
New-Item -ItemType Directory -Force -Path "docs\trading-system-docs\$FeatureDir" | Out-Null
New-Item -ItemType Directory -Force -Path ".claude\agents\$FeatureDir" | Out-Null
New-Item -ItemType Directory -Force -Path "logs" | Out-Null

Write-Host "   ✅ Feature directories created" -ForegroundColor Green

# ====================
# 4. Create Design Template
# ====================
Write-Host ""
Write-Host "📝 Step 4: Creating design template..." -ForegroundColor Yellow

$designTemplate = @"
# Feature: $FeatureName

> ⚠️ COMPILE THIS AFTER BRAINSTORMING PHASE
> See: docs/brainstorming-output-template.md for guidance

## 1. Obiettivo
[Cosa deve fare questa feature — 2-3 frasi]

## 2. Requisiti Funzionali
- REQ-F-01: [Requisito principale testabile]
- REQ-F-02: [Requisito secondario]
- REQ-F-03: [Edge case critico]

## 3. Requisiti Non-Funzionali
- PERF-01: [Performance requirement]
- SEC-01: [Security requirement]
- OPS-01: [Operability requirement]

## 4. Architettura

### 4.1 Componenti Coinvolti

**Nuovi:**
- [ ] src/NewComponent/
- [ ] dashboard/features/new-feature/

**Modificati:**
- [ ] src/ExistingService/Worker.cs
- [ ] src/SharedKernel/Data/Schema.sql

### 4.2 Data Flow
``````
[Input] → [Processing] → [Storage] → [Output]
``````

### 4.3 Database Changes
- [ ] Nuove tabelle: ``table_name``
- [ ] Nuove colonne: ``existing_table.new_column``
- [ ] Migration file: ``YYYY-MM-DD-description.sql``

## 5. Task Breakdown

### Phase 1: Foundation
- T-00: Setup (structure, dependencies)
- T-01: Database schema
- T-02: Domain models

### Phase 2: Implementation
- T-03: Core business logic
- T-04: API/Integration layer
- T-05: UI components (if applicable)

### Phase 3: Testing
- T-06: Unit tests
- T-07: Integration tests
- T-08: E2E test checklist

## 6. Rischi & Mitigazioni
| Rischio | P | I | Mitigazione |
|---|---|---|---|
| [Esempio: Rate limiting] | M | H | Backoff exponential |

## 7. Rollback Plan
1. [Step to disable feature via config]
2. [Step to rollback DB migration]
3. [Step to restore previous version]

## 8. Success Criteria
- [ ] All tests pass (T-XX-YY)
- [ ] Performance: [metric] < [threshold]
- [ ] No regression on existing features
- [ ] Documentation updated

---
**Created**: $(Get-Date -Format 'yyyy-MM-dd')
**Script**: Start-NewFeature.ps1
"@

$designTemplate | Set-Content "docs\trading-system-docs\$FeatureDir\00-DESIGN.md"
Write-Host "   ✅ Design template: docs\trading-system-docs\$FeatureDir\00-DESIGN.md" -ForegroundColor Green

# ====================
# 5. Create T-00 Template
# ====================
Write-Host ""
Write-Host "📝 Step 5: Creating T-00 setup task..." -ForegroundColor Yellow

$t00Template = @"
# T-00 — $FeatureName Setup

## Pre-Task Knowledge Check

### 1. Rules (auto-loaded)
.claude/rules/*.md are already in context (if you ran Sync-KBToRules after previous feature)

### 2. claude-mem Search (if installed)
If you have ``claude-mem`` plugin, search for related context:
``````
/mem-search "$FeatureSlug"
/mem-search "[related-keyword-from-design]"
``````

### 3. Error Registry (domain-specific check)
``````powershell
# Check for known errors in relevant domain
Select-String -Path "knowledge\errors-registry.md" -Pattern "sqlite|ibkr|dashboard|worker" -CaseSensitive:$false
``````

### 4. Lessons Learned (domain-specific check)
``````powershell
# Check past lessons in relevant domain
Get-Content "knowledge\lessons-learned.md" | Select-String "[domain-keyword]" | Select-Object -Last 20
``````

## Obiettivo
Preparare ambiente per implementazione: $FeatureName

## Checklist
- [ ] Read 00-DESIGN.md completamente
- [ ] Identify new projects/directories needed
- [ ] Update dependencies (NuGet packages, npm packages)
- [ ] Verify clean baseline build (no regressions)
- [ ] Update .agent-state.json

## Implementazione

### Se serve nuovo progetto .NET
``````powershell
# Example: new domain component
dotnet new classlib -n NewComponent -o src/NewComponent
dotnet sln add src/NewComponent/NewComponent.csproj
``````

### Se serve nuova tabella DB
``````powershell
# Create migration in appropriate service
# Example: src/TradingSupervisorService/Data/Migrations/YYYY-MM-DD-feature.sql
``````

### Baseline verification
``````powershell
dotnet restore
dotnet build TradingSystem.sln
dotnet test --no-build
``````

## Test
- TEST-00-01: ``dotnet build TradingSystem.sln`` → 0 errors
- TEST-00-02: ``dotnet test`` → all existing tests PASS (no regression)
- TEST-00-03: If new projects created → they build successfully

## Done Criteria
- Build clean (0 critical warnings)
- All existing tests still pass
- .agent-state.json: ``"T-00": "done"``
- Log produced: ``logs/T-00-result.md``

## Output Format
``````json
{
  "task": "T-00",
  "status": "done",
  "files_created": [
    "src/NewComponent/NewComponent.csproj",
    "..."
  ],
  "next_task": "T-01"
}
``````

---
**Feature**: $FeatureName
**Created**: $(Get-Date -Format 'yyyy-MM-dd')
"@

$t00Template | Set-Content ".claude\agents\$FeatureDir\T-00-setup.md"
Write-Host "   ✅ T-00 template: .claude\agents\$FeatureDir\T-00-setup.md" -ForegroundColor Green

# ====================
# 6. Check claude-mem
# ====================
Write-Host ""
Write-Host "🧠 Step 6: Checking claude-mem availability..." -ForegroundColor Yellow

$ClaudeMemAvailable = $false
if (Get-Command claude -ErrorAction SilentlyContinue) {
    try {
        $skills = & claude --list-skills 2>$null
        if ($skills -match "claude-mem") {
            Write-Host "   ✅ claude-mem plugin AVAILABLE" -ForegroundColor Green
            Write-Host "   💡 Use: /mem-search `"<keyword>`" during tasks" -ForegroundColor Cyan
            $ClaudeMemAvailable = $true
        } else {
            Write-Host "   ⚠️  claude-mem NOT installed (optional)" -ForegroundColor Yellow
            Write-Host "   💡 Install: https://github.com/padolsey/claude-mem" -ForegroundColor Cyan
        }
    } catch {
        Write-Host "   ⚠️  Could not check skills" -ForegroundColor Yellow
        $ClaudeMemAvailable = $false
    }
} else {
    Write-Host "   ⚠️  Claude CLI not in PATH" -ForegroundColor Yellow
    $ClaudeMemAvailable = $false
}

# ====================
# 7. Knowledge Reminder
# ====================
Write-Host ""
Write-Host "📚 Step 7: Knowledge check reminder..." -ForegroundColor Yellow
Write-Host ""
Write-Host "⚠️  BEFORE STARTING IMPLEMENTATION, REVIEW:" -ForegroundColor Red
Write-Host ""

Write-Host "1. Rules (auto-loaded if exist):"
if (Test-Path ".claude\rules") {
    $ruleFiles = Get-ChildItem ".claude\rules\*.md" -ErrorAction SilentlyContinue
    if ($ruleFiles) {
        $ruleFiles | ForEach-Object { Write-Host "   - $($_.Name)" -ForegroundColor Cyan }
    } else {
        Write-Host "   (No rules yet — will be created after first sync)" -ForegroundColor Gray
    }
} else {
    Write-Host "   (No rules yet)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "2. Recent Errors (last 10):"
if (Test-Path "knowledge\errors-registry.md") {
    Get-Content "knowledge\errors-registry.md" |
        Select-String "^## ERR-" |
        Select-Object -Last 10 |
        ForEach-Object { Write-Host "   $_" -ForegroundColor Yellow }
} else {
    Write-Host "   (No errors yet)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "3. Recent Lessons (last 10):"
if (Test-Path "knowledge\lessons-learned.md") {
    Get-Content "knowledge\lessons-learned.md" |
        Select-String "^- LESSON-" |
        Select-Object -Last 10 |
        ForEach-Object { Write-Host "   $_" -ForegroundColor Cyan }
} else {
    Write-Host "   (No lessons yet)" -ForegroundColor Gray
}

# ====================
# 8. Next Steps
# ====================
Write-Host ""
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "✅ Feature Setup Complete!" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "📂 Feature: $FeatureDir" -ForegroundColor Cyan
Write-Host ""
Write-Host "📝 NEXT STEPS:" -ForegroundColor Yellow
Write-Host ""
Write-Host "1️⃣  BRAINSTORMING (with AI - ChatGPT/Claude):" -ForegroundColor White
Write-Host "   - Use: docs\brainstorming-output-template.md as guide"
Write-Host "   - Fill: docs\trading-system-docs\$FeatureDir\00-DESIGN.md"
Write-Host "   - Create task files: .claude\agents\$FeatureDir\T-01.md, T-02.md, ..."
Write-Host ""
Write-Host "2️⃣  IMPLEMENTATION:" -ForegroundColor White
Write-Host "   - Run orchestrator: .\scripts\Run-Agents.ps1 -FeatureDir $FeatureDir -StartTask 0 -EndTask N"
Write-Host "   - OR run tasks manually one-by-one"
Write-Host ""
Write-Host "3️⃣  DURING EXECUTION:" -ForegroundColor White
Write-Host "   - Update knowledge\errors-registry.md (new errors discovered)"
Write-Host "   - Update knowledge\lessons-learned.md (new insights)"
Write-Host ""
Write-Host "4️⃣  AFTER ALL TASKS COMPLETE:" -ForegroundColor White
Write-Host "   - Run: .\scripts\Sync-KBToRules.ps1"
Write-Host "   - This syncs discoveries → .claude\rules\ for next feature"
Write-Host ""
Write-Host "5️⃣  COMMIT & DEPLOY:" -ForegroundColor White
Write-Host "   - Review: .\scripts\pre-deployment-checklist.sh (or .ps1 version)"
Write-Host "   - Commit changes"
Write-Host "   - Deploy as per DEPLOYMENT.md"
Write-Host ""

if ($ClaudeMemAvailable) {
    Write-Host "💡 claude-mem TIP:" -ForegroundColor Magenta
    Write-Host "   During tasks, use: /mem-search `"$FeatureSlug`""
    Write-Host ""
}

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""
