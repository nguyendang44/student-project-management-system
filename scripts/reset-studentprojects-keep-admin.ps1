# StudentProjectsDev reset to Admin-only users + intact tables, Roles and EF migrations.
# Preview: powershell -ExecutionPolicy Bypass -File .\scripts\reset-studentprojects-keep-admin.ps1
# Execute: powershell -ExecutionPolicy Bypass -File .\scripts\reset-studentprojects-keep-admin.ps1 -Apply
param(
    [string]$Server = '(localdb)\MSSQLLocalDB',
    [string]$Database = 'StudentProjectsDev',
    [string]$BackupPath,
    [switch]$Apply
)
$ErrorActionPreference = 'Stop'
if ($Database -cne 'StudentProjectsDev') { throw 'Only StudentProjectsDev can be reset by this script.' }
$sqlPath = Join-Path $PSScriptRoot 'reset-studentprojects-keep-admin.sql'
if (-not (Test-Path -LiteralPath $sqlPath)) { throw "SQL file missing: $sqlPath" }
Add-Type -AssemblyName System.Data
$connectionString = "Data Source=$Server;Initial Catalog=$Database;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=15;"
$connection = New-Object System.Data.SqlClient.SqlConnection -ArgumentList $connectionString

function SqlScalar([string]$commandText) {
    $cmd = $connection.CreateCommand()
    $cmd.CommandText = $commandText
    return $cmd.ExecuteScalar()
}

function PreviewOrApply([bool]$commit) {
    $cmd = $connection.CreateCommand()
    $cmd.CommandText = [System.IO.File]::ReadAllText($sqlPath)
    $cmd.CommandTimeout = 180
    $p = $cmd.Parameters.Add('@Apply', [System.Data.SqlDbType]::Bit)
    $p.Value = $commit
    $reader = $cmd.ExecuteReader()
    try {
        $total = [long]0
        Write-Host ''
        if ($commit) { Write-Host 'RESET RESULTS:' } else { Write-Host 'PREVIEW (no data deleted):' }
        while ($reader.Read()) {
            $table = [string]$reader['Table']
            $before = [long]$reader['RowsBefore']
            $deleted = [long]$reader['RowsDeleted']
            $total += $before
            Write-Host ('  {0}: before={1}; deleted={2}' -f $table, $before, $deleted)
        }
        if (-not $reader.NextResult() -or -not $reader.Read()) { throw 'Missing SQL verification result.' }
        Write-Host ('  Mode: {0}' -f $reader['Mode'])
        Write-Host ('  Admin accounts preserved: {0}' -f $reader['AdminAccountsPreserved'])
        Write-Host ('  Roles preserved: {0}' -f $reader['RolesPreserved'])
        Write-Host ('  Total business/user records selected: {0}' -f $total)
    } finally { $reader.Dispose() }
}

try {
    $connection.Open()
    $currentDb = [string](SqlScalar 'SELECT DB_NAME()')
    if ($currentDb -cne $Database) { throw "Wrong database: $currentDb" }
    Write-Host "Server: $Server | Database: $currentDb"
    $admins = $connection.CreateCommand()
    $admins.CommandText = "SELECT u.Email FROM dbo.Users u INNER JOIN dbo.Roles r ON r.Id=u.RoleId WHERE r.Name=N'Admin' ORDER BY u.Email"
    Write-Host 'Admin accounts that will be preserved:'
    $r = $admins.ExecuteReader()
    try { while ($r.Read()) { Write-Host ('  ' + [string]$r.GetString(0)) } }
    finally { $r.Dispose() }
    # Preview also checks for unexpected tables and validates an active Admin account.
    PreviewOrApply $false
    if (-not $Apply) {
        Write-Host ''
        Write-Host 'PREVIEW ONLY: no changes made. Stop Backend, then rerun with -Apply for a backed-up reset.'
        return
    }

    if ([string]::IsNullOrWhiteSpace($BackupPath)) {
        $directory = [string](SqlScalar "SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(4000))")
        if ([string]::IsNullOrWhiteSpace($directory)) {
            $dataFile = [string](SqlScalar "SELECT TOP 1 physical_name FROM sys.database_files WHERE type_desc = 'ROWS' ORDER BY file_id")
            $directory = [System.IO.Path]::GetDirectoryName($dataFile)
        }
        if ([string]::IsNullOrWhiteSpace($directory)) {
            throw 'Cannot locate SQL Server backup directory. Specify -BackupPath with a .bak full path.'
        }
        $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
        $BackupPath = [System.IO.Path]::Combine($directory, "StudentProjectsDev-before-reset-$stamp.bak")
    }
    if (-not [System.IO.Path]::IsPathRooted($BackupPath)) { throw 'BackupPath must be a full absolute path.' }
    if (Test-Path -LiteralPath $BackupPath) { throw "Refusing to overwrite an existing backup: $BackupPath" }

    Write-Host "Backing up database to: $BackupPath"
    $dbNameSafe = $Database.Replace(']', ']]')
    $fileSafe = $BackupPath.Replace("'", "''")
    $backup = $connection.CreateCommand()
    $backup.CommandTimeout = 0
    $backup.CommandText = "BACKUP DATABASE [$dbNameSafe] TO DISK = N'$fileSafe' WITH COPY_ONLY, CHECKSUM, INIT;"
    [void]$backup.ExecuteNonQuery()
    $verify = $connection.CreateCommand()
    $verify.CommandTimeout = 0
    $verify.CommandText = "RESTORE VERIFYONLY FROM DISK = N'$fileSafe' WITH CHECKSUM;"
    [void]$verify.ExecuteNonQuery()
    Write-Host 'Backup complete and RESTORE VERIFYONLY passed.'

    $answer = Read-Host 'Type DELETE StudentProjectsDev to erase ALL non-Admin users and ALL business data'
    if ($answer -cne 'DELETE StudentProjectsDev') { throw 'Cancelled. Nothing was deleted.' }
    PreviewOrApply $true
    Write-Host "RESET COMPLETE. Backup preserved at: $BackupPath"
} finally {
    $connection.Dispose()
}
