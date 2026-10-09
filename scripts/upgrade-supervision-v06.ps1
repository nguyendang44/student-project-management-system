# Apply once or rerun safely against local SQL Server, not an in-memory test database.
param(
    [string]$Server = '(localdb)\MSSQLLocalDB',
    [string]$Database = 'StudentProjectsDev'
)
$ErrorActionPreference = 'Stop'
$sqlPath = Join-Path $PSScriptRoot 'upgrade-supervision-v06.sql'
if (-not (Test-Path -LiteralPath $sqlPath)) { throw "Missing $sqlPath" }
Add-Type -AssemblyName System.Data
# Use known-good SQL Client keywords; do NOT assign the unsupported .DataSource property.
$connectionString = "Data Source=$Server;Initial Catalog=$Database;Integrated Security=True;Connect Timeout=15;TrustServerCertificate=True;"
$connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
try {
    $connection.Open()
    $command = $connection.CreateCommand()
    $command.CommandTimeout = 90
    $command.CommandText = [System.IO.File]::ReadAllText($sqlPath)
    [void]$command.ExecuteNonQuery()
    foreach ($table in @('LecturerCapacities', 'LecturerRequests', 'Projects')) {
        $check = $connection.CreateCommand()
        $check.CommandText = 'SELECT CASE WHEN OBJECT_ID(@tableName, ''U'') IS NULL THEN 0 ELSE 1 END'
        [void]$check.Parameters.AddWithValue('@tableName', "dbo.$table")
        if ([int]$check.ExecuteScalar() -ne 1) { throw "Table $table was not created" }
        Write-Host "OK: $table"
    }
    Write-Host "v0.6 supervision schema ready in $Database on $Server."
} finally { $connection.Dispose() }
