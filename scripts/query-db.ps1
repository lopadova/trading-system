# Quick SQLite query script for testing
param(
    [string]$Database = "src/TradingSupervisorService/data/supervisor.db",
    [string]$Query
)

Add-Type -Path "C:\Users\lopad\Documents\DocLore\Visual Basic\_NET\Applicazioni\trading-system\src\TradingSupervisorService\bin\Release\net10.0\Microsoft.Data.Sqlite.dll"

$connectionString = "Data Source=$Database"
$connection = New-Object Microsoft.Data.Sqlite.SqliteConnection($connectionString)
$connection.Open()

$command = $connection.CreateCommand()
$command.CommandText = $Query

$reader = $command.ExecuteReader()

# Print column names
$columns = @()
for ($i = 0; $i -lt $reader.FieldCount; $i++) {
    $columns += $reader.GetName($i)
}
Write-Host ($columns -join " | ")
Write-Host ("-" * 80)

# Print rows
while ($reader.Read()) {
    $row = @()
    for ($i = 0; $i -lt $reader.FieldCount; $i++) {
        $row += $reader.GetValue($i)
    }
    Write-Host ($row -join " | ")
}

$reader.Close()
$connection.Close()
