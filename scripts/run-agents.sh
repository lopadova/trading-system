#!/usr/bin/env bash
# run-agents.sh - Orchestrator for multi-task execution
# Usage:
#   ./run-agents.sh <feature-dir>              # Auto-detect all tasks
#   ./run-agents.sh <feature-dir> 0 5          # Run T-00 to T-05
#   ./run-agents.sh                            # Auto-detect in .claude/agents/ (root)

set -euo pipefail

FEATURE_DIR=${1:-""}
START_TASK=${2:-}
END_TASK=${3:-}

# Determine agent directory (try docs/ first, fallback to .claude/agents/)
if [ -z "$FEATURE_DIR" ]; then
  AGENT_DIR=".claude/agents"
  FEATURE_NAME="root"
else
  # Try docs/trading-system-docs/feature-XXX/ first (preferred)
  if [ -d "docs/trading-system-docs/$FEATURE_DIR" ]; then
    AGENT_DIR="docs/trading-system-docs/$FEATURE_DIR"
    echo "📁 Using task files from: docs/ (preferred location)"
  # Fallback to .claude/agents/feature-XXX/
  elif [ -d ".claude/agents/$FEATURE_DIR" ]; then
    AGENT_DIR=".claude/agents/$FEATURE_DIR"
    echo "📁 Using task files from: .claude/agents/ (legacy location)"
  else
    echo "❌ Error: Feature directory not found"
    echo ""
    echo "Tried:"
    echo "  - docs/trading-system-docs/$FEATURE_DIR"
    echo "  - .claude/agents/$FEATURE_DIR"
    echo ""
    echo "Usage:"
    echo "  ./run-agents.sh <feature-dir>         # Auto-detect all tasks"
    echo "  ./run-agents.sh <feature-dir> 0 5     # Run T-00 to T-05"
    echo ""
    echo "Example:"
    echo "  ./run-agents.sh feature-202604-alerts"
    exit 1
  fi
  FEATURE_NAME="$FEATURE_DIR"
fi

# Auto-detect task range if not specified
if [ -z "$START_TASK" ] || [ -z "$END_TASK" ]; then
  echo "🔍 Auto-detecting tasks in $AGENT_DIR..."
  echo ""

  # Find all T-XX.md files and extract numbers
  TASK_FILES=$(find "$AGENT_DIR" -name "T-*.md" -type f 2>/dev/null | sort)

  if [ -z "$TASK_FILES" ]; then
    echo "❌ No task files found in $AGENT_DIR"
    echo ""
    echo "Expected: $AGENT_DIR/T-00.md, T-01.md, ..."
    exit 1
  fi

  # Extract min and max task numbers
  TASK_NUMBERS=$(echo "$TASK_FILES" | sed -n 's/.*\/T-\([0-9]\+\)[^0-9]*.*/\1/p' | sort -n | uniq)
  START_TASK=$(echo "$TASK_NUMBERS" | head -1)
  END_TASK=$(echo "$TASK_NUMBERS" | tail -1)

  TASK_COUNT=$(echo "$TASK_NUMBERS" | wc -l | tr -d ' ')

  echo "✅ Found $TASK_COUNT tasks (T-$(printf "%02d" $START_TASK) to T-$(printf "%02d" $END_TASK))"
  echo ""
fi

# Auto-populate .agent-state.json if empty or missing
if [ ! -f ".agent-state.json" ] || [ "$(cat .agent-state.json 2>/dev/null)" = "{}" ]; then
  echo "🔧 Populating .agent-state.json with detected tasks..."

  # Create JSON with all tasks set to "pending" (formatted)
  {
    echo "{"
    for i in $(seq $START_TASK $END_TASK); do
      TASK_NUM=$(printf "%02d" $i)
      if [ $i -lt $END_TASK ]; then
        echo "  \"T-$TASK_NUM\": \"pending\","
      else
        echo "  \"T-$TASK_NUM\": \"pending\""
      fi
    done
    echo "}"
  } > .agent-state.json

  echo "   ✅ Initialized .agent-state.json with $TASK_COUNT tasks"
  echo ""
