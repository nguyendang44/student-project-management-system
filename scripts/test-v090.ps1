# V090_SECURITY_QUALITY_GATE
# Read-only against the local SQL database: all integration tests use EF InMemory.
param([switch]$SkipFrontend)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$reportDir = Join-Path $root 'qa-v090-results'
New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'

function Invoke-Step([string]$Label, [string]$Log, [string[]]$Command) {
    Write-Host "`n=== $Label ===" -ForegroundColor Cyan
    $exe = $Command[0]
    $arguments = @($Command | Select-Object -Skip 1)
    & $exe @arguments 2>&1 | Tee-Object -FilePath (Join-Path $reportDir "$stamp-$Log.log")
    if ($LASTEXITCODE -ne 0) { throw "FAILED: $Label (exit $LASTEXITCODE). Xem $stamp-$Log.log" }
}

if (-not (Test-Path '.\backend\StudentProjects.IntegrationTests\V09SecurityAndProjectLifecycleTests.cs')) {
    throw 'Chưa giải nén file kiểm thử của v0.9 vào thư mục dự án.'
}
if (-not (Select-String -Path '.\backend\StudentProjects.IntegrationTests\V09SecurityAndProjectLifecycleTests.cs' -Pattern 'V090_REGRESSION_TESTS' -SimpleMatch -Quiet)) {
    throw 'File kiểm thử v0.9 không đúng phiên bản.'
}
Invoke-Step '1/6 NuGet restore + vulnerability discovery' 'restore' @('dotnet','restore','.\backend\StudentProjects.sln','--force-evaluate')

# Enforce the patched dependency from NuGet's ACTUAL resolved assets,
# rather than assuming a successful edit fixed the transitive graph.
Write-Host "`n=== 2/6 Check resolved NuGet dependency ===" -ForegroundColor Cyan
foreach ($project in @('StudentProjects.Infrastructure','StudentProjects.Api','StudentProjects.IntegrationTests')) {
    $assetsFile = Join-Path $root "backend\$project\obj\project.assets.json"
    if (-not (Test-Path $assetsFile)) { throw "Missing NuGet assets: $assetsFile" }
    $assets = Get-Content $assetsFile -Raw | ConvertFrom-Json
    $matches = @($assets.libraries.PSObject.Properties.Name | Where-Object { $_ -like 'System.Security.Cryptography.Pkcs/*' })
    if ($matches.Count -ne 1 -or $matches[0] -ne 'System.Security.Cryptography.Pkcs/10.0.13') {
        throw "$project resolved Pkcs to '$($matches -join ',')'; expected 10.0.13. Do not ignore vulnerability warnings."
    }
    Write-Host "PASS $project : $($matches[0])"
}
Invoke-Step '3/6 Backend build' 'build' @('dotnet','build','.\backend\StudentProjects.sln','--no-restore','-v:minimal')
Invoke-Step '4/6 All integration tests' 'tests' @('dotnet','test','.\backend\StudentProjects.IntegrationTests\StudentProjects.IntegrationTests.csproj','--no-build','--no-restore','--logger','console;verbosity=normal')
Invoke-Step '5/6 NuGet vulnerability report' 'audit' @('dotnet','list','.\backend\StudentProjects.sln','package','--vulnerable','--include-transitive')

if (-not $SkipFrontend) {
    Write-Host "`n=== 6/6 Frontend production build ===" -ForegroundColor Cyan
    Push-Location (Join-Path $root 'frontend')
    try {
        npm run build 2>&1 | Tee-Object -FilePath (Join-Path $reportDir "$stamp-frontend.log")
        if ($LASTEXITCODE -ne 0) { throw 'FAILED: Frontend build.' }
    } finally { Pop-Location }
} else {
    Write-Host '6/6 Frontend skipped by user option.'
}

Write-Host "`nPASS: v0.9 quality gate completed." -ForegroundColor Green
Write-Host "Logs: $reportDir"
Write-Host 'Note: manual end-to-end registration and browser permission review remain to be validated (see QA checklist).'
