#!/usr/bin/env bash
# start-new-feature.sh
# Prepara ambiente per nuova feature (post-brainstorming)

set -euo pipefail

FEATURE_NAME=${1:-}
if [ -z "$FEATURE_NAME" ]; then
  echo "Usage: $0 <feature-name>"
  echo "Example: $0 real-time-alerts"
  exit 1
fi

# Converti nome in slug
FEATURE_SLUG=$(echo "$FEATURE_NAME" | tr '[:upper:]' '[:lower:]' | tr ' ' '-')
FEATURE_DIR="feature-$(date +%Y%m)-$FEATURE_SLUG"
ARCHIVE_DIR="docs/archive/$(date +%Y-%m)-build"

echo "=========================================="
echo "🚀 Starting New Feature: $FEATURE_NAME"
echo "=========================================="
echo ""

# ====================
# 1. Archive Previous
# ====================
echo "📦 Step 1: Archiving previous build..."
mkdir -p "$ARCHIVE_DIR/logs"

if [ -f "IMPLEMENTATION_REPORT.md" ]; then
  mv IMPLEMENTATION_REPORT.md "$ARCHIVE_DIR/"
  echo "   ✅ Archived IMPLEMENTATION_REPORT.md"
fi

if [ -f "knowledge/SUMMARY.md" ]; then
  mv knowledge/SUMMARY.md "$ARCHIVE_DIR/"
  echo "   ✅ Archived knowledge/SUMMARY.md"
fi

if [ -f "knowledge/task-corrections.md" ]; then
  mv knowledge/task-corrections.md "$ARCHIVE_DIR/"
  echo "   ✅ Archived task-corrections.md"
fi

if ls logs/T-*.md 1> /dev/null 2>&1; then
  mv logs/T-*.md "$ARCHIVE_DIR/logs/"
  echo "   ✅ Archived task logs"
fi

echo "   📂 Archive location: $ARCHIVE_DIR"

# ====================
# 2. Reset State
# ====================
echo ""
echo "🔄 Step 2: Resetting task state..."
echo '{}' > .agent-state.json
echo "   ✅ .agent-state.json reset"

# ====================
# 3. Create Structure
# ====================
echo ""
echo "📁 Step 3: Creating feature structure..."
mkdir -p "docs/trading-system-docs/$FEATURE_DIR"
mkdir -p ".claude/agents/$FEATURE_DIR"
mkdir -p logs

echo "   ✅ Feature directories created"

# ====================
# 4. Create Design Template
# ====================
echo ""
echo "📝 Step 4: Creating design template..."

