---
title: "Disaster Recovery (DR)"
tags: ["ops", "runbook", "incident-response", "safety"]
aliases: ["DR", "Disaster Recovery", "Disaster Recovery (DR)"]
status: current
audience: ["operator"]
phase: "phase-7"
last-reviewed: "2026-04-21"
related:
  - "[[RUNBOOK]]"
  - "[[GO_LIVE]]"
  - "[[SECRETS]]"
  - "[[RELEASE]]"
---

# Disaster Recovery (DR)

> Phase 7.7 deliverable. Real operator procedure for backing up and
> restoring the trading system. "Untested backup = no backup" — the DR
> drill in § 5 is mandatory every ~3 months.
>
> Audience: operator / on-call. Every command below is copy-pasteable.
> Every path exists (or is created by the procedure) so you can follow
> along on a real machine with no improvisation.
>
> Last updated: 2026-04-21.

---

## 1. What we protect and why

| Data                         | Where                                       | Loss impact                                      | RTO    | RPO    |
|------------------------------|---------------------------------------------|--------------------------------------------------|--------|--------|
| Cloudflare D1 (`trading-db`) | Cloudflare edge                             | Dashboard blind, order audit history gone        | 4h     | 24h    |
| SQLite — supervisor          | `data/supervisor.db` on Windows host        | Heartbeat / outbox / logs lost, replay impossible | 2h     | 24h    |
| SQLite — options-execution   | `data/options-execution.db` on Windows host | Local audit log + safety flags lost              | 2h     | 24h    |
| `appsettings.Production.json`| Windows Service host                        | DPAPI blobs, service cannot start                | 1h     | on-change |
| `secrets/.env.production`    | Local workstation (gitignored)              | Re-provision Cloudflare secrets from scratch     | 1h     | on-change |

