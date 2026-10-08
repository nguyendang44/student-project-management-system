# Preview by default. Use -Apply to delete historical non-ACCEPTED registrations.
# Does not touch registrations of other students or any ACCEPTED row.
param(
    [string]$Server = '(localdb)\MSSQLLocalDB',
    [string]$Database = 'StudentProjectsDev',
    [switch]$Apply
)
$ErrorActionPreference = 'Stop'
$sqlPath = Join-Path $PSScriptRoot 'cleanup-existing-topic-registration-history.sql'
if (-not (Test-Path -LiteralPath $sqlPath)) {
    throw "Missing SQL script: $sqlPath"
}
$sql = [System.IO.File]::ReadAllText($sqlPath)
Add-Type -AssemblyName System.Data
# Deliberately use an ordinary connection string. SqlConnectionStringBuilder's
# PowerShell member assignment caused the previous 'DataSource' failure.
$connectionString = "Server=$Server;Database=$Database;Integrated Security=SSPI;TrustServerCertificate=True;Connect Timeout=15;"
$connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
try {
    $connection.Open()
    $command = $connection.CreateCommand()
    $command.CommandText = $sql
    $command.CommandTimeout = 120
    $parameter = $command.Parameters.Add('@Apply', [System.Data.SqlDbType]::Bit)
    $parameter.Value = [bool]$Apply
    $reader = $command.ExecuteReader()
    try {
        if (-not $reader.Read()) { throw 'SQL did not return a cleanup summary.' }
        Write-Host ('Mode: {0}' -f $reader['Mode'])
        Write-Host ('Students affected: {0}' -f $reader['AffectedStudents'])
        Write-Host ('Old registration rows found: {0}' -f $reader['OldRegistrationsFound'])
        Write-Host ('Registration rows deleted: {0}' -f $reader['DeletedRegistrations'])
        if (-not $Apply) { Write-Host 'Preview only. Nothing was deleted. Run again with -Apply after backup.' }
    } finally {
        $reader.Dispose()
    }
} finally {
    $connection.Dispose()
}
