param(
 [string]$Server = '(localdb)\MSSQLLocalDB',
 [string]$Database = 'StudentProjectsDev'
)
$ErrorActionPreference = 'Stop'
if ($Database -cne 'StudentProjectsDev') { throw 'This patch is limited to StudentProjectsDev.' }
$sqlPath = Join-Path $PSScriptRoot 'upgrade-project-progress-v07.sql'
if (-not (Test-Path $sqlPath)) { throw "Missing SQL: $sqlPath" }
Add-Type -AssemblyName System.Data
$connection = New-Object System.Data.SqlClient.SqlConnection("Data Source=$Server;Initial Catalog=$Database;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=15;")
try {
  $connection.Open()
  $cmd = $connection.CreateCommand()
  $cmd.CommandText = [System.IO.File]::ReadAllText($sqlPath)
  $cmd.CommandTimeout = 120
  $reader = $cmd.ExecuteReader()
  try {
    if ($reader.Read()) {
      Write-Host ('{0} on {1}' -f $reader['Result'], $reader['DatabaseName'])
      foreach ($col in @('Milestones','ProgressUpdates','MilestoneSubmissions','ProjectEvaluations')) {
        Write-Host ('  {0}: {1}' -f $col, $reader[$col])
      }
    }
  } finally { $reader.Dispose() }
} finally { $connection.Dispose() }
