# T-00 — wizard-strategies-and-bot Setup

## Pre-Task Knowledge Check

### 1. Rules (auto-loaded)
.claude/rules/*.md are already in context (if you ran sync-kb-to-rules after previous feature)

### 2. claude-mem Search (if installed)
If you have `claude-mem` plugin, search for related context:
```
/mem-search "wizard-strategies-and-bot"
/mem-search "[related-keyword-from-design]"
```

### 3. Error Registry (domain-specific check)
```bash
# Check for known errors in relevant domain
grep -i "sqlite\|ibkr\|dashboard\|worker" knowledge/errors-registry.md
```

### 4. Lessons Learned (domain-specific check)
```bash
# Check past lessons in relevant domain
grep -i "[domain-keyword]" knowledge/lessons-learned.md | tail -20
```

## Obiettivo
Preparare ambiente per implementazione: wizard-strategies-and-bot

## Checklist
- [ ] Read 00-DESIGN.md completamente
- [ ] Identify new projects/directories needed
- [ ] Update dependencies (NuGet packages, npm packages)
- [ ] Verify clean baseline build (no regressions)
- [ ] Update .agent-state.json

## Implementazione

### Se serve nuovo progetto .NET
```bash
# Example: new domain component
dotnet new classlib -n NewComponent -o src/NewComponent
dotnet sln add src/NewComponent/NewComponent.csproj
```

### Se serve nuova tabella DB
```bash
# Create migration in appropriate service
# Example: src/TradingSupervisorService/Data/Migrations/YYYY-MM-DD-feature.sql
```

### Baseline verification
```bash
dotnet restore
dotnet build TradingSystem.sln
dotnet test --no-build
```

## Test
- TEST-00-01: `dotnet build TradingSystem.sln` → 0 errors
- TEST-00-02: `dotnet test` → all existing tests PASS (no regression)
- TEST-00-03: If new projects created → they build successfully

## Done Criteria
- Build clean (0 critical warnings)
- All existing tests still pass
- .agent-state.json: `"T-00": "done"`
- Log produced: `logs/T-00-result.md`

## Output Format
```json
{
  "task": "T-00",
  "status": "done",
  "files_created": [
    "src/NewComponent/NewComponent.csproj",
    "..."
  ],
  "next_task": "T-01"
}
```

---
**Feature**: wizard-strategies-and-bot
**Created**: 2026-04-06