**RTO** = Recovery Time Objective (how fast we're back up).
**RPO** = Recovery Point Objective (how much data we can lose).

Note: RPO 24h for D1 and SQLite is acceptable because the Outbox +
order audit is append-only and idempotent. A replay of the previous
day's IBKR-sourced data reconstructs 99%+ of the state; the
1-day-of-market-data gap is recoverable from IBKR historical endpoints
on demand.

---

## 2. D1 backup

### 2.1 Daily export

**Command** (manual or cron):

```bash
./scripts/backup-d1.sh trading-db
# Windows:
.\scripts\Backup-D1.ps1 -DatabaseName trading-db
```

This emits a timestamped SQL file to the chosen output directory:

```
backups/d1/trading-db_2026-04-21T0130.sql
```

If the environment variable `R2_BUCKET` is set, the script also
uploads the file to Cloudflare R2:

```bash
R2_BUCKET=trading-system-backups ./scripts/backup-d1.sh trading-db
```

The helper is idempotent — rerun it and it writes a new file, never
overwrites. 30-day retention is the operator's responsibility; the
scripts intentionally do NOT delete historical files.

### 2.2 Scheduling

The backup runs once per day (off-market hours to reduce read
contention). Recommended: 02:30 local time, which is 20:30 ET
(deep after-hours for US markets).

**Windows — register a Scheduled Task**:

```powershell
# Run AS ADMIN on the operator workstation (or service host)
$TaskName = "TradingSystem-DailyD1Backup"
$ScriptPath = "$PSScriptRoot\Backup-D1.ps1"
$Action = New-ScheduledTaskAction `
  -Execute "powershell.exe" `
  -Argument "-ExecutionPolicy Bypass -File `"$ScriptPath`" -DatabaseName trading-db" `
  -WorkingDirectory "C:\trading-system"
$Trigger = New-ScheduledTaskTrigger -Daily -At 02:30
$Settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -RunOnlyIfNetworkAvailable
$Principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest
Register-ScheduledTask -TaskName $TaskName -Action $Action -Trigger $Trigger -Settings $Settings -Principal $Principal
```

**Linux / macOS — crontab**:

```cron
30 2 * * * cd /path/to/trading-system && ./scripts/backup-d1.sh trading-db >> logs/backup-d1.log 2>&1
```

### 2.3 R2 destination

If using R2 (recommended — survives workstation failure):

```bash
# One-time setup (creates the bucket via wrangler)
bunx wrangler r2 bucket create trading-system-backups

# Then set the env var so backup-d1.sh auto-uploads
export R2_BUCKET=trading-system-backups
```

The script uses `bunx wrangler r2 object put` to upload each export
with the object key format:
`d1/trading-db/YYYY/MM/trading-db_YYYY-MM-DDTHHMM.sql`.

Retention policy for R2: no automatic lifecycle (Cloudflare R2 has
lifecycle rules as a Phase 8+ add-on). The operator manually trims
quarterly with:

```bash
bunx wrangler r2 object list trading-system-backups --prefix=d1/trading-db/2025/ \
  | jq -r '.objects[].key' | while read k; do
    bunx wrangler r2 object delete trading-system-backups "$k"
  done
```

### 2.4 Restore procedure

Restoring a D1 database DOES destroy the current contents. Never run
this against production without operator confirmation and a
`WORKER_PAUSED=true` safety flag.

```bash
# 1. Halt all ingest so writes aren't happening during restore.
#    Option A — scale Worker to maintenance mode (set KV flag).
bunx wrangler kv:key put --binding=KV "WORKER_PAUSED" "true" --remote

#    Option B (more drastic) — rollback Worker to a no-op deployment.

# 2. Download the export you want to restore FROM (if using R2).
bunx wrangler r2 object get trading-system-backups \
  "d1/trading-db/2026/04/trading-db_2026-04-20T0230.sql" \
  --file=./restore.sql

# 3. Wipe the existing D1 schema (safe because we're restoring).
#    IMPORTANT: a SELECT that GENERATES DROP statements doesn't actually
#    drop anything — we have to materialise the statements into a script
#    and execute it. Drop order matters: triggers → views → indexes →
#    tables (otherwise FK constraints or dependent objects would fail).
printf 'PRAGMA foreign_keys=OFF;\n' > ./wipe-schema.sql
bunx wrangler d1 execute trading-db --remote --json --command="
  SELECT
    CASE type
      WHEN 'trigger' THEN 'DROP TRIGGER IF EXISTS \"' || REPLACE(name, '\"', '\"\"') || '\";'
      WHEN 'view'    THEN 'DROP VIEW IF EXISTS \"'    || REPLACE(name, '\"', '\"\"') || '\";'
      WHEN 'index'   THEN 'DROP INDEX IF EXISTS \"'   || REPLACE(name, '\"', '\"\"') || '\";'
      WHEN 'table'   THEN 'DROP TABLE IF EXISTS \"'   || REPLACE(name, '\"', '\"\"') || '\";'
    END AS stmt
  FROM sqlite_master
  WHERE type IN ('table','view','trigger','index')
    AND name NOT LIKE 'sqlite_%'
  ORDER BY CASE type
    WHEN 'trigger' THEN 1
    WHEN 'view'    THEN 2
    WHEN 'index'   THEN 3
    WHEN 'table'   THEN 4
  END, name;
" | jq -r '.[0].results[].stmt' >> ./wipe-schema.sql
bunx wrangler d1 execute trading-db --remote --file=./wipe-schema.sql

# 4. Apply the restore SQL.
#    Wrangler does not accept multi-statement SQL from a file directly via
#    --command; use the --file flag added in wrangler 3.60+.
bunx wrangler d1 execute trading-db --remote --file=./restore.sql

# 5. Validate the restore with REAL per-table row counts.
#    The "SELECT COUNT(*) FROM sqlite_master WHERE tbl_name=m.name" trick
#    returns metadata counts (typically 1 per table), NOT row counts — a
#    broken restore would look fine. Build expected counts from the same
#    restore.sql in a LOCAL sqlite, then diff against what we just wrote
#    to remote D1. An empty diff == bit-exact restore.
rm -f ./restore-verify.sqlite ./expected-counts.txt ./actual-counts.txt
sqlite3 ./restore-verify.sqlite < ./restore.sql
#    5a. Expected counts (local): what the restore.sql claims to produce.
while IFS= read -r TABLE_NAME; do
  printf '%s\t' "${TABLE_NAME}" >> ./expected-counts.txt
  sqlite3 ./restore-verify.sqlite \
    "SELECT COUNT(*) FROM \"${TABLE_NAME}\";" >> ./expected-counts.txt
done < <(
  sqlite3 ./restore-verify.sqlite \
    "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name;"
)
#    5b. Actual counts (remote): what's actually in D1 now.
while IFS= read -r TABLE_NAME; do
  printf '%s\t' "${TABLE_NAME}" >> ./actual-counts.txt
  bunx wrangler d1 execute trading-db --remote --json \
    --command="SELECT COUNT(*) AS c FROM \"${TABLE_NAME}\";" \
    | jq -r '.[0].results[0].c' >> ./actual-counts.txt
done < <(
  bunx wrangler d1 execute trading-db --remote --json \
    --command="SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name;" \
    | jq -r '.[0].results[].name'
)
#    5c. The diff MUST be empty. Any line = broken restore.
diff -u ./expected-counts.txt ./actual-counts.txt
rm -f ./restore-verify.sqlite ./expected-counts.txt ./actual-counts.txt ./wipe-schema.sql

# 6. Re-enable ingest.
bunx wrangler kv:key delete --binding=KV "WORKER_PAUSED" --remote

# 7. Watch the first 5 minutes of ingest traffic to confirm normal operation.
```

After a restore, the supervisor's Outbox will redeliver any events
that were pending between the backup timestamp and now. Idempotency
on the ingest side (all tables use natural primary keys + INSERT OR
REPLACE) ensures this is safe.

---

## 3. SQLite backup (supervisor + options-execution)

### 3.1 Nightly Robocopy

SQLite databases are file-based. We use Robocopy (built into Windows)
to mirror the `data/` directory to a NAS share. Robocopy is
incremental and handles partial copies cleanly — a `.db-wal` file
that gets rotated mid-copy is re-copied on the next run.

**Target**: `\\NAS\backups\trading-system\$HOSTNAME\YYYY-MM-DD\`

**Script** (operator installs once, runs nightly):

```powershell
# scripts/Backup-Sqlite.ps1 (to be created if not present)
param(
    [string]$NasRoot = "\\NAS\backups\trading-system",
    [string]$SourceDir = "C:\trading-system\data",
    [int]$RetentionDays = 30
)

$Today = Get-Date -Format "yyyy-MM-dd"
$Dest = Join-Path $NasRoot "$env:COMPUTERNAME\$Today"
New-Item -ItemType Directory -Force -Path $Dest | Out-Null

# /MIR mirror source to dest, /Z resumable, /R:3 retries, /W:5 wait secs
# Exclude hot wal files mid-write: we back up .db only; SQLite's journaling
# ensures the .db snapshot is consistent thanks to WAL rollover.
robocopy $SourceDir $Dest `
  /MIR /Z /R:3 /W:5 /NFL /NDL /NP `
  /XF "*.db-shm" "*.db-journal"

# Prune older retention
Get-ChildItem "$NasRoot\$env:COMPUTERNAME" -Directory |
  Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-$RetentionDays) } |
  Remove-Item -Recurse -Force -ErrorAction Continue
