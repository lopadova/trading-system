---
title: "Guida Bash → PowerShell"
tags: ["knowledge-base", "dev", "reference"]
aliases: ["Bash to PowerShell"]
status: current
audience: ["ai-agent", "developer"]
last-reviewed: "2026-04-21"
---

# Guida Bash → PowerShell — Per Agenti

> Riferimento rapido per convertire script bash in PowerShell su Windows.

---

## Pattern Comuni

### Shebang e Error Handling

```bash
#!/usr/bin/env bash
set -euo pipefail
```

```powershell
$ErrorActionPreference = 'Stop'
```

---

### Variabili e Parametri

```bash
# Variabili
VAR="value"
VAR="${OTHER_VAR:-default}"

# Parametri posizionali
PARAM1="$1"
PARAM2="$2"

# Parsing argomenti
while [[ $# -gt 0 ]]; do
  case $1 in
    --flag) FLAG="$2"; shift 2 ;;
    *) echo "Unknown: $1"; exit 1 ;;
  esac
done
```

```powershell
# Variabili
$VAR = "value"
$VAR = if ($env:OTHER_VAR) { $env:OTHER_VAR } else { "default" }

# Parametri nominali
param(
    [string]$Param1 = "",
    [string]$Param2 = "",
    [switch]$Flag
)

# Accesso a parametri posizionali (se necessario)
$Param1 = $args[0]
$Param2 = $args[1]
```

---

### File System

```bash
# Check esistenza
[ -f "file.txt" ]        # file esiste
[ -d "dir" ]             # directory esiste
[ ! -f "file.txt" ]      # file NON esiste

# Crea directory
mkdir -p "path/to/dir"

# Leggi file
content=$(cat file.txt)

# Scrivi file
echo "text" > file.txt
printf "text\n" > file.txt

# Move/copy
mv file.txt backup.txt
cp file.txt backup.txt

# Remove
rm file.txt
rm -rf dir/
```

```powershell
# Check esistenza
Test-Path "file.txt"               # file/dir esiste
Test-Path "file.txt" -PathType Leaf    # solo file
Test-Path "dir" -PathType Container    # solo directory
-not (Test-Path "file.txt")           # NON esiste

# Crea directory
New-Item -ItemType Directory -Force -Path "path\to\dir" | Out-Null

# Leggi file
$content = Get-Content "file.txt" -Raw
$content = Get-Content "file.txt" -Raw -Encoding UTF8

# Scrivi file
"text" | Set-Content "file.txt"
Set-Content -Path "file.txt" -Value "text" -Encoding UTF8

# Move/copy
Move-Item "file.txt" "backup.txt" -Force
Copy-Item "file.txt" "backup.txt" -Force

# Remove
Remove-Item "file.txt" -Force
Remove-Item "dir\" -Recurse -Force
```

---

### Stringhe e Path

```bash
# Estrai numero da stringa
TASK_NUM="${TASK_ID#T-}"        # T-05 → 05

# Path join
FILE="$DIR/$NAME.txt"

# Basename/dirname
basename /path/to/file.txt      # file.txt
dirname /path/to/file.txt       # /path/to
```

```powershell
# Estrai numero da stringa
$TASK_NUM = $TASK_ID -replace 'T-', ''

# Path join
$FILE = Join-Path $DIR "$NAME.txt"

# Basename/dirname
Split-Path "C:\path\to\file.txt" -Leaf        # file.txt
Split-Path "C:\path\to\file.txt" -Parent      # C:\path\to
```

---

### Data e Timestamp

```bash
TIMESTAMP=$(date +%Y%m%d-%H%M%S)
```

```powershell
$TIMESTAMP = Get-Date -Format "yyyyMMdd-HHmmss"
```

---

### Grep e Pattern Matching

```bash
# Conta match
grep -c '^## ERR-' file.md

# Lista file con match
grep -l 'pattern' *.md

# Match con context
grep -C 2 'pattern' file.txt
```

```powershell
# Conta match
(Select-String -Path "file.md" -Pattern '^## ERR-' -AllMatches).Count

# Lista file con match
Get-ChildItem *.md | Select-String 'pattern' | Select-Object -Unique Path

# Match con context
Select-String -Path "file.txt" -Pattern 'pattern' -Context 2
```

---

### JSON Processing

```bash
# Python inline
python3 -c "
import json
with open('state.json') as f:
    state = json.load(f)
state['key'] = 'value'
with open('state.json', 'w') as f:
    json.dump(state, f, indent=2)
"
```

