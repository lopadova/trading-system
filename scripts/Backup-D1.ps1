<#
.SYNOPSIS
  Exports a Cloudflare D1 database to a timestamped SQL file and optionally
  uploads it to Cloudflare R2.

.DESCRIPTION
  Windows twin of scripts/backup-d1.sh. Safe for unattended daily execution
  via Task Scheduler. Writes a new timestamp-named file on every run and
  never overwrites an existing backup.

.PARAMETER DatabaseName
  The D1 database to export. Allowed values: trading-db, trading-db-staging.

.PARAMETER OutputDir
  Optional output directory. Default: <repo>\backups\d1.

.PARAMETER R2Bucket
  Optional R2 bucket name. If provided (or if $env:R2_BUCKET is set), the
  export is also uploaded to:
    d1/<database>/<YYYY>/<MM>/<database>_<YYYY-MM-DDTHHMM>.sql

.EXAMPLE
  .\scripts\Backup-D1.ps1 -DatabaseName trading-db

.EXAMPLE
  $env:R2_BUCKET = "trading-system-backups"
  .\scripts\Backup-D1.ps1 -DatabaseName trading-db

.NOTES
  Requires bunx on PATH. Scheduled-task registration lives in docs/ops/DR.md.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('trading-db', 'trading-db-staging')]
    [string]$DatabaseName,

    [Parameter(Mandatory = $false)]
    [string]$OutputDir,

    [Parameter(Mandatory = $false)]
    [string]$R2Bucket
)

# Negative-first: bail if bunx missing.
if (-not (Get-Command bunx -ErrorAction SilentlyContinue)) {
    Write-Error "bunx not found on PATH. Install Bun: https://bun.sh"
    exit 1
}

# Resolve repo root relative to this script.
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = (Resolve-Path (Join-Path $ScriptDir "..")).Path
$WorkerDir = Join-Path $RepoRoot "infra\cloudflare\worker"

# Output directory default.
if (-not $OutputDir) {
    $OutputDir = Join-Path $RepoRoot "backups\d1"
}
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

# R2 bucket from either param or env var.
if (-not $R2Bucket) {
    $R2Bucket = $env:R2_BUCKET
}

# Timestamp in UTC so backups are ordered deterministically across machines.
$Timestamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHHmm")
$FileName = "${DatabaseName}_${Timestamp}.sql"
$OutputPath = Join-Path $OutputDir $FileName

Write-Host "=== D1 backup ==="
Write-Host "Database: $DatabaseName"
Write-Host "Output:   $OutputPath"
Write-Host ""

# Run the export. --remote required — local would produce an empty file.
Push-Location $WorkerDir
try {
    & bunx wrangler d1 export $DatabaseName --remote --output="$OutputPath"
    if ($LASTEXITCODE -ne 0) {
        Write-Error "wrangler d1 export failed with exit code $LASTEXITCODE"
        exit 2
    }
}
finally {
    Pop-Location
}

# Sanity check — empty SQL file = silent failure.
if (-not (Test-Path $OutputPath)) {
    Write-Error "Export file missing after wrangler reported success: $OutputPath"
    exit 2
}
$FileSize = (Get-Item $OutputPath).Length
if ($FileSize -eq 0) {
    Write-Error "Export file is empty: $OutputPath"
    exit 2
}
if ($FileSize -lt 1024) {
    Write-Warning "Export file is suspiciously small ($FileSize bytes). Inspect before trusting as a restore source."
}

Write-Host "Export OK -- $FileSize bytes."

# Optional R2 upload.
if ($R2Bucket) {
    $Year = (Get-Date).ToUniversalTime().ToString("yyyy")
    $Month = (Get-Date).ToUniversalTime().ToString("MM")
    $R2Key = "d1/$DatabaseName/$Year/$Month/$FileName"

    Write-Host ""
    Write-Host "Uploading to R2: $R2Bucket/$R2Key"

    Push-Location $WorkerDir
    try {
        & bunx wrangler r2 object put "$R2Bucket/$R2Key" --file="$OutputPath" --remote
        if ($LASTEXITCODE -ne 0) {
            Write-Error "R2 upload failed with exit code $LASTEXITCODE (local file preserved at $OutputPath)"
            exit 3
        }
        Write-Host "R2 upload OK."
    }
    finally {
        Pop-Location
    }
}

Write-Host ""
Write-Host "Done."
Write-Host "  Local:  $OutputPath"
if ($R2Bucket) {
    Write-Host "  Remote: r2://$R2Bucket/$R2Key"
}
