# Documentation System

> **Purpose**: Maintain consistency across all README and documentation files  
> **Created**: 2026-04-18  
> **Owner**: Lorenzo Padovani (Padosoft)

---

## Overview

This system prevents documentation drift by ensuring all README files and guides stay synchronized when any markdown file is modified.

## Components

### 1. Skill: `skill-documentation-sync`

**Location**: `.claude/skills/skill-documentation-sync.md`

**Purpose**: Automated checks when editing any .md file

**Triggers**: Automatically when Claude Code edits any README or documentation

**What it does**:
- Identifies related documentation that needs updates
- Provides sync patterns for common changes (tests, architecture, deployment)
- Validates consistency across files

### 2. Rules: `documentation.md`

**Location**: `.claude/rules/documentation.md`

**Purpose**: Hard rules loaded in every Claude Code session

**What it enforces**:
- Test command consistency (npm test for dashboard, bun test for worker)
- README hierarchy (root README is source of truth)
- Pre-commit checklist
- Critical warning preservation

### 3. GitHub Copilot Instructions

**Location**: `.github/copilot-instructions.md`

**Purpose**: Same standards for GitHub Copilot users

**Contains**:
- All coding standards from CLAUDE.md
- Documentation sync rules
- Common patterns and anti-patterns
- Quick reference for port numbers, env vars

---

## Documentation Hierarchy

```
README.md (root)                     ← SOURCE OF TRUTH
  ↓ High-level overview
  ↓ Quick start
  ↓ Testing (all components)
  ↓ Deployment (all components)
  ↓
docs/DEPLOYMENT_GUIDE.md             ← Detailed deployment
docs/ARCHITECTURE.md                 ← System architecture
docs/GETTING_STARTED.md              ← Developer onboarding
docs/CONTRIBUTING.md                 ← Contribution guide
  ↓
Component READMEs                    ← Component-specific
  dashboard/README.md                  (React app)
  infra/cloudflare/worker/README.md    (Hono worker)
  scripts/README.md                    (Automation)
  tests/README.md                      (Testing overview)
```

---

## When to Sync

### Scenario 1: Changing Test Commands

**Trigger**: Modifying test instructions in ANY file

**Files to check**:
- `README.md` (line ~753)
- `dashboard/README.md` (line ~45)
- `infra/cloudflare/worker/README.md` (line ~85)
- `docs/DEPLOYMENT_GUIDE.md` (line ~158)

**Pattern**:
```bash
# Search for test commands
grep -A 5 "npm test\|bun test\|dotnet test" \
  README.md \
  dashboard/README.md \
  infra/cloudflare/worker/README.md \
  docs/DEPLOYMENT_GUIDE.md
```

### Scenario 2: Architecture Changes

**Trigger**: Adding/removing components or changing communication

**Files to check**:
- `README.md` → Architecture diagram
- `docs/ARCHITECTURE.md` → Detailed architecture
- Component README → Component overview

**Pattern**: Update diagrams and descriptions in all locations

### Scenario 3: Version Requirements

**Trigger**: Upgrading .NET, Bun, Node, or other tools

**Files to check**:
- `README.md` → Prerequisites
- `docs/GETTING_STARTED.md` → Requirements
- `docs/DEPLOYMENT_GUIDE.md` → Prerequisites
- Component READMEs → Installation sections

**Pattern**: Update ALL version numbers together

### Scenario 4: Deployment Procedures

**Trigger**: Changing build/deploy commands or configuration

**Files to check**:
- `README.md` → Deployment section
- `docs/DEPLOYMENT_GUIDE.md` → Full guide
- Component READMEs → Build/deploy sections

**Pattern**: Ensure commands are identical

---

## Workflow

### For Claude Code Users

1. **Edit any .md file**

2. **Skill auto-triggers** (or invoke manually):
   ```
   Use Skill tool → "skill-documentation-sync"
   ```

3. **Follow prompts** to identify related files

4. **Update all locations** with consistent information

5. **Verify before commit**:
   ```bash
   # Check test commands
   grep "npm test" README.md docs/DEPLOYMENT_GUIDE.md dashboard/README.md
   
   # Check versions
   grep -E "\.NET 10|Bun 1\.3|Node 20" \
     README.md \
     docs/GETTING_STARTED.md \
     docs/DEPLOYMENT_GUIDE.md
   ```

### For GitHub Copilot Users

