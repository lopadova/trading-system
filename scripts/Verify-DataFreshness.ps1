<#
.SYNOPSIS
  PowerShell twin of scripts/verify-data-freshness.sh.

.DESCRIPTION
  Verifies that the 5 critical D1 tables receiving the Trading System's
  market-data ingest are not stale. Intended for a nightly CI run after
  the US market close, but can be invoked manually at any time.

.PARAMETER Environment
  Target Worker environment. 'production' or 'staging'.

.PARAMETER HeartbeatsMinutes
  service_heartbeats stale threshold in minutes. Default 2.

.PARAMETER EquityDays
  account_equity_daily stale threshold in days. Default 1.

.PARAMETER QuotesDays
  market_quotes_daily stale threshold in days. Default 1. The underlying
  column is a DATE (not a timestamp), so sub-day granularity is not
  expressible without a schema change.

.PARAMETER VixDays
  vix_term_structure stale threshold in days. Default 1. Same date-granularity
  note as -QuotesDays.

.PARAMETER BenchmarkDays
  benchmark_series stale threshold in days. Default 1.

.PARAMETER DbName
  D1 database name. Default: parsed from infra/cloudflare/worker/wrangler.toml
  for the given -Environment (production from the top-level block, staging
  from the [env.staging] block).

.EXAMPLE
  .\scripts\Verify-DataFreshness.ps1 -Environment staging

.EXAMPLE
  .\scripts\Verify-DataFreshness.ps1 -Environment production -HeartbeatsMinutes 5
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [ValidateSet('production', 'staging')]
    [string]$Environment = 'production',

    [int]$HeartbeatsMinutes = 2,
    [int]$EquityDays = 1,
    [int]$QuotesDays = 1,
    [int]$VixDays = 1,
    [int]$BenchmarkDays = 1,

    [string]$DbName = ''
)

$ErrorActionPreference = 'Stop'

# Resolve paths from the script location so cwd doesn't matter.
$scriptDir = Split-Path -Parent $PSCommandPath
$repoRoot  = Resolve-Path (Join-Path $scriptDir '..')
$wranglerDir = Join-Path $repoRoot 'infra/cloudflare/worker'
$wranglerConfig = Join-Path $wranglerDir 'wrangler.toml'

if (-not (Test-Path (Join-Path $wranglerDir 'package.json'))) {
    Write-Error "Missing wrangler dir at $wranglerDir"
    exit 2
}

# -----------------------------------------------------------------------------
# Resolve D1 database name — parse from wrangler.toml if -DbName was not given.
# Production sits under the top-level [[d1_databases]] block; staging sits
# under [env.staging.[[d1_databases]]] (or an equivalent section prefix).
# -----------------------------------------------------------------------------
if ([string]::IsNullOrWhiteSpace($DbName)) {
    if (-not (Test-Path $wranglerConfig)) {
        Write-Error "wrangler.toml not found at $wranglerConfig; pass -DbName explicitly."
        exit 2
    }
    $inStaging = $false
    foreach ($line in Get-Content -LiteralPath $wranglerConfig) {
        if ($Environment -eq 'staging') {
            # Match both [env.staging.vars] (single-bracket) and
            # [[env.staging.d1_databases]] (double-bracket array-of-tables).
            if ($line -match '^\[+env\.staging\.') { $inStaging = $true; continue }
            if ($line -match '^\[+' -and -not ($line -match '^\[+env\.staging\.')) { $inStaging = $false }
            if ($inStaging -and $line -match '^\s*database_name\s*=\s*"([^"]+)"') {
                $DbName = $Matches[1]; break
            }
        }
        else {
            # Production: first database_name BEFORE any [env.*] marker.
            if ($line -match '^\[+env\.') { break }
            if ($line -match '^\s*database_name\s*=\s*"([^"]+)"') {
                $DbName = $Matches[1]; break
            }
        }
    }
    if ([string]::IsNullOrWhiteSpace($DbName)) {
        Write-Error "Could not parse database_name for Environment='$Environment' from wrangler.toml. Pass -DbName explicitly."
        exit 2
    }
    Write-Host "Using D1 database: $DbName (parsed from wrangler.toml)"
}

# Production uses the default Worker env; staging takes --env staging.
$wranglerEnvFlag = @()
if ($Environment -eq 'staging') {
    $wranglerEnvFlag = @('--env', 'staging')
}

