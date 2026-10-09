param(
    [string]$Server = '(localdb)\MSSQLLocalDB',
    [string]$Database = 'StudentProjectsDev'
)
$ErrorActionPreference = 'Stop'
$sqlPath = Join-Path $PSScriptRoot 'upgrade-joint-registration-v061.sql'
if (-not (Test-Path -LiteralPath $sqlPath)) { throw "Missing $sqlPath" }
Add-Type -AssemblyName System.Data
$connectionString = "Data Source=$Server;Initial Catalog=$Database;Integrated Security=True;Connect Timeout=15;TrustServerCertificate=True;"
$connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
try {
    $connection.Open()
    $command = $connection.CreateCommand()
    $command.CommandTimeout = 90
    $command.CommandText = [System.IO.File]::ReadAllText($sqlPath)
    [void]$command.ExecuteNonQuery()
    $check = $connection.CreateCommand()
    $check.CommandText = "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.LecturerRequests') AND name IN ('IsCombined','DraftTitle','DraftDescription','DraftObjective','DraftExpectedContent','DraftProposedTechnology')"
    if ([int]$check.ExecuteScalar() -ne 6) { throw 'v0.6.1 columns were not installed.' }
    Write-Host 'OK: v0.6.1 joint topic + lecturer workflow schema ready.'
} finally { $connection.Dispose() }
