$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $root
$migrationsDir = Join-Path $root 'backend/StudentProjects.Infrastructure/Persistence/Migrations'
$templatePath = Join-Path $PSScriptRoot 'TopicManagementV05.template.cs.txt'
if (-not (Test-Path $templatePath)) { throw 'Missing topic migration template.' }
if ((Get-ChildItem -Path $migrationsDir -Filter '*_TopicManagementV05.cs' -ErrorAction SilentlyContinue).Count -gt 0) {
    throw 'TopicManagementV05 migration exists already. Do not scaffold twice. Review existing migration.'
}
Write-Host 'Scaffolding Topic Management v0.5 migration (does NOT update the database)...'
& dotnet ef migrations add TopicManagementV05 --project .\backend\StudentProjects.Infrastructure --startup-project .\backend\StudentProjects.Api --context StudentProjectsDbContext --output-dir Persistence/Migrations
if ($LASTEXITCODE -ne 0) { throw 'EF Core could not scaffold migration. Database unchanged.' }
$files = @(Get-ChildItem -Path $migrationsDir -Filter '*_TopicManagementV05.cs')
if ($files.Count -ne 1) { throw 'Could not uniquely identify newly scaffolded migration. Stop.' }
$target = $files[0].FullName
$original = Get-Content -Path $target -Raw
$ops = [regex]::Matches($original, 'migrationBuilder\.(\w+)\s*\(') | ForEach-Object { $_.Groups[1].Value }
$unexpected = @($ops | Where-Object { $_ -notin @('CreateTable', 'CreateIndex', 'DropTable') })
if ($unexpected.Count -gt 0) { throw "Scaffold contains other operations: $($unexpected -join ','). Review. Database unchanged." }
$createTables = @([regex]::Matches($original, 'CreateTable\s*\(\s*name:\s*"([^"]+)"') | ForEach-Object { $_.Groups[1].Value })
$expected = @('RegistrationPeriods', 'Topics', 'TopicStateHistories', 'TopicRegistrations')
$dropped = @([regex]::Matches($original, 'DropTable\s*\(\s*name:\s*"([^"]+)"') | ForEach-Object { $_.Groups[1].Value })
if (@($dropped | Where-Object { $_ -notin $expected }).Count -gt 0) {
    throw "Unexpected scaffold drop tables: $($dropped -join ','). Database unchanged."
}
if ($createTables.Count -gt 0 -and (($createTables.Count -ne 4) -or (@($createTables | Where-Object { $_ -notin $expected }).Count -gt 0))) {
    throw "Unexpected scaffold tables: $($createTables -join ','). Review. Database unchanged."
}
# Excluded-from-migrations entities may produce empty Up. Replace only the newly generated
# migration implementation. Keep EF Core's generated Designer and ModelSnapshot intact.
Copy-Item $templatePath $target -Force
Write-Host 'Migration implementation updated; EF Designer and ModelSnapshot preserved.'
& dotnet build .\backend\StudentProjects.sln
if ($LASTEXITCODE -ne 0) { throw 'Build failed. Database unchanged.' }
$sqlPath = Join-Path $root 'topics-v05-preview.sql'
& dotnet ef migrations script 20261008034714_UserManagementV04Schema TopicManagementV05 --project .\backend\StudentProjects.Infrastructure --startup-project .\backend\StudentProjects.Api --context StudentProjectsDbContext --output $sqlPath
if ($LASTEXITCODE -ne 0) { throw 'SQL preview failed. Database unchanged.' }
$sql = Get-Content $sqlPath -Raw
foreach ($name in $expected) {
    if ($sql -notmatch "CREATE TABLE \[$name\]") { throw "SQL preview does not create $name. Database unchanged." }
}
$created = @([regex]::Matches($sql,'CREATE TABLE \[([^]]+)\]') | ForEach-Object { $_.Groups[1].Value })
if ($created.Count -ne 4 -or @($created | Where-Object { $_ -notin $expected }).Count -gt 0) {
    throw "Preview includes unexpected tables: $($created -join ','). Stop."
}
Write-Host 'Build OK. Preview verified: 4 tables. No database changes have been made.'
Write-Host "Review SQL preview: $sqlPath"
Write-Host 'Then, after backup, apply: dotnet ef database update --project .\backend\StudentProjects.Infrastructure --startup-project .\backend\StudentProjects.Api --context StudentProjectsDbContext'