```powershell
# Native PowerShell
$state = Get-Content "state.json" -Raw | ConvertFrom-Json
$state.key = "value"
$state | ConvertTo-Json -Depth 10 | Set-Content "state.json" -Encoding UTF8
```

---

### Piping e Output

```bash
# Tee (stdout + file)
command 2>&1 | tee log.txt

# xargs
echo "value" | xargs -I{} echo "Result: {}"

# Redirect stderr + stdout
command 2>&1

# Silence output
command > /dev/null 2>&1
```

```powershell
# Tee (stdout + file)
command 2>&1 | Tee-Object -FilePath "log.txt"

# ForEach
"value" | ForEach-Object { "Result: $_" }

# Redirect stderr + stdout
command 2>&1

# Silence output
command | Out-Null
command > $null 2>&1
```

---

### Loops e Condizioni

```bash
# For loop
for i in {0..27}; do
  echo "T-$(printf '%02d' $i)"
done

# Array iteration
for item in "${array[@]}"; do
  echo "$item"
done

# If condition
if [ "$VAR" = "value" ]; then
  echo "match"
elif [ -z "$VAR" ]; then
  echo "empty"
else
  echo "other"
fi

# Null/empty check
[ -z "$VAR" ]       # empty
[ -n "$VAR" ]       # not empty
```

```powershell
# For loop
0..27 | ForEach-Object {
    $taskId = "T-{0:D2}" -f $_
    Write-Host $taskId
}

# Array iteration
foreach ($item in $array) {
    Write-Host $item
}

# If condition
if ($VAR -eq "value") {
    Write-Host "match"
} elseif ([string]::IsNullOrEmpty($VAR)) {
    Write-Host "empty"
} else {
    Write-Host "other"
}

# Null/empty check
[string]::IsNullOrEmpty($VAR)      # empty/null
-not [string]::IsNullOrEmpty($VAR) # not empty
```

---

### Exit Codes

```bash
# Check last exit code
if [ $? -eq 0 ]; then
  echo "success"
fi

# Exit with code
exit 1
```

```powershell
# Check last exit code
if ($LASTEXITCODE -eq 0) {
    Write-Host "success"
}

# Exit with code
exit 1
```

---

## Pattern Specifici Trading System

### Invoke Claude CLI

```bash
claude \
  --dangerously-skip-permissions \
  --model "$MODEL" \
  -p "$(cat .claude/prompts/file.md)" \
  2>&1 | tee "$LOG_FILE"
```

```powershell
$prompt = Get-Content ".claude\prompts\file.md" -Raw -Encoding UTF8

claude `
  --dangerously-skip-permissions `
  --model $MODEL `
  -p $prompt `
  2>&1 | Tee-Object -FilePath $LOG_FILE
```

### Update State File

```bash
python3 -c "
import json
with open('.agent-state.json') as f: s = json.load(f)
s['T-05'] = 'done'
with open('.agent-state.json', 'w') as f: json.dump(s, f, indent=2)
"
```

```powershell
$state = Get-Content ".agent-state.json" -Raw | ConvertFrom-Json
$state.'T-05' = "done"
$state | ConvertTo-Json -Depth 10 | Set-Content ".agent-state.json" -Encoding UTF8
```

---

## Checklist Conversione

Quando converti uno script bash in PowerShell:

- [ ] Sostituisci shebang con `$ErrorActionPreference = 'Stop'`
- [ ] Converti parametri posizionali in `param()` block
- [ ] Sostituisci `[ -f ]` con `Test-Path`
- [ ] Sostituisci `mkdir -p` con `New-Item -ItemType Directory -Force`
- [ ] Sostituisci `cat` con `Get-Content -Raw`
- [ ] Sostituisci `echo >` con `Set-Content`
- [ ] Sostituisci `grep -c` con `Select-String | Measure-Object`
- [ ] Sostituisci `tee` con `Tee-Object`
- [ ] Sostituisci `date +format` con `Get-Date -Format`
- [ ] Usa `-Encoding UTF8` per file di testo
- [ ] Converti path Unix (`/`) in path Windows (`\`) dove necessario
- [ ] Testa su PowerShell 5.1+ per compatibilità Windows 10/11

---

## Testing Script Convertito

```powershell
# Test syntax
powershell -NoProfile -Command "& { . .\script.ps1 }"

# Test esecuzione con parametri
.\script.ps1 -Param1 "value" -Flag

# Verifica encoding UTF-8
Get-Content .\output.txt -Encoding UTF8
```

---

*Ultima modifica: 2026-04-05*
