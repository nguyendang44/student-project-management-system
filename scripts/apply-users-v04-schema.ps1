# Run from any folder: powershell -ExecutionPolicy Bypass -File .\scripts\apply-users-v04-schema.ps1
# Non-destructive, additive schema update for an existing v0.3 LocalDB installation.
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$backend = Join-Path $repo 'backend'
$api = Join-Path $backend 'StudentProjects.Api'
$infra = Join-Path $backend 'StudentProjects.Infrastructure'
$migrations = Join-Path $infra 'Persistence\Migrations'
$target = 'UserManagementV04Schema'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Missing dotnet SDK.' }
$runningApi = @(Get-Process -Name 'StudentProjects.Api' -ErrorAction SilentlyContinue)
if ($runningApi.Count -gt 0) { throw 'Stop the running StudentProjects.Api process before updating schema (Ctrl+C in API terminal).' }
if (-not (Test-Path $migrations)) { throw 'InitialAuth migration files are missing. Do NOT rerun setup-auth.ps1. Check your local Persistence/Migrations folder.' }
$initial = @(Get-ChildItem $migrations -Filter '*InitialAuth.cs' -File)
if ($initial.Count -ne 1) { throw 'Expected exactly one local InitialAuth migration. Review your existing migration history before proceeding.' }

$toolBin = Join-Path $env:USERPROFILE '.dotnet\tools'
if ((Test-Path $toolBin) -and (($env:PATH -split ';') -notcontains $toolBin)) { $env:PATH += ';' + $toolBin }
if (-not (Get-Command dotnet-ef -ErrorAction SilentlyContinue)) { throw 'dotnet-ef was not found. Install the EF tool compatible with your .NET 10 SDK.' }

Push-Location $repo
try {
    dotnet build '.\backend\StudentProjects.sln'
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    $candidate = @(Get-ChildItem $migrations -Filter "*$target.cs" -File)
    if ($candidate.Count -eq 0) {
        dotnet ef migrations add $target --project $infra --startup-project $api --context StudentProjectsDbContext --output-dir Persistence/Migrations
        if ($LASTEXITCODE -ne 0) { throw 'Failed to generate the v0.4 migration.' }
        $candidate = @(Get-ChildItem $migrations -Filter "*$target.cs" -File)
    }
    if ($candidate.Count -ne 1) { throw 'Could not identify exactly one v0.4 migration file.' }
    $migrationText = Get-Content $candidate[0].FullName -Raw
    $up = [regex]::Match($migrationText, '(?s)protected\s+override\s+void\s+Up\(MigrationBuilder migrationBuilder\)(.*?)(?=protected\s+override\s+void\s+Down\()')
    if (-not $up.Success) { throw 'Unable to validate the generated migration Up() method. STOP and review migration.' }
    $created = @([regex]::Matches($up.Groups[1].Value, 'migrationBuilder\s*\.\s*CreateTable\(\s*name:\s*"([^"]+)"') | ForEach-Object { $_.Groups[1].Value } | Sort-Object)
    $expected = @('AuditEntries', 'Lecturers', 'Students')
    if (($created -join ',') -ne ($expected -join ',')) {
        throw "Unexpected migration tables: $($created -join ','). Expected: $($expected -join ','). STOP and review; database has not been modified."
    }
    $unsafe = 'migrationBuilder\s*\.\s*(DropTable|DropColumn|AlterColumn|DeleteData|UpdateData|RenameTable|RenameColumn)\s*\('
    if ([regex]::IsMatch($up.Groups[1].Value, $unsafe)) {
        throw 'Migration has a potentially destructive change. STOP and review; database has not been modified.'
    }
    Write-Host 'Validated migration: creates only Students, Lecturers, AuditEntries.'
    Write-Host 'Applying migration to your configured SQL Server database...'
    dotnet ef database update --project $infra --startup-project $api --context StudentProjectsDbContext
    if ($LASTEXITCODE -ne 0) { throw 'Database update failed. Inspect the error; do not recreate the database.' }
    Write-Host 'v0.4 User Management schema applied.'
    Write-Host 'Now restart Backend and reload the Users page.'
}
finally { Pop-Location }
