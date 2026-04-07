#!/usr/bin/env bash
# Mostra stato della knowledge base accumulata dagli agenti
echo "=== KNOWLEDGE BASE STATUS ==="
echo ""
echo "Errors registry:"
grep -c '^## ERR-' knowledge/errors-registry.md 2>/dev/null \
  | xargs -I{} echo "  {} errori registrati"

echo "Lessons learned:"
grep -c '^## LL-' knowledge/lessons-learned.md 2>/dev/null \
  | xargs -I{} echo "  {} lezioni"

echo "Skill updates:"
grep -c '^## 20' knowledge/skill-changelog.md 2>/dev/null \
  | xargs -I{} echo "  {} aggiornamenti skill"

echo "Task corrections:"
grep -c '^## CORR-' knowledge/task-corrections.md 2>/dev/null \
  | xargs -I{} echo "  {} correzioni"

echo ""
echo "=== AGENT STATE ==="
python3 -c "
import json
with open('.agent-state.json') as f: s = json.load(f)
for status in ['done', 'running', 'failed', 'pending']:
    tasks = [k for k,v in s.items() if v == status]
    if tasks: print(f'  {status.upper()}: {len(tasks)} — {\" \".join(tasks)}')
" 2>/dev/null || echo "  (no state file)"
