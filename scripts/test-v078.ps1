$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $root
Write-Host '=== v0.7.8 Backend build ==='
dotnet build .\backend\StudentProjects.sln
if ($LASTEXITCODE -ne 0) { throw 'Backend build failed' }
Write-Host '=== v0.7.8 integration tests ==='
dotnet test .\backend\StudentProjects.IntegrationTests\StudentProjects.IntegrationTests.csproj
if ($LASTEXITCODE -ne 0) { throw 'Integration tests failed' }
Write-Host '=== v0.7.8 frontend build ==='
Set-Location (Join-Path $root 'frontend')
npm run build
if ($LASTEXITCODE -ne 0) { throw 'Frontend build failed' }
Write-Host 'v0.7.8 checks passed. Please test template application manually as described in README.'
