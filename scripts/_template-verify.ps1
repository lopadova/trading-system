# Template per script di verifica task
# Da usare come base per verify-e2e.ps1, verify-task.ps1, etc.
#
# Uso: .\scripts\verify-XXX.ps1 [parametri]

param(
    [string]$TaskId = ""
)

$ErrorActionPreference = 'Stop'

# ============================================================
# CONFIGURAZIONE
# ============================================================

$TESTS_DIR = ".\tests"
$RESULTS_DIR = ".\logs"

# ============================================================
# FUNZIONI HELPER
# ============================================================

function Write-TestHeader {
    param([string]$Message)
    Write-Host ""
    Write-Host "=========================================="
    Write-Host $Message
    Write-Host "=========================================="
}

function Write-TestResult {
    param(
        [string]$TestId,
        [string]$Status,  # PASS, FAIL, SKIP
        [string]$Message = ""
    )

    $color = switch ($Status) {
        "PASS" { "Green" }
        "FAIL" { "Red" }
        "SKIP" { "Yellow" }
        default { "White" }
    }

    Write-Host "[${Status}] ${TestId}" -ForegroundColor $color
    if ($Message) {
        Write-Host "       $Message" -ForegroundColor Gray
    }
}

function Invoke-DotNetTest {
    param(
        [string]$TestFilter,
        [string]$TestId
    )

    try {
        $output = dotnet test --filter "FullyQualifiedName~$TestFilter" --logger "console;verbosity=minimal" 2>&1
        $exitCode = $LASTEXITCODE

        if ($exitCode -eq 0) {
            Write-TestResult -TestId $TestId -Status "PASS"
            return $true
        } else {
            Write-TestResult -TestId $TestId -Status "FAIL" -Message "Exit code: $exitCode"
            return $false
        }
    }
    catch {
        Write-TestResult -TestId $TestId -Status "FAIL" -Message $_.Exception.Message
        return $false
    }
}

# ============================================================
# LOGICA PRINCIPALE
# ============================================================

Write-TestHeader "Test Verification Script"

# Esempio di verifica di un test
# $result = Invoke-DotNetTest -TestFilter "MyTest" -TestId "TEST-01-01"

# Esempio di check di file
# if (Test-Path "src\MyFile.cs") {
#     Write-TestResult -TestId "FILE-CHECK-01" -Status "PASS"
# } else {
#     Write-TestResult -TestId "FILE-CHECK-01" -Status "FAIL" -Message "File not found"
# }

Write-Host ""
Write-Host "Verification completed."
