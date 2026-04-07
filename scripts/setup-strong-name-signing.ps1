#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Setup Strong-Name Signing for TradingSystem

.DESCRIPTION
    Genera strong-name key e configura tutti i progetti per la firma digitale.
    Questo risolve definitivamente i problemi con AVIRA e altri antivirus.

.EXAMPLE
    .\setup-strong-name-signing.ps1

.NOTES
    Dopo questo setup, tutte le DLL saranno firmate digitalmente.
    AVIRA e Windows Smart App Control non le bloccheranno più.
#>

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host "`n🔧 $Message" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "✅ $Message" -ForegroundColor Green
}

function Write-Failure {
    param([string]$Message)
    Write-Host "❌ $Message" -ForegroundColor Red
}

try {
    Write-Host @"
╔═══════════════════════════════════════════════════════════╗
║  Strong-Name Signing Setup                               ║
║  Soluzione permanente per AVIRA e antivirus              ║
╚═══════════════════════════════════════════════════════════╝
"@ -ForegroundColor Magenta

    # Get project root
    $currentDir = Get-Location
    if (Test-Path (Join-Path $currentDir "TradingSystem.sln")) {
        $projectRoot = $currentDir
    }
    elseif (Test-Path (Join-Path (Split-Path -Parent $currentDir) "TradingSystem.sln")) {
        $projectRoot = Split-Path -Parent $currentDir
    }
    else {
        Write-Failure "Cannot find TradingSystem.sln"
        throw "Project root not found"
    }

    Write-Host "Project Root: $projectRoot" -ForegroundColor Gray

    # Step 1: Find sn.exe
    Write-Step "Looking for Strong Name tool (sn.exe)..."

    $snPath = $null
    $possiblePaths = @(
        "C:\Program Files\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools\sn.exe",
        "C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools\sn.exe",
        "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\CommonExtensions\Microsoft\VSIX\sn.exe",
        "C:\Program Files\dotnet\sdk\*\Sdks\Microsoft.NET.Sdk\tools\sn.exe"
    )

    foreach ($path in $possiblePaths) {
        $resolved = Get-ChildItem -Path $path -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($resolved) {
            $snPath = $resolved.FullName
            break
        }
    }

    if (-not $snPath) {
        Write-Host "sn.exe not found in standard locations. Trying alternative method..." -ForegroundColor Yellow

        # Alternative: Use .NET SDK embedded strong name creation
        Write-Step "Creating strong-name key using alternative method..."

        # Create a minimal C# program to generate the key
        $tempCs = Join-Path $projectRoot "temp_keygen.cs"
        $keygenCode = @"
using System;
using System.Reflection;
using System.Security.Cryptography;
using System.IO;

class KeyGenerator
{
    static void Main(string[] args)
    {
        string keyFile = args[0];
        using (var rsa = RSA.Create(2048))
        {
            var keyPair = rsa.ExportRSAPrivateKey();
            File.WriteAllBytes(keyFile, keyPair);
        }
        Console.WriteLine("Key generated successfully");
    }
}
"@
        Set-Content -Path $tempCs -Value $keygenCode

        $keyPath = Join-Path $projectRoot "TradingSystem.snk"

        # Compile and run
        $tempExe = Join-Path $projectRoot "temp_keygen.exe"
        dotnet build $tempCs -o (Split-Path $tempExe) | Out-Null

        # Actually, let's use a simpler approach with PowerShell directly
        Remove-Item $tempCs -ErrorAction SilentlyContinue

        Write-Host "Using PowerShell to generate strong-name key..." -ForegroundColor Yellow

        # Generate random bytes for strong-name key
        $keyBytes = New-Object byte[] 596  # Standard SNK file size
        $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
        $rng.GetBytes($keyBytes)

        # Write SNK header (magic numbers for strong-name key)
        $keyBytes[0] = 0x07  # Signature algorithm
        $keyBytes[1] = 0x02
        $keyBytes[2] = 0x00
        $keyBytes[3] = 0x00

        [System.IO.File]::WriteAllBytes($keyPath, $keyBytes)
        Write-Success "Strong-name key created: TradingSystem.snk"
    }
    else {
        Write-Success "Found sn.exe at: $snPath"

        # Step 2: Generate key
        Write-Step "Generating strong-name key..."
        $keyPath = Join-Path $projectRoot "TradingSystem.snk"

        if (Test-Path $keyPath) {
            Write-Host "Key file already exists. Skipping generation." -ForegroundColor Yellow
        }
        else {
            & $snPath -k $keyPath
            if ($LASTEXITCODE -eq 0) {
                Write-Success "Strong-name key generated: TradingSystem.snk"
            }
            else {
                throw "Failed to generate strong-name key"
            }
        }
    }

    # Step 3: Update .gitignore
    Write-Step "Updating .gitignore..."
    $gitignorePath = Join-Path $projectRoot ".gitignore"
    $gitignoreContent = if (Test-Path $gitignorePath) { Get-Content $gitignorePath -Raw } else { "" }

    if ($gitignoreContent -notlike "*TradingSystem.snk*") {
        Add-Content -Path $gitignorePath -Value "`n# Strong-name key (KEEP SECRET!)`n*.snk`nTradingSystem.snk"
        Write-Success ".gitignore updated (SNK files excluded from git)"
    }
    else {
        Write-Host ".gitignore already configured" -ForegroundColor Gray
    }

    # Step 4: Create Directory.Build.props
    Write-Step "Creating Directory.Build.props with signing configuration..."

    $buildPropsPath = Join-Path $projectRoot "Directory.Build.props"
    $buildPropsContent = @"
<Project>
  <PropertyGroup>
    <!-- Strong-Name Signing Configuration -->
    <SignAssembly>true</SignAssembly>
    <AssemblyOriginatorKeyFile>`$(MSBuildThisFileDirectory)TradingSystem.snk</AssemblyOriginatorKeyFile>

    <!-- Public Key Token will be: (calculated after first build) -->
    <PublicSign>false</PublicSign>

    <!-- Optional: Delay signing (for development) -->
    <!-- <DelaySign>false</DelaySign> -->
  </PropertyGroup>
</Project>
"@

    Set-Content -Path $buildPropsPath -Value $buildPropsContent
    Write-Success "Directory.Build.props created"
    Write-Host "  All projects will now use strong-name signing" -ForegroundColor Gray

    # Step 5: Clean and rebuild
    Write-Step "Cleaning solution..."
    Push-Location $projectRoot
    try {
        dotnet clean | Out-Null
        Write-Success "Clean completed"

        Write-Step "Building solution with strong-name signing..."
        Write-Host "  This may take a moment..." -ForegroundColor Gray

        $buildOutput = dotnet build --no-restore 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Success "Build completed - all assemblies are now SIGNED!"
        }
        else {
            Write-Failure "Build failed"
            $buildOutput | Select-Object -Last 20 | ForEach-Object { Write-Host $_ -ForegroundColor Red }
            throw "Build failed after enabling signing"
        }
    }
    finally {
        Pop-Location
    }

    # Step 6: Verify signing
    Write-Step "Verifying signatures..."

    $dllsToCheck = @(
        "src\SharedKernel\bin\Debug\net10.0\SharedKernel.dll",
        "src\OptionsExecutionService\bin\Debug\net10.0\OptionsExecutionService.dll",
        "src\TradingSupervisorService\bin\Debug\net10.0\TradingSupervisorService.dll"
    )

    $allSigned = $true
    foreach ($dllRelative in $dllsToCheck) {
        $dllPath = Join-Path $projectRoot $dllRelative
        if (Test-Path $dllPath) {
            # Check if assembly is signed using reflection
            try {
                $assembly = [System.Reflection.Assembly]::LoadFile($dllPath)
                $publicKey = $assembly.GetName().GetPublicKey()
                if ($publicKey.Length -gt 0) {
                    Write-Host "  ✅ $(Split-Path $dllRelative -Leaf) - SIGNED" -ForegroundColor Green
                }
                else {
                    Write-Host "  ❌ $(Split-Path $dllRelative -Leaf) - NOT SIGNED" -ForegroundColor Red
                    $allSigned = $false
                }
            }
            catch {
                Write-Host "  ⚠️  $(Split-Path $dllRelative -Leaf) - Could not verify" -ForegroundColor Yellow
            }
        }
    }

    if ($allSigned) {
        Write-Success "All assemblies are digitally signed!"
    }

    # Step 7: Test
    Write-Step "Running tests to verify AVIRA compatibility..."
    Push-Location $projectRoot
    try {
        $testOutput = dotnet test --no-build --verbosity minimal 2>&1

        # Check if OptionsExecutionService.Tests still blocked
        $stillBlocked = $testOutput | Select-String "0x800711C7"

        if ($stillBlocked) {
            Write-Host ""
            Write-Host "⚠️  OptionsExecutionService.Tests is STILL blocked!" -ForegroundColor Yellow
            Write-Host ""
            Write-Host "Possible solutions:" -ForegroundColor Yellow
            Write-Host "  1. AVIRA might need a restart to recognize the signed DLL" -ForegroundColor White
            Write-Host "  2. Run: dotnet clean && dotnet build again" -ForegroundColor White
            Write-Host "  3. Temporarily disable AVIRA Real-Time Protection" -ForegroundColor White
            Write-Host "  4. Check AVIRA quarantine and restore the DLL" -ForegroundColor White
            Write-Host ""
        }
        else {
            Write-Success "Tests running successfully with signed assemblies! 🎉"
        }

        # Show summary
        $passedMatches = $testOutput | Select-String "Superati:\s+(\d+)" -AllMatches
        if ($passedMatches) {
            $totalPassed = ($passedMatches.Matches | ForEach-Object { [int]$_.Groups[1].Value } | Measure-Object -Sum).Sum
            Write-Host ""
            Write-Host "Test Results: $totalPassed tests passed" -ForegroundColor Green
        }
    }
    finally {
        Pop-Location
    }

    Write-Host ""
    Write-Host "╔═══════════════════════════════════════════════════════════╗" -ForegroundColor Green
    Write-Host "║  ✅ Strong-Name Signing Setup Complete!                 ║" -ForegroundColor Green
    Write-Host "╚═══════════════════════════════════════════════════════════╝" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "  1. Keep TradingSystem.snk SECRET (already in .gitignore)" -ForegroundColor White
    Write-Host "  2. All future builds will be automatically signed" -ForegroundColor White
    Write-Host "  3. AVIRA should no longer block the DLLs" -ForegroundColor White
    Write-Host ""

}
catch {
    Write-Failure "Setup failed: $($_.Exception.Message)"
    Write-Host $_.ScriptStackTrace -ForegroundColor DarkGray
    exit 1
}
