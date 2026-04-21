<#
.SYNOPSIS
  PowerShell equivalent of scripts/provision-secrets.sh.

.DESCRIPTION
  Reads cleartext KEY=VALUE pairs from secrets/.env.<Environment>
  (gitignored) and pushes each to the Cloudflare Worker via
  `bunx wrangler secret put`.

.PARAMETER Environment
  Either 'production' or 'staging'. Anything else is rejected.

.EXAMPLE
  .\scripts\Provision-Secrets.ps1 -Environment staging
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet('production', 'staging')]
    [string]$Environment
)

$ErrorActionPreference = 'Stop'

# Resolve paths from the script location so cwd doesn't matter.
$scriptDir = Split-Path -Parent $PSCommandPath
$repoRoot = Resolve-Path (Join-Path $scriptDir '..')
$wranglerConfig = Join-Path $repoRoot 'infra/cloudflare/worker/wrangler.toml'
$envFile = Join-Path $repoRoot "secrets/.env.$Environment"

if (-not (Test-Path $envFile)) {
    Write-Error "Missing $envFile. See secrets/README.md for the template."
    exit 1
}
if (-not (Test-Path $wranglerConfig)) {
    Write-Error "Missing wrangler config at $wranglerConfig"
    exit 1
}

# Production uses the default Worker env; staging takes --env staging.
$envFlag = @()
if ($Environment -eq 'staging') {
    $envFlag = @('--env', 'staging')
}

Write-Host "Provisioning Cloudflare Worker secrets for env='$Environment' from $envFile..."

$pushed = 0
$skipped = 0
foreach ($rawLine in (Get-Content -LiteralPath $envFile)) {
    $line = $rawLine.Trim()
    # Skip comments and blanks.
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    if ($line.StartsWith('#')) { continue }

    # Split on the FIRST '=' only (values may legitimately contain '=').
    $eqIndex = $line.IndexOf('=')
    if ($eqIndex -lt 1) {
        Write-Warning "Skipping malformed line (no '='): $line"
        continue
    }
    $key = $line.Substring(0, $eqIndex).Trim()
    $value = $line.Substring($eqIndex + 1).Trim()

    if ([string]::IsNullOrEmpty($value) -or $value -like 'REPLACE_*') {
        Write-Host "  SKIP $key`: empty / placeholder value"
        $skipped++
        continue
    }

    Write-Host "  PUT  $key"
    # Pipe the value into wrangler via stdin. We use cmd /c to preserve exact
    # byte semantics (no trailing newline appended by Write-Output).
    $value | & bunx wrangler secret put $key --config $wranglerConfig @envFlag
    if ($LASTEXITCODE -ne 0) {
        Write-Error "wrangler secret put failed for key '$key' with exit $LASTEXITCODE"
        exit $LASTEXITCODE
    }
    $pushed++
}

Write-Host ""
Write-Host "Done. Pushed $pushed secret(s); skipped $skipped placeholder(s)."
Write-Host "Verify with: bunx wrangler secret list $($envFlag -join ' ') --config $wranglerConfig"