fi

# Display header
echo "=========================================="
echo "🚀 Orchestrator Starting"
echo "=========================================="
echo "Feature: $FEATURE_NAME"
echo "Directory: $AGENT_DIR"
echo "Task range: T-$(printf "%02d" $START_TASK) to T-$(printf "%02d" $END_TASK)"
echo "=========================================="
echo ""

# Execute tasks sequentially
COMPLETED_COUNT=0
FAILED_COUNT=0

for i in $(seq $START_TASK $END_TASK); do
  TASK_NUM=$(printf "%02d" $i)
  TASK_FILE=$(find "$AGENT_DIR" -name "T-$TASK_NUM*.md" -type f 2>/dev/null | head -1)

  if [ ! -f "$TASK_FILE" ]; then
    echo "⚠️  Task T-$TASK_NUM not found in $AGENT_DIR, skipping"
    echo ""
    continue
  fi

  TASK_NAME=$(basename "$TASK_FILE" .md)

  echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
  echo "🚀 Task T-$TASK_NUM: $TASK_NAME"
  echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
  echo ""

  # Launch Claude with task context
  claude --file "$TASK_FILE" \
         --file "CLAUDE.md" \
         --file "knowledge/errors-registry.md" \
         --file "knowledge/lessons-learned.md" \
         "Execute this task. Use /mem-search if claude-mem is available."

  echo ""

  # Check if task succeeded
  if [ -f ".agent-state.json" ]; then
    TASK_STATE=$(jq -r ".\"T-$TASK_NUM\"" .agent-state.json 2>/dev/null || echo "unknown")

    if [ "$TASK_STATE" = "done" ]; then
      echo "✅ T-$TASK_NUM completed successfully"
      ((COMPLETED_COUNT++))
    else
      echo ""
      echo "❌ T-$TASK_NUM failed or not marked as done"
      echo "   State: $TASK_STATE"
      echo "   Check: logs/T-$TASK_NUM-result.md"
      echo ""
      echo "Stopping orchestrator."
      ((FAILED_COUNT++))
      exit 1
    fi
  else
    echo "⚠️  Warning: .agent-state.json not found"
    echo "   Task state unknown"
  fi

  echo ""
done

# Summary
echo "=========================================="
echo "✅ All Tasks Completed Successfully!"
echo "=========================================="
echo ""
echo "Summary:"
echo "  - Completed: $COMPLETED_COUNT tasks"
echo "  - Failed: $FAILED_COUNT tasks"
echo "  - Feature: $FEATURE_NAME"
echo ""

# ============================================
# AUTOMATIC SYNC: KB → Rules
# ============================================
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "🔄 Step: Sync Knowledge Base → Rules"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""
echo "This extracts critical discoveries and generates"
echo ".claude/rules/ files for auto-loading in next feature."
echo ""

if [ -f "scripts/sync-kb-to-rules.sh" ]; then
  ./scripts/sync-kb-to-rules.sh

  echo ""
  echo "✅ Knowledge base synchronized to rules"
  echo "   (.claude/rules/ updated for next feature)"
else
  echo "⚠️  scripts/sync-kb-to-rules.sh not found"
  echo "   Run manually: ./scripts/sync-kb-to-rules.sh"
fi

echo ""
echo "=========================================="
echo "🎉 Feature Implementation Complete!"
echo "=========================================="
echo ""
echo "📋 Next Steps:"
echo ""
echo "  1. Verify E2E:"
echo "     ./scripts/verify-e2e.sh"
echo ""
echo "  2. Pre-deployment checklist:"
echo "     ./scripts/pre-deployment-checklist.sh"
echo ""
echo "  3. Commit changes:"
echo "     git add ."
echo "     git commit -m \"feat: $FEATURE_NAME implementation\""
echo ""
echo "  4. Deploy (if applicable):"
echo "     See DEPLOYMENT.md"
echo ""
echo "=========================================="
echo ""
