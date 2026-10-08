# Windows PowerShell setup for SQL Server LocalDB + EF Core migration + development admin.
# Run from the project root: powershell -ExecutionPolicy Bypass -File .\scripts\setup-auth.ps1
$ErrorActionPreference = 'Stop'
$backend = Join-Path $PSScriptRoot '..\backend'
$api = Join-Path $backend 'StudentProjects.Api'
$infra = Join-Path $backend 'StudentProjects.Infrastructure'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw '.NET 10 SDK required.' }
if (-not (Get-Command sqllocaldb -ErrorAction SilentlyContinue)) { throw 'SQL Server Express LocalDB is not installed. Install LocalDB or configure another SQL Server connection string.' }
$instances = @(sqllocaldb info)
if ($instances -notcontains 'MSSQLLocalDB') { sqllocaldb create MSSQLLocalDB | Out-Host }
sqllocaldb start MSSQLLocalDB | Out-Host

Push-Location $backend
try {
    dotnet restore 'StudentProjects.sln'
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed' }
    dotnet build 'StudentProjects.sln' --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }

    $toolPath = Join-Path $env:USERPROFILE '.dotnet\tools'
    if ((Test-Path $toolPath) -and (($env:PATH -split ';') -notcontains $toolPath)) { $env:PATH += ';' + $toolPath }
    $installedTools = (dotnet tool list --global | Out-String)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect global dotnet tools' }
    if ($installedTools -notmatch 'dotnet-ef') {
        dotnet tool install --global dotnet-ef --version 10.0.10
        if ($LASTEXITCODE -ne 0) { throw 'dotnet-ef installation failed' }
    }
    $bytes = New-Object byte[] 48
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
    $jwtKey = [Convert]::ToBase64String($bytes)
    dotnet user-secrets set 'Jwt:Key' $jwtKey --project $api | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'JWT secret setup failed' }

    $migrationDir = Join-Path $infra 'Persistence\Migrations'
    if (-not (Test-Path $migrationDir) -or -not (Get-ChildItem $migrationDir -Filter '*InitialAuth*.cs' -ErrorAction SilentlyContinue)) {
        dotnet ef migrations add InitialAuth --project $infra --startup-project $api --context StudentProjectsDbContext --output-dir Persistence/Migrations
        if ($LASTEXITCODE -ne 0) { throw 'Migration creation failed' }
    }
    dotnet ef database update --project $infra --startup-project $api --context StudentProjectsDbContext
    if ($LASTEXITCODE -ne 0) { throw 'Database migration failed' }

    $email = Read-Host 'Enter development Admin email'
    if (-not $email -or -not $email.Contains('@')) { throw 'Valid admin email required' }
    $password = Read-Host 'Enter development Admin password (12+ characters)' -AsSecureString
    $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($password)
    try { $plain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) }
    if ($plain.Length -lt 12) { throw 'Password must have at least 12 characters' }
    dotnet user-secrets set 'AuthBootstrap:Email' $email --project $api | Out-Null
    dotnet user-secrets set 'AuthBootstrap:Password' $plain --project $api | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Admin bootstrap secret setup failed' }
    Remove-Variable plain -ErrorAction SilentlyContinue
    Write-Host 'Database ready. Start API with: dotnet run --project backend\StudentProjects.Api'
    Write-Host 'The Admin account will be created on first development API startup.'
}
finally { Pop-Location }
