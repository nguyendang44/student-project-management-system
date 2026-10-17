$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
Push-Location $root
try {
  Write-Host 'BACKEND BUILD'
  dotnet build .\backend\StudentProjects.sln
  if ($LASTEXITCODE -ne 0) { throw 'Backend build failed' }
  Write-Host 'INTEGRATION TESTS'
  dotnet test .\backend\StudentProjects.IntegrationTests\StudentProjects.IntegrationTests.csproj --no-build
  if ($LASTEXITCODE -ne 0) { throw 'Integration tests failed' }
  Push-Location .\frontend
  try {
    Write-Host 'FRONTEND BUILD'
    npm run build
    if ($LASTEXITCODE -ne 0) { throw 'Frontend build failed' }
  } finally { Pop-Location }
  Write-Host 'BUILD AND EXISTING TESTS PASSED. Continue with the manual v0.7 workflow QA in README-V07.md.'
} finally { Pop-Location }
