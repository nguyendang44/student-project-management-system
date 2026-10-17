param(
    [string]$Server = '(localdb)\MSSQLLocalDB',
    [string]$Database = 'StudentProjectsDev'
)
$ErrorActionPreference = 'Stop'
if ($Database -cne 'StudentProjectsDev') { throw 'This patch only supports StudentProjectsDev.' }
$sqlPath = Join-Path $PSScriptRoot 'upgrade-student-milestone-proposal-v071.sql'
if (-not (Test-Path $sqlPath)) { throw "Missing SQL: $sqlPath" }
Add-Type -AssemblyName System.Data
$conn = New-Object System.Data.SqlClient.SqlConnection("Data Source=$Server;Initial Catalog=$Database;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=15;")
try {
    $conn.Open()
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = [System.IO.File]::ReadAllText($sqlPath)
    $cmd.CommandTimeout = 120
    $reader = $cmd.ExecuteReader()
    try {
        if ($reader.Read()) {
            Write-Host ('{0} | {1}' -f $reader['Result'], $reader['DatabaseName'])
            Write-Host ('Milestones: {0}; student proposals: {1}' -f $reader['MilestoneCount'], $reader['StudentProposedCount'])
        }
    } finally { $reader.Dispose() }
} finally { $conn.Dispose() }
