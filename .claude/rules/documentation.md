# Documentation Rules
> Last updated: 2026-04-18

**THESE RULES ARE LOADED AUTOMATICALLY IN EVERY SESSION**

---

## Mandatory Documentation Sync

### RULE 1: Test Command Consistency

**ALWAYS** use these exact commands in ALL documentation:

| Component | Command | Notes |
|-----------|---------|-------|
| Dashboard | `npm test` | ⚠️ REQUIRED (NOT `bun test`) - Bun doesn't support DOM |
| Worker | `bun test` | Runs unit + integration tests |
| .NET Services | `dotnet test` | Full test suite |

**When editing test instructions**:
1. Search for test commands: `grep -r "test" --include="*.md" README.md docs/ dashboard/ infra/`
2. Update ALL locations
3. Always include the warning for dashboard: "REQUIRED: use npm, Bun doesn't support DOM"

### RULE 2: README Hierarchy

```
README.md (root)                 ← SOURCE OF TRUTH
  ↓
docs/DEPLOYMENT_GUIDE.md         ← Deployment procedures
docs/ARCHITECTURE.md             ← Architecture details
docs/GETTING_STARTED.md          ← Quick start
  ↓
Component READMEs                ← Component-specific
  dashboard/README.md
  infra/cloudflare/worker/README.md
  scripts/README.md
```

**When updating**:
- Root README → Sync to component READMEs
- Component README → Check if root needs update
- Never contradict root README

### RULE 3: Before Committing Docs

**Checklist** (ALL required):
- [ ] Searched related content: `grep -r "keyword" --include="*.md"`
- [ ] Updated all locations with same information
- [ ] Verified command syntax identical across files
- [ ] Checked version numbers consistent
- [ ] No conflicting information

### RULE 4: Critical Warnings

**ALWAYS preserve these warnings**:

1. **Dashboard testing**:
   ```markdown
   ⚠️ IMPORTANT: Dashboard tests MUST use `npm test` (NOT `bun test`)
   ```

2. **TradingMode**:
   ```markdown
   ⚠️ CRITICAL: Always verify TradingMode = "paper" before deployment
   ```

3. **Secrets**:
   ```markdown
   ⚠️ NEVER commit secrets (API keys, tokens) to git
   ```

### RULE 5: Invoke Documentation Skill

**WHEN**: Editing ANY .md file

**ACTION**: Use `Skill tool` → `skill-documentation-sync` to check consistency

---

*These rules prevent documentation drift and ensure all guides stay synchronized*