1. **Read** `.github/copilot-instructions.md` before editing docs

2. **Follow documentation sync rules**

3. **Search** for related content:
   ```bash
   grep -r "keyword" --include="*.md" .
   ```

4. **Update all locations** manually

---

## Critical Consistency Points

### Test Commands (NEVER differ)

| Component | Command | Rationale |
|-----------|---------|-----------|
| Dashboard | `npm test` | Bun vitest doesn't support DOM (jsdom/happy-dom) |
| Worker | `bun test` | Runs unit + integration tests |
| .NET | `dotnet test` | Standard .NET test runner |

**Always include warning for dashboard**:
```markdown
⚠️ IMPORTANT: Dashboard tests MUST use `npm test` (NOT `bun test`)
```

### Version Numbers (ALWAYS sync)

- .NET SDK version
- Bun version  
- Node version (dashboard tests)
- Wrangler version
- Framework versions (React, Hono, etc.)

### Port Numbers (NEVER change without global search)

- IBKR TWS Paper: 4002
- IBKR TWS Live: 4001
- Dashboard Dev: 5173
- Worker Local: 8787

### Critical Warnings (PRESERVE everywhere)

1. TradingMode verification
2. Secret handling
3. Dashboard test command
4. Windows Defender setup

---

## Automation

### Pre-commit Hook (Optional)

Create `.git/hooks/pre-commit`:

```bash
#!/bin/bash

if git diff --cached --name-only | grep -q "\.md$"; then
  echo ""
  echo "⚠️  Markdown files changed!"
  echo "    Remember to sync related README files."
  echo ""
  echo "    Quick check:"
  echo "    grep -r 'changed content' --include='*.md' ."
  echo ""
fi
```

Make executable:
```bash
chmod +x .git/hooks/pre-commit
```

### Validation Script

Create `scripts/validate-docs-sync.sh`:

```bash
#!/bin/bash

echo "Validating documentation consistency..."

# Check test commands
if ! grep -q "npm test.*REQUIRED" README.md; then
  echo "❌ README.md missing npm test warning"
  exit 1
fi

if ! grep -q "npm test.*REQUIRED" docs/DEPLOYMENT_GUIDE.md; then
  echo "❌ DEPLOYMENT_GUIDE.md missing npm test warning"
  exit 1
fi

echo "✅ Documentation sync validated"
```

---

## Examples

### Example 1: Update Test Coverage Command

**Change**: Add `--coverage` flag to dashboard tests

**Steps**:

1. **Find locations**:
   ```bash
   grep -n "npm test" README.md dashboard/README.md docs/DEPLOYMENT_GUIDE.md
   ```

2. **Update all files**:
   ```diff
   -npm test
   +npm test -- --coverage
   ```

3. **Verify sync**:
   ```bash
   grep "npm test" README.md dashboard/README.md docs/DEPLOYMENT_GUIDE.md | grep coverage
   # All should show the new command
   ```

### Example 2: Add New Worker Endpoint

**Change**: Added `/api/alerts/unresolved`

**Steps**:

1. **Update architecture**:
   - `README.md` → Add to architecture diagram
   - `docs/ARCHITECTURE.md` → Add to API section
   - `infra/cloudflare/worker/README.md` → Add to endpoints list

2. **Verify consistency**:
   - Endpoint path identical
   - HTTP method same
   - Auth requirements match
   - Response format documented

---

## Troubleshooting

### Problem: Conflicting information in different READMEs

**Solution**:
1. Root README is source of truth
2. Update component READMEs to match root
3. Never contradict root README

### Problem: Forgot to sync a file

**Detection**:
```bash
# Find inconsistencies
grep "npm test" README.md docs/*.md dashboard/README.md
# Compare outputs
```

**Fix**: Update the out-of-sync file

---

## Maintenance

### Monthly Review

- [ ] Check all test commands match
- [ ] Verify version numbers are current
- [ ] Confirm port numbers haven't changed
- [ ] Review critical warnings are present
- [ ] Update this document if workflow changes

### After Major Changes

- [ ] Run full documentation consistency check
- [ ] Update `.github/copilot-instructions.md` if patterns change
- [ ] Review and update skill if new sync scenarios emerge

---

**Last Updated**: 2026-04-18  
**Maintained By**: Lorenzo Padovani (Padosoft)  
**Tools**: Claude Code, GitHub Copilot