```

### 3.2 Register as a Scheduled Task

```powershell
# AS ADMIN on the .NET service host
$TaskName = "TradingSystem-NightlySqliteBackup"
$ScriptPath = "C:\trading-system\scripts\Backup-Sqlite.ps1"
$Action = New-ScheduledTaskAction `
  -Execute "powershell.exe" `
  -Argument "-ExecutionPolicy Bypass -File `"$ScriptPath`""
$Trigger = New-ScheduledTaskTrigger -Daily -At 03:00
$Settings = New-ScheduledTaskSettingsSet -StartWhenAvailable
$Principal = New-ScheduledTaskPrincipal `
  -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest
Register-ScheduledTask -TaskName $TaskName -Action $Action `
  -Trigger $Trigger -Settings $Settings -Principal $Principal
```

### 3.3 Restore SQLite

```powershell
# 1. Stop the affected service.
Stop-Service TradingSupervisorService
Stop-Service OptionsExecutionService

# 2. Move the current (bad) DB aside. Never overwrite — keep it for
#    forensics.
$ts = Get-Date -Format "yyyyMMddHHmm"
Move-Item "C:\trading-system\data\supervisor.db" `
          "C:\trading-system\data\supervisor.corrupted.$ts.db"

# 3. Copy the most recent good backup into place.
Copy-Item "\\NAS\backups\trading-system\$env:COMPUTERNAME\2026-04-20\supervisor.db" `
          "C:\trading-system\data\supervisor.db"

# 4. Restart the service. SQLite will create a fresh WAL on first open.
Start-Service TradingSupervisorService

# 5. Tail the log for "database opened" + first successful heartbeat.
Get-Content "C:\trading-system\logs\supervisor-*.log" -Tail 50 -Wait
```

