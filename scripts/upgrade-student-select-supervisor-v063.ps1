param(
    [string]$Server = '(localdb)\MSSQLLocalDB',
    [string]$Database = 'StudentProjectsDev'
)
$ErrorActionPreference = 'Stop'
$sqlPath = Join-Path $PSScriptRoot 'upgrade-student-select-supervisor-v063.sql'
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
    $check.CommandText = "SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.LecturerRequests') AND name = N'CK_LecturerRequests_Status' AND definition LIKE '%OFFERED%'"
    if ([int]$check.ExecuteScalar() -ne 1) { throw 'OFFERED status constraint missing after upgrade.' }
    Write-Host 'OK: v0.6.3 supervisor selection and global capacity upgrade applied.'
} finally { $connection.Dispose() }
