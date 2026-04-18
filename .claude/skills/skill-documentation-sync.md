---
name: documentation-sync
description: Maintains consistency across README files and documentation when modifying any .md file
trigger: Automatic when editing any README.md or documentation file
---

# Documentation Synchronization Skill

## Purpose

Ensures all README files and documentation stay consistent when editing any markdown file, preventing documentation drift.

## Documentation Structure

```
trading-system/
├── README.md                                    # PRIMARY REFERENCE (source of truth)
├── dashboard/README.md                          # Dashboard component
├── infra/cloudflare/worker/README.md           # Worker component
├── scripts/README.md                            # Scripts overview
├── tests/README.md                              # Testing overview
├── docs/
│   ├── DEPLOYMENT_GUIDE.md                      # Deployment procedures
│   ├── ARCHITECTURE.md                          # System architecture
│   ├── GETTING_STARTED.md                       # Quick start guide
│   └── CONTRIBUTING.md                          # Contribution guidelines
```

## Sync Rules

### 1. Testing Instructions

**When modifying test commands in ANY README:**

1. **Check these locations**:
   - `README.md` → "Running Tests" section
   - `dashboard/README.md` → "Testing" section
   - `infra/cloudflare/worker/README.md` → "Testing" section
   - `docs/DEPLOYMENT_GUIDE.md` → "Build Services" → Testing subsections

2. **Ensure consistency**:
   - ✅ Dashboard: `npm test` (REQUIRED, NOT `bun test`)
   - ✅ Worker: `bun test` (runs unit + integration)
   - ✅ .NET Services: `dotnet test`

3. **Standard format**:
   ```markdown
   **Dashboard (React/TypeScript)**:
   ```bash
   cd dashboard
   
   # Run all tests (REQUIRED: use npm, Bun doesn't support DOM)
   npm test
   ```
   
   **⚠️ IMPORTANT**: Dashboard tests **MUST** use `npm test` (NOT `bun test`).
   ```

### 2. Architecture Descriptions

**When modifying system architecture:**

1. **Update these files**:
   - `README.md` → "Architecture" section (high-level diagram)
   - `docs/ARCHITECTURE.md` → Detailed architecture
   - Component README → "Overview" section

2. **Maintain consistency**:
   - Component names
   - Communication protocols (HTTP, TCP, etc.)
   - Port numbers
   - Technology stack

### 3. Installation/Setup Instructions

**When modifying setup instructions:**

1. **Check sync with**:
   - `README.md` → "Quick Start"
   - `docs/GETTING_STARTED.md`
   - `docs/DEPLOYMENT_GUIDE.md` → "Prerequisites"
   - Component README → "Installation" sections

2. **Version requirements**:
   - .NET version
   - Bun version
   - Node version (dashboard tests)
   - Wrangler version

### 4. Deployment Instructions

**When modifying deployment procedures:**

1. **Sync files**:
   - `README.md` → "Deployment" section
   - `docs/DEPLOYMENT_GUIDE.md` → Full deployment guide
   - `infra/cloudflare/worker/README.md` → "Deployment" section
   - `dashboard/README.md` → "Build & Deploy" section

2. **Critical consistency checks**:
   - Build commands
   - Configuration file requirements
   - Environment variables
   - Security warnings (TradingMode, secrets)

## Workflow

### When Editing ANY .md File

1. **Identify scope**:
   - Is this a testing procedure? → Apply Testing sync rules
   - Is this architecture? → Apply Architecture sync rules
   - Is this setup/installation? → Apply Installation sync rules
   - Is this deployment? → Apply Deployment sync rules

2. **Find related files**:
   ```bash
   # Search for related content
   grep -r "keyword" --include="*.md" . | grep -v node_modules
   ```

3. **Update all related locations**:
   - Use exact same wording for critical instructions
   - Maintain consistent formatting
   - Preserve component-specific details

4. **Verify consistency**:
   ```bash
   # Check test commands consistency
   grep -A 5 "npm test\|bun test\|dotnet test" README.md docs/DEPLOYMENT_GUIDE.md dashboard/README.md infra/cloudflare/worker/README.md
   ```

## Common Patterns

### Testing Commands

**Pattern**: Always state the REQUIRED tool and WHY

```markdown
# ✅ CORRECT (with context)
npm test           # REQUIRED: Bun doesn't support DOM

# ❌ WRONG (no context)
npm test
```

### Architecture Diagrams

**Pattern**: Use consistent symbols and layout

```
┌─────────────────┐
│  Component      │  ← Box with title
└─────────────────┘
       ↓             ← Arrow for data flow
```

### Prerequisites

**Pattern**: Table format with versions

```markdown
| Tool | Version | Purpose |
|------|---------|---------|
| .NET | 10 SDK  | Services |
| Bun  | 1.3+    | Build    |
| Node | 20+     | Tests    |
```

## Examples

### Example 1: Updating Test Command

**Scenario**: Changed dashboard test command to add coverage flag

1. **Files to update**:
   - `README.md` (line ~760)
   - `dashboard/README.md` (line ~50)
   - `docs/DEPLOYMENT_GUIDE.md` (line ~165)

2. **Change**:
   ```diff
   -npm test
   +npm test -- --coverage
   ```

3. **Verification**:
   ```bash
   grep "npm test" README.md dashboard/README.md docs/DEPLOYMENT_GUIDE.md
   # All should show the same command
   ```

### Example 2: Architecture Update

**Scenario**: Added new Worker endpoint

1. **Files to update**:
   - `README.md` → Architecture diagram (add endpoint)
   - `docs/ARCHITECTURE.md` → API section
   - `infra/cloudflare/worker/README.md` → Endpoints list

2. **Check consistency**:
   - Endpoint path (`/api/new-endpoint`)
   - HTTP method (GET, POST, etc.)
   - Authentication requirements
   - Response format

## Automation

### Pre-commit Hook (Optional)

```bash
#!/bin/bash
# .git/hooks/pre-commit

# Check if any .md files changed
if git diff --cached --name-only | grep -q "\.md$"; then
  echo "⚠️  Markdown files changed. Remember to sync related README files!"
  echo "   Run: grep -r 'changed content' --include='*.md' ."
fi
```

### Validation Script

```bash
# scripts/validate-docs-sync.sh
#!/bin/bash

# Check test command consistency
if ! grep -q "npm test.*REQUIRED" README.md; then
  echo "❌ README.md missing REQUIRED note for npm test"
  exit 1
fi

if ! grep -q "npm test.*REQUIRED" docs/DEPLOYMENT_GUIDE.md; then
  echo "❌ DEPLOYMENT_GUIDE.md missing REQUIRED note for npm test"
  exit 1
fi

echo "✅ Documentation sync validated"
```

## Error Prevention

### Before Committing

**Checklist**:
- [ ] Searched for related content in other .md files
- [ ] Updated all locations with same information
- [ ] Verified command syntax is identical
- [ ] Checked version numbers are consistent
- [ ] Confirmed no duplicate or conflicting info

### Red Flags

🚩 **Watch for these**:
- Different test commands in different files
- Mismatched version requirements
- Inconsistent component names
- Conflicting architecture descriptions
- Outdated examples in older docs

---

*Skill version: 1.0 — Created: 2026-04-18 — Purpose: Prevent documentation drift*