cat > "docs/trading-system-docs/$FEATURE_DIR/00-DESIGN.md" << EOF
# Feature: $FEATURE_NAME

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
\`\`\`
[Input] → [Processing] → [Storage] → [Output]
\`\`\`

### 4.3 Database Changes
- [ ] Nuove tabelle: \`table_name\`
- [ ] Nuove colonne: \`existing_table.new_column\`
- [ ] Migration file: \`YYYY-MM-DD-description.sql\`

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
**Created**: $(date +%Y-%m-%d)
**Script**: start-new-feature.sh
EOF

echo "   ✅ Design template: docs/trading-system-docs/$FEATURE_DIR/00-DESIGN.md"

# ====================
# 5. Create T-00 Template
# ====================
echo ""
echo "📝 Step 5: Creating T-00 setup task..."

cat > ".claude/agents/$FEATURE_DIR/T-00-setup.md" << EOF
# T-00 — $FEATURE_NAME Setup

## Pre-Task Knowledge Check

### 1. Rules (auto-loaded)
.claude/rules/*.md are already in context (if you ran sync-kb-to-rules after previous feature)

### 2. claude-mem Search (if installed)
If you have \`claude-mem\` plugin, search for related context:
\`\`\`
/mem-search "$FEATURE_SLUG"
/mem-search "[related-keyword-from-design]"
\`\`\`

### 3. Error Registry (domain-specific check)
\`\`\`bash
# Check for known errors in relevant domain
grep -i "sqlite\|ibkr\|dashboard\|worker" knowledge/errors-registry.md
\`\`\`

### 4. Lessons Learned (domain-specific check)
\`\`\`bash
# Check past lessons in relevant domain
grep -i "[domain-keyword]" knowledge/lessons-learned.md | tail -20
\`\`\`

## Obiettivo
Preparare ambiente per implementazione: $FEATURE_NAME

## Checklist
- [ ] Read 00-DESIGN.md completamente
- [ ] Identify new projects/directories needed
- [ ] Update dependencies (NuGet packages, npm packages)
- [ ] Verify clean baseline build (no regressions)
- [ ] Update .agent-state.json

## Implementazione

### Se serve nuovo progetto .NET
\`\`\`bash
# Example: new domain component
dotnet new classlib -n NewComponent -o src/NewComponent
dotnet sln add src/NewComponent/NewComponent.csproj
\`\`\`

### Se serve nuova tabella DB
\`\`\`bash
# Create migration in appropriate service
# Example: src/TradingSupervisorService/Data/Migrations/YYYY-MM-DD-feature.sql
\`\`\`

### Baseline verification
\`\`\`bash
dotnet restore
dotnet build TradingSystem.sln
dotnet test --no-build
\`\`\`

## Test
- TEST-00-01: \`dotnet build TradingSystem.sln\` → 0 errors
- TEST-00-02: \`dotnet test\` → all existing tests PASS (no regression)
- TEST-00-03: If new projects created → they build successfully

## Done Criteria
- Build clean (0 critical warnings)
- All existing tests still pass
- .agent-state.json: \`"T-00": "done"\`
- Log produced: \`logs/T-00-result.md\`

## Output Format
\`\`\`json
{
  "task": "T-00",
  "status": "done",
  "files_created": [
    "src/NewComponent/NewComponent.csproj",
    "..."
  ],
  "next_task": "T-01"
}
\`\`\`

---
**Feature**: $FEATURE_NAME
**Created**: $(date +%Y-%m-%d)
EOF

echo "   ✅ T-00 template: .claude/agents/$FEATURE_DIR/T-00-setup.md"

# ====================
# 6. Check claude-mem
# ====================
echo ""
echo "🧠 Step 6: Checking claude-mem availability..."

if command -v claude &> /dev/null; then
  # Try to detect if claude-mem is available
  if claude --list-skills 2>/dev/null | grep -q "claude-mem"; then
    echo "   ✅ claude-mem plugin AVAILABLE"
    echo "   💡 Use: /mem-search \"<keyword>\" during tasks"
    CLAUDE_MEM_AVAILABLE=true
  else
    echo "   ⚠️  claude-mem NOT installed (optional)"
    echo "   💡 Install: https://github.com/padolsey/claude-mem"
    CLAUDE_MEM_AVAILABLE=false
  fi
else
  echo "   ⚠️  Claude CLI not in PATH"
  CLAUDE_MEM_AVAILABLE=false
fi

# ====================
# 7. Knowledge Reminder
# ====================
echo ""
echo "📚 Step 7: Knowledge check reminder..."
echo ""
echo "⚠️  BEFORE STARTING IMPLEMENTATION, REVIEW:" -ForegroundColor Yellow
echo ""

echo "1. Rules (auto-loaded if exist):"
if [ -d ".claude/rules" ] && [ "$(ls -A .claude/rules 2>/dev/null)" ]; then
  ls -1 .claude/rules/*.md 2>/dev/null | sed 's/^/   - /'
else
  echo "   (No rules yet — will be created after first sync)"
fi

echo ""
echo "2. Recent Errors (last 10):"
if [ -f "knowledge/errors-registry.md" ]; then
  grep "^## ERR-" knowledge/errors-registry.md | tail -10 | sed 's/^/   /'
else
  echo "   (No errors yet)"
fi

echo ""
echo "3. Recent Lessons (last 10):"
if [ -f "knowledge/lessons-learned.md" ]; then
  grep "^- LESSON-" knowledge/lessons-learned.md | tail -10 | sed 's/^/   /'
else
  echo "   (No lessons yet)"
fi

# ====================
# 8. Next Steps
# ====================
echo ""
echo "=========================================="
echo "✅ Feature Setup Complete!"
echo "=========================================="
echo ""
echo "📂 Feature: $FEATURE_DIR"
echo ""
echo "📝 NEXT STEPS:"
echo ""
echo "1️⃣  BRAINSTORMING (with AI - ChatGPT/Claude):"
echo "   - Use: docs/brainstorming-output-template.md as guide"
echo "   - Fill: docs/trading-system-docs/$FEATURE_DIR/00-DESIGN.md"
echo "   - Create task files: .claude/agents/$FEATURE_DIR/T-01.md, T-02.md, ..."
echo ""
echo "2️⃣  IMPLEMENTATION:"
echo "   - Run orchestrator: ./scripts/run-agents.sh $FEATURE_DIR 0 N"
echo "   - OR run tasks manually one-by-one"
echo ""
echo "3️⃣  DURING EXECUTION:"
echo "   - Update knowledge/errors-registry.md (new errors discovered)"
echo "   - Update knowledge/lessons-learned.md (new insights)"
echo ""
echo "4️⃣  AFTER ALL TASKS COMPLETE:"
echo "   - Run: ./scripts/sync-kb-to-rules.sh"
echo "   - This syncs discoveries → .claude/rules/ for next feature"
echo ""
echo "5️⃣  COMMIT & DEPLOY:"
echo "   - Review: ./scripts/pre-deployment-checklist.sh"
echo "   - Commit changes"
echo "   - Deploy as per DEPLOYMENT.md"
echo ""

if [ "$CLAUDE_MEM_AVAILABLE" = true ]; then
  echo "💡 claude-mem TIP:"
  echo "   During tasks, use: /mem-search \"$FEATURE_SLUG\""
  echo ""
fi

echo "=========================================="
echo ""
