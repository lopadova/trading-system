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

.PARAMETER QuotesHours
  market_quotes_daily stale threshold in hours. Default 1.

.PARAMETER VixHours
  vix_term_structure stale threshold in hours. Default 1.

.PARAMETER BenchmarkDays
  benchmark_series stale threshold in days. Default 1.

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
    [int]$QuotesHours = 1,
    [int]$VixHours = 1,
    [int]$BenchmarkDays = 1
)

$ErrorActionPreference = 'Stop'

# Resolve paths from the script location so cwd doesn't matter.
$scriptDir = Split-Path -Parent $PSCommandPath
$repoRoot  = Resolve-Path (Join-Path $scriptDir '..')
$wranglerDir = Join-Path $repoRoot 'infra/cloudflare/worker'

if (-not (Test-Path (Join-Path $wranglerDir 'package.json'))) {
    Write-Error "Missing wrangler dir at $wranglerDir"
    exit 2
}

# Production uses the default Worker env; staging takes --env staging.
$wranglerEnvFlag = @()
if ($Environment -eq 'staging') {
    $wranglerEnvFlag = @('--env', 'staging')
}

# -----------------------------------------------------------------------------
# Run a scalar D1 query and return the first column of the first row as string.
# -----------------------------------------------------------------------------
function Invoke-D1Scalar {
    param(
        [Parameter(Mandatory)][string]$Sql
    )
    try {
        Push-Location $wranglerDir
        # --json returns [{results:[{...}]}]; we pull the first value out.
        $rawArgs = @('wrangler', 'd1', 'execute', 'ts-d1') + $wranglerEnvFlag + @(
            '--remote', '--json', '--command', $Sql
        )
        $raw = & npx @rawArgs 2>$null
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($raw)) {
            return $null
        }
        $parsed = $raw | ConvertFrom-Json
        $firstRow = $parsed[0].results[0]
        if ($null -eq $firstRow) { return $null }
        # Grab first property's value regardless of column name.
        $firstVal = ($firstRow.PSObject.Properties | Select-Object -First 1).Value
        if ($null -eq $firstVal) { return $null }
        return [string]$firstVal
    }
    catch {
        Write-Verbose "D1 query failed: $_"
        return $null
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
$latest = Invoke-D1Scalar -Sql "SELECT MAX(last_seen_at) FROM service_heartbeats"
if ($null -eq $latest) {
    Add-Row 'service_heartbeats' 'error' '<query failed>' "<$HeartbeatsMinutes min"
}
elseif ([string]::IsNullOrWhiteSpace($latest)) {
    Add-Row 'service_heartbeats' 'stale' '<empty table>' "<$HeartbeatsMinutes min"
}
elseif ($latest -lt $thresholdIso) {
    Add-Row 'service_heartbeats' 'stale' $latest ">$HeartbeatsMinutes min behind"
}
else {
    Add-Row 'service_heartbeats' 'fresh' $latest "within $HeartbeatsMinutes min"
}

# Reusable helpers for the daily-date checks. Returns $true when fresh.
function Test-DailyByDays {
    param(
        [string]$Table,
        [string]$Column,
        [int]$Days
    )
    $threshold = (Get-Date).ToUniversalTime().AddDays(-$Days).ToString("yyyy-MM-dd")
    $latest = Invoke-D1Scalar -Sql "SELECT MAX($Column) FROM $Table"
    if ($null -eq $latest) {
        Add-Row $Table 'error' '<query failed>' "<$Days day"
        return
    }
    if ([string]::IsNullOrWhiteSpace($latest)) {
        Add-Row $Table 'stale' '<empty table>' "<$Days day"
        return
    }
    if ($latest -lt $threshold) {
        Add-Row $Table 'stale' $latest ">$Days day(s) behind"
    }
    else {
        Add-Row $Table 'fresh' $latest "within $Days day(s)"
    }
}

function Test-DailyByHours {
    param(
        [string]$Table,
        [string]$Column,
        [int]$Hours
    )
    $today = (Get-Date).ToUniversalTime().ToString("yyyy-MM-dd")
    $yesterday = (Get-Date).ToUniversalTime().AddDays(-1).ToString("yyyy-MM-dd")
    $latest = Invoke-D1Scalar -Sql "SELECT MAX($Column) FROM $Table"
    if ($null -eq $latest) {
        Add-Row $Table 'error' '<query failed>' "<$Hours hours"
        return
    }
    if ([string]::IsNullOrWhiteSpace($latest)) {
        Add-Row $Table 'stale' '<empty table>' "<$Hours hours"
        return
    }
    if ($latest -eq $today -or $latest -eq $yesterday) {
        Add-Row $Table 'fresh' $latest "<= $Hours hours (date granularity)"
    }
    else {
        Add-Row $Table 'stale' $latest ">$Hours hours behind"
    }
}

Test-DailyByDays  -Table 'account_equity_daily' -Column 'date' -Days  $EquityDays
Test-DailyByHours -Table 'market_quotes_daily'  -Column 'date' -Hours $QuotesHours
Test-DailyByHours -Table 'vix_term_structure'   -Column 'date' -Hours $VixHours
Test-DailyByDays  -Table 'benchmark_series'     -Column 'date' -Days  $BenchmarkDays

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
