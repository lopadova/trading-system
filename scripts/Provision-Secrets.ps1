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
    # Invoke bunx via the Process API rather than PowerShell's native pipe
    # operator. The native pipe (`$value | & bunx ...`) terminates the stream
    # with CRLF on Windows, and wrangler stores the exact bytes it reads —
    # a trailing newline in the secret breaks downstream HMAC / bearer-auth
    # comparisons in subtle ways (the value "looks right" in the CF dashboard
    # but 401s at runtime). The bash script sidesteps this with `printf '%s'`;
    # the equivalent in PowerShell is manual stdin control via Process.
    $bunxCmd = Get-Command 'bunx' -ErrorAction SilentlyContinue
    if (-not $bunxCmd) {
        Write-Error "bunx not found in PATH. Install Bun (https://bun.sh) before running this script."
        exit 1
    }
    $psi = [System.Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = $bunxCmd.Source
    $psi.UseShellExecute = $false
    $psi.RedirectStandardInput = $true
    $psi.ArgumentList.Add('wrangler')
    $psi.ArgumentList.Add('secret')
    $psi.ArgumentList.Add('put')
    $psi.ArgumentList.Add($key)
    $psi.ArgumentList.Add('--config')
    $psi.ArgumentList.Add($wranglerConfig)
    foreach ($f in $envFlag) { $psi.ArgumentList.Add($f) }

    $proc = [System.Diagnostics.Process]::Start($psi)
    # Write exactly the secret bytes — no newline. Close() signals EOF to wrangler.
    $proc.StandardInput.Write($value)
    $proc.StandardInput.Close()
    $proc.WaitForExit()
    if ($proc.ExitCode -ne 0) {
        Write-Error "wrangler secret put failed for key '$key' with exit $($proc.ExitCode)"
        exit $proc.ExitCode
    }
    $pushed++
}

Write-Host ""
Write-Host "Done. Pushed $pushed secret(s); skipped $skipped placeholder(s)."
Write-Host "Verify with: bunx wrangler secret list $($envFlag -join ' ') --config $wranglerConfig"
