param([string]$Server='(localdb)\MSSQLLocalDB',[string]$Database='StudentProjectsDev')
$ErrorActionPreference='Stop'
if ($Database -cne 'StudentProjectsDev') { throw 'Only StudentProjectsDev is supported.' }
Add-Type -AssemblyName System.Data
$conn=New-Object System.Data.SqlClient.SqlConnection("Data Source=$Server;Initial Catalog=$Database;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=15;")
try {
  $conn.Open()
  $cmd=$conn.CreateCommand()
  $cmd.CommandText=@'
SELECT
  IIF(OBJECT_ID(N'dbo.Projects',N'U') IS NOT NULL, 1, 0) AS Projects,
  IIF(OBJECT_ID(N'dbo.Milestones',N'U') IS NOT NULL, 1, 0) AS Milestones,
  IIF(OBJECT_ID(N'dbo.Notifications',N'U') IS NOT NULL, 1, 0) AS Notifications,
  IIF(OBJECT_ID(N'dbo.GuidanceSessions',N'U') IS NOT NULL, 1, 0) AS GuidanceSessions,
  IIF(EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Notifications') AND name=N'UX_V08_Notifications_EventKey'),1,0) AS UniqueEvents;
'@
  $r=$cmd.ExecuteReader()
  try {
    if (-not $r.Read()) {throw 'Unable to read SQL schema status.'}
    foreach($name in @('Projects','Milestones','Notifications','GuidanceSessions','UniqueEvents')) {
      if ([int]$r[$name] -ne 1) { throw "v0.8 QA failed: missing $name" }
      Write-Host "PASS: $name"
    }
  }finally {$r.Dispose()}
}finally {$conn.Dispose()}
Write-Host 'PASS: v0.8 schema check. Continue API and permission tests via README.'
