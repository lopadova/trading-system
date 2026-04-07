#!/usr/bin/env bash
# Resetta un task a "pending" per ri-eseguirlo
# Uso: ./scripts/reset-task.sh T-05
TASK_ID="$1"
[ -z "$TASK_ID" ] && echo "Usage: $0 T-XX" && exit 1
python3 -c "
import json
with open('.agent-state.json') as f: s = json.load(f)
s['$TASK_ID'] = 'pending'
with open('.agent-state.json', 'w') as f: json.dump(s, f, indent=2)
print(f'Reset $TASK_ID to pending')
"
# Rinomina il report precedente se esiste
[ -f "logs/${TASK_ID}-result.md" ] && \
  mv "logs/${TASK_ID}-result.md" "logs/${TASK_ID}-result.$(date +%Y%m%d%H%M%S).bak.md" && \
  echo "Previous report archived"
