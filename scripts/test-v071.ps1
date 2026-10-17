$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
  & dotnet build .\backend\StudentProjects.sln
  if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed' }
  & dotnet test .\backend\StudentProjects.IntegrationTests\StudentProjects.IntegrationTests.csproj --no-restore
  if ($LASTEXITCODE -ne 0) { throw 'dotnet test failed' }
  Push-Location (Join-Path $root 'frontend')
  try {
    & npm run build
    if ($LASTEXITCODE -ne 0) { throw 'npm run build failed' }
  } finally { Pop-Location }
  Write-Host 'v0.7.1 BUILD + EXISTING TESTS PASSED. Continue with manual role/workflow QA.'
} finally { Pop-Location }