After restore, the Outbox pick up where it left off (any pending
rows retry on schedule). No data loss between the backup snapshot
and restart is avoidable — that's the RPO window.

---

## 4. Config + secrets backup

### 4.1 `appsettings.Production.json`

- **Where**: on the service host, in the service binary directory
  (e.g. `C:\Program Files\TradingSupervisorService\`).
- **Why precious**: contains the DPAPI-wrapped secret blobs. DPAPI
  keys are machine-bound; if the host is re-imaged, these blobs
  can NOT be decrypted on the new machine — you must re-wrap each
  value on the new box. See `docs/ops/SECRETS.md` § Recovery.
- **Backup**: included in the nightly Robocopy (step 3.1) — copy
  the entire binary directory, not just `data/`. Alternative: keep
  a dated copy in the password manager after every change.
- **Restore**: ONLY useful on the same machine. On a new machine,
  re-wrap all secrets from scratch.

### 4.2 `secrets/.env.production`

- **Where**: operator's local workstation, gitignored, **never**
  on the service host.
- **Why precious**: source-of-truth cleartext secrets; used by
  `scripts/provision-secrets.sh` to re-push to Cloudflare.
- **Backup**: store in the operator's password manager (1Password,
  Bitwarden). The file itself is NOT synced to any cloud — too risky.
- **Restore**: paste values from the password manager into a fresh
  `.env.production` file; run `scripts/provision-secrets.sh
  production` to re-push to Cloudflare.

---

## 5. Recovery procedures end-to-end

### 5.1 "I lost the D1 database" (full loss)

1. Pause Worker ingest (§ 2.4 step 1).
2. Download latest R2 export (§ 2.4 step 2).
3. Apply migrations to an empty D1 (if the database itself is gone,
   recreate with `bunx wrangler d1 create trading-db` and update
   `wrangler.toml` with the new id, then apply `migrations/*.sql` in
   order).
4. Apply the restore SQL (§ 2.4 step 4).
5. Un-pause Worker (§ 2.4 step 6).
6. Supervisor Outbox redelivers any events in the RPO window —
   verify with:
   ```sql
   SELECT event_type, COUNT(*), MAX(created_at)
   FROM sync_outbox WHERE status = 'sent'
     AND sent_at > datetime('now', '-30 minutes')
   GROUP BY event_type;
   ```

### 5.2 "Supervisor SQLite corrupted"

1. Stop both services (§ 3.3 step 1).
2. Move corrupted file aside (§ 3.3 step 2).
3. Restore from NAS (§ 3.3 step 3).
4. Restart supervisor FIRST (outbox drains to Worker).
5. Verify outbox is catching up:
   ```sql
   SELECT status, COUNT(*) FROM sync_outbox GROUP BY status;
   ```
   `pending` count should shrink toward 0.
6. Start OptionsExecutionService.

### 5.3 "Lost IBKR market data subscription state"

Not a DR concern — the supervisor reconnects on restart and
re-subscribes to all active symbols at startup. No backup needed.
The only state this affects is the in-memory cache; it repopulates
within 30s of the first tick.

### 5.4 "Lost DPAPI key material (machine re-imaged)"

See `docs/ops/SECRETS.md` § Recovery → "Lost DPAPI blob". Short
version: re-wrap every secret with `EncryptConfigValue` on the new
box using the cleartext values from the operator's password
manager. Update `appsettings.Production.json` with the new blobs.
Restart services.

### 5.5 "Lost the entire service host"

Complete rebuild:

1. Provision a fresh Windows Server box (Phase 7 used a Hetzner
   Windows VPS as reference — see `docs/DEPLOYMENT_GUIDE.md`).
2. Install .NET 10 runtime.
3. Restore `data/` from NAS (step 3.3).
4. Clone the repo, build `dotnet publish` the two services.
5. Install services (`infra/windows/install-supervisor.ps1`,
   `install-options-engine.ps1`).
6. Re-wrap DPAPI secrets on the new box (§ 5.4).
7. Start services; verify Cloudflare ingest resumes
   (`X-Data-Source` header disappears from aggregate routes).
8. Re-install the backup scheduled tasks (§ 2.2, § 3.2).

Expected total time: ~3 hours for a prepared operator.

---

## 6. DR drill — quarterly mandatory

**When**: first Saturday of each quarter (Jan, Apr, Jul, Oct).
Calendar it.

**Why mandatory**: an untested backup is not a backup. The ONLY
way to know the backups work is to exercise the restore path.

**Drill procedure** (~90 minutes, staging environment):

1. **Snapshot current staging D1** REAL row counts by table.
   IMPORTANT: the obvious-looking
   `(SELECT COUNT(*) FROM sqlite_master WHERE tbl_name=m.name)` returns
   **metadata** counts (≈1 per table), NOT actual row counts — the
   drill diff would be meaningless. Emit one `COUNT(*)` per table
   instead:
   ```bash
   rm -f before.txt
   while IFS= read -r TABLE_NAME; do
     printf '%s\t' "${TABLE_NAME}" >> before.txt
     bunx wrangler d1 execute trading-db-staging --remote --json \
       --command="SELECT COUNT(*) AS c FROM \"${TABLE_NAME}\";" \
       | jq -r '.[0].results[0].c' >> before.txt
   done < <(
     bunx wrangler d1 execute trading-db-staging --remote --json \
       --command="SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name;" \
       | jq -r '.[0].results[].name'
   )
   ```

2. **Run the daily backup script** against staging:
   ```bash
   ./scripts/backup-d1.sh trading-db-staging
   ```
   Verify a file dropped into `backups/d1/`.

3. **Destroy** a non-critical staging table (pick one with a
   natural PK that's easy to spot-check, e.g. `web_vitals`):
   ```bash
   bunx wrangler d1 execute trading-db-staging --remote \
     --command="DROP TABLE web_vitals;"
   ```

4. **Restore** from the export produced in step 2:
   ```bash
   bunx wrangler d1 execute trading-db-staging --remote \
     --file=backups/d1/trading-db-staging_*.sql
   ```

5. **Re-count** the restored DB the same way (real per-table
   `COUNT(*)`, not sqlite_master metadata) and diff:
   ```bash
   rm -f after.txt
   while IFS= read -r TABLE_NAME; do
     printf '%s\t' "${TABLE_NAME}" >> after.txt
     bunx wrangler d1 execute trading-db-staging --remote --json \
       --command="SELECT COUNT(*) AS c FROM \"${TABLE_NAME}\";" \
       | jq -r '.[0].results[0].c' >> after.txt
   done < <(
     bunx wrangler d1 execute trading-db-staging --remote --json \
       --command="SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name;" \
       | jq -r '.[0].results[].name'
   )
   diff -u before.txt after.txt
   ```
   Expected: zero diff (and a non-empty before.txt — an all-zero
   diff with empty files is a bug, not a pass). Any difference = your
   backup missed data.

6. **Log the drill** in `docs/ops/dr-drills/YYYY-QX.md` — template:

   ```markdown
   # DR Drill — YYYY QX (YYYY-MM-DD)

   **Operator**: <name>
   **Duration**: NN min
   **Target**: staging

   ## Results
   - Backup file: `<path>`
   - Backup size: N MB
   - Restore time: N min
   - Row-count diff: zero / N rows missing
   - Issues: <any>

   ## Action items
   - [ ] <if any>
   ```

7. **If the drill failed** (non-zero diff, restore errored, script
   failed): this is a severity-1 issue. Open an incident issue in
   the repo tagged `dr-drill-failure` and fix before the next drill.
   Do NOT go live with an unrestorable backup.

---

## 7. Related docs

- `docs/ops/SECRETS.md` — secret rotation and DPAPI recovery.
- `docs/ops/RUNBOOK.md` — on-call playbooks referenced from here.
- `docs/ops/GO_LIVE.md` — precondition "DR drill executed within
  last 90 days".
- `scripts/backup-d1.sh` + `scripts/Backup-D1.ps1` — the helpers
  this doc references.
