# Windows PowerShell 5.1+ / SQL Server LocalDB.
# This script changes only topic-registration indexes; it does not delete data.
param(
    [string]$Server = '(localdb)\MSSQLLocalDB',
    [string]$Database = 'StudentProjectsDev'
)

$ErrorActionPreference = 'Stop'
$sqlPath = Join-Path $PSScriptRoot 'upgrade-multiple-pending-registrations.sql'
if (-not (Test-Path -LiteralPath $sqlPath)) {
    throw "Missing SQL script: $sqlPath"
}
$sql = [System.IO.File]::ReadAllText($sqlPath)

Add-Type -AssemblyName System.Data

# Use canonical SQL Server connection-string keywords. Avoid assigning
# DataSource as a PowerShell property (some environments treat it as an
# unsupported connection-string key called 'DataSource').
$connectionString = "Data Source=$Server;Initial Catalog=$Database;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=15;"
$conn = New-Object System.Data.SqlClient.SqlConnection($connectionString)
try {
    $conn.Open()
    if ($conn.Database -ne $Database) {
        throw "Unexpected database: $($conn.Database). Expected: $Database"
    }

    $cmd = $conn.CreateCommand()
    try {
        $cmd.CommandText = $sql
        $cmd.CommandTimeout = 60
        [void]$cmd.ExecuteNonQuery()
        Write-Host "OK: TopicRegistrations allows multiple PENDING topics per student in $Database."
        Write-Host 'Topic, registration, user, and audit records were not deleted by this script.'
    } finally {
        $cmd.Dispose()
    }
} finally {
    $conn.Dispose()
}