# -----------------------------------------------------------------------------
# Run a scalar D1 query and return the first column of the first row.
#
# Return-shape semantics (required so callers can distinguish empty table
# from command failure — the original one-$null-for-everything version
# misclassified empty-table rows as 'error' instead of 'stale'):
#   - [pscustomobject]@{ Ok=$true;  Value='<string>' }  — query returned a scalar value
#   - [pscustomobject]@{ Ok=$true;  Value=$null }       — query succeeded but no rows / NULL
#   - [pscustomobject]@{ Ok=$false; Value=$null }       — wrangler / JSON parse failure
# -----------------------------------------------------------------------------
function Invoke-D1Scalar {
    param(
        [Parameter(Mandatory)][string]$Sql
    )
    try {
        Push-Location $wranglerDir
        # --json returns [{results:[{...}]}]; we pull the first value out.
        $rawArgs = @('wrangler', 'd1', 'execute', $DbName) + $wranglerEnvFlag + @(
            '--remote', '--json', '--command', $Sql
        )
        $raw = & npx @rawArgs 2>$null
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($raw)) {
            return [pscustomobject]@{ Ok = $false; Value = $null }
        }
        $parsed = $raw | ConvertFrom-Json
        # Empty results array (no rows) — success with null value, treated as stale upstream.
        if ($null -eq $parsed -or $null -eq $parsed[0].results -or $parsed[0].results.Count -eq 0) {
            return [pscustomobject]@{ Ok = $true; Value = $null }
        }
        $firstRow = $parsed[0].results[0]
        if ($null -eq $firstRow) {
            return [pscustomobject]@{ Ok = $true; Value = $null }
        }
        # Grab first property's value regardless of column name. A MAX() over
        # an empty set returns SQL NULL which ConvertFrom-Json renders as $null.
        $firstProp = $firstRow.PSObject.Properties | Select-Object -First 1
        if ($null -eq $firstProp -or $null -eq $firstProp.Value) {
            return [pscustomobject]@{ Ok = $true; Value = $null }
        }
        return [pscustomobject]@{ Ok = $true; Value = [string]$firstProp.Value }
    }
    catch {
        Write-Verbose "D1 query failed: $_"
        return [pscustomobject]@{ Ok = $false; Value = $null }
    }
    finally {
        Pop-Location
    }
}

# -----------------------------------------------------------------------------
# Checks
# -----------------------------------------------------------------------------
class FreshnessRow {
    [string]$Table
    [string]$Status     # fresh | stale | error
    [string]$Latest
    [string]$Threshold
}

$results = New-Object 'System.Collections.Generic.List[FreshnessRow]'
$staleCount = 0
$errorCount = 0

function Add-Row {
    param([string]$Table, [string]$Status, [string]$Latest, [string]$Threshold)
    $row = [FreshnessRow]::new()
    $row.Table = $Table
    $row.Status = $Status
    $row.Latest = $Latest
    $row.Threshold = $Threshold
    $results.Add($row)
    if ($Status -eq 'stale')  { $script:staleCount++ }
    if ($Status -eq 'error')  { $script:errorCount++ }
}

# service_heartbeats — column is ISO-8601 timestamp
$thresholdIso = (Get-Date).ToUniversalTime().AddMinutes(-$HeartbeatsMinutes).ToString("yyyy-MM-ddTHH:mm:ssZ")
$r = Invoke-D1Scalar -Sql "SELECT MAX(last_seen_at) FROM service_heartbeats"
if (-not $r.Ok) {
    Add-Row 'service_heartbeats' 'error' '<query failed>' "<$HeartbeatsMinutes min"
}
elseif ([string]::IsNullOrWhiteSpace($r.Value)) {
    Add-Row 'service_heartbeats' 'stale' '<empty table>' "<$HeartbeatsMinutes min"
}
elseif ($r.Value -lt $thresholdIso) {
    Add-Row 'service_heartbeats' 'stale' $r.Value ">$HeartbeatsMinutes min behind"
}
else {
    Add-Row 'service_heartbeats' 'fresh' $r.Value "within $HeartbeatsMinutes min"
}

# Reusable helper for the daily-date checks (all 4 remaining tables).
function Test-DailyByDays {
    param(
        [string]$Table,
        [string]$Column,
        [int]$Days
    )
    $threshold = (Get-Date).ToUniversalTime().AddDays(-$Days).ToString("yyyy-MM-dd")
    $r = Invoke-D1Scalar -Sql "SELECT MAX($Column) FROM $Table"
    if (-not $r.Ok) {
        Add-Row $Table 'error' '<query failed>' "<$Days day"
        return
    }
    if ([string]::IsNullOrWhiteSpace($r.Value)) {
        Add-Row $Table 'stale' '<empty table>' "<$Days day"
        return
    }
    if ($r.Value -lt $threshold) {
        Add-Row $Table 'stale' $r.Value ">$Days day(s) behind"
    }
    else {
        Add-Row $Table 'fresh' $r.Value "within $Days day(s)"
    }
}

Test-DailyByDays -Table 'account_equity_daily' -Column 'date' -Days $EquityDays
Test-DailyByDays -Table 'market_quotes_daily'  -Column 'date' -Days $QuotesDays
Test-DailyByDays -Table 'vix_term_structure'   -Column 'date' -Days $VixDays
Test-DailyByDays -Table 'benchmark_series'     -Column 'date' -Days $BenchmarkDays

# -----------------------------------------------------------------------------
# Report
# -----------------------------------------------------------------------------
Write-Host ""
"{0,-24} {1,-7} {2,-26} {3,-s}" -f 'Table', 'Status', 'Latest', 'Threshold' | Write-Host
('-' * 86) | Write-Host
foreach ($row in $results) {
    "{0,-24} {1,-7} {2,-26} {3,-s}" -f $row.Table, $row.Status, $row.Latest, $row.Threshold | Write-Host
}
Write-Host ""

if ($errorCount -gt 0) {
    Write-Error "$errorCount check(s) failed to execute. Inspect wrangler logs."
    exit 2
}

if ($staleCount -gt 0) {
    Write-Error "$staleCount table(s) are out-of-date."
    exit 1
}

Write-Host "OK: all $($results.Count) tables are fresh." -ForegroundColor Green
exit 0
