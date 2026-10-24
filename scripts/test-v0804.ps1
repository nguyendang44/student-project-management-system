$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root

Write-Host '1/3 - Validate notification deep-link patch...'
# These five source files are changed by install-v0804-notification-links.cjs
# and therefore carry the installer marker.
$patchedFiles = @(
  'backend/StudentProjects.Api/Endpoints/V08Endpoints.cs',
  'frontend/src/features/v08/v08.api.ts',
  'frontend/src/views/NotificationsV08View.vue',
  'frontend/src/views/GuidanceV08View.vue',
  'frontend/src/views/ProjectProgressV07View.vue'
)
foreach ($path in $patchedFiles) {
  if (-not (Test-Path $path)) { throw "Missing: $path" }
  if (-not (Select-String -Path $path -Pattern 'V0804_NOTIFICATION_LINKS' -Quiet)) {
    throw "v0.8.4 marker missing: $path"
  }
}

# The helper is an added C# source file, not a modified file.
# Validate its actual class/method rather than expecting an installer marker.
$helper = 'backend/StudentProjects.Api/Services/V08NotificationLinks.cs'
if (-not (Test-Path $helper)) { throw "Missing: $helper" }
if (-not (Select-String -Path $helper -Pattern 'public static class V08NotificationLinks' -SimpleMatch -Quiet)) {
  throw "Invalid v0.8.4 notification-link helper: $helper"
}
if (-not (Select-String -Path $helper -Pattern 'ResolveAsync(' -SimpleMatch -Quiet)) {
  throw "Missing ResolveAsync in: $helper"
}
Write-Host 'PASS: v0.8.4 patch files and helper validated.'

Write-Host '2/3 - Backend build and integration tests...'
dotnet build .\backend\StudentProjects.sln
if ($LASTEXITCODE -ne 0) { throw 'Backend build failed.' }
dotnet test .\backend\StudentProjects.IntegrationTests\StudentProjects.IntegrationTests.csproj --no-build
if ($LASTEXITCODE -ne 0) { throw 'Integration tests failed.' }

Write-Host '3/3 - Frontend typecheck and production build...'
Push-Location .\frontend
try {
  npm run build
  if ($LASTEXITCODE -ne 0) { throw 'Frontend build failed.' }
} finally { Pop-Location }
Write-Host 'PASS: Static patch check, backend build, tests and frontend build.'
Write-Host 'Next: test a real notification click in the browser.'
