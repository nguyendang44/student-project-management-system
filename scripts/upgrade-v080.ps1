param([string]$Server='(localdb)\MSSQLLocalDB',[string]$Database='StudentProjectsDev')
$ErrorActionPreference='Stop'
if ($Database -cne 'StudentProjectsDev') { throw 'Only StudentProjectsDev is supported.' }
Add-Type -AssemblyName System.Data
$conn=New-Object System.Data.SqlClient.SqlConnection("Data Source=$Server;Initial Catalog=$Database;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=15;")
try {
  $conn.Open()
  $command=$conn.CreateCommand()
  $command.CommandText=[System.IO.File]::ReadAllText((Join-Path $PSScriptRoot 'upgrade-v080.sql'))
  $command.CommandTimeout=120
  $reader=$command.ExecuteReader()
  try { if ($reader.Read()) { Write-Host ($reader['Result']) } }
  finally { $reader.Dispose() }
} finally { $conn.Dispose() }
