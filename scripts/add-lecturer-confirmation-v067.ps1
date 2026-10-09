$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot
$modulesPath = Join-Path $root 'frontend\src\app\modules.ts'
$routerPath = Join-Path $root 'frontend\src\app\router.ts'
$viewPath = Join-Path $root 'frontend\src\views\LecturerSupervisionView.vue'

foreach ($path in @($modulesPath, $routerPath, $viewPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Missing project file: $path. Extract the whole v0.6.7 zip into the project root first."
    }
}

$modules = [System.IO.File]::ReadAllText($modulesPath, [System.Text.Encoding]::UTF8)
$router = [System.IO.File]::ReadAllText($routerPath, [System.Text.Encoding]::UTF8)
$view = [System.IO.File]::ReadAllText($viewPath, [System.Text.Encoding]::UTF8)
if (-not $view.Contains("moduleId === 'lecturerconfirmation'")) {
    throw 'LecturerSupervisionView.vue is not the v0.6.7 patch. Extract the zip first.'
}

$nl = if ($modules.Contains("`r`n")) { "`r`n" } else { "`n" }
$typeAnchor = "  | 'lecturerrequests'"
$typeNew = "  | 'lecturerconfirmation'"
if (-not $modules.Contains($typeNew)) {
    if (-not $modules.Contains($typeAnchor)) { throw "Cannot find lecturerrequests ModuleId. No changes made." }
    $modules = $modules.Replace($typeAnchor, $typeAnchor + $nl + $typeNew)
}

$entryId = '"id": "lecturerconfirmation"'
if (-not $modules.Contains($entryId)) {
    $anchor = [regex]::Match($modules, '(?m)^  \{\r?\n    "id": "projects",')
    if (-not $anchor.Success) { throw 'Cannot locate projects module insertion point. No changes made.' }
    $entry = @'
  {
    "id": "lecturerconfirmation",
    "path": "/lecturer-confirmation",
    "group": "Hướng dẫn",
    "title": "Xác nhận giảng viên",
    "description": "Chọn giảng viên đã đồng ý hướng dẫn để xác nhận và tạo Project",
    "roles": ["Student"],
    "requirements": "FR-08, FR-09",
    "useCases": ["UC-11", "UC-13"],
    "columns": ["Giảng viên", "Đề tài", "Thao tác"],
    "endpoints": ["GET /lecturer-requests", "POST /lecturer-requests/{id}/select"]
  },
'@
    $modules = $modules.Insert($anchor.Index, $entry.Replace("`n", $nl) + $nl)
}

if (-not $router.Contains("mod.id === 'lecturerconfirmation'")) {
    $fallback = [regex]::Match($router, '(?m)^(?<indent>[ \t]*):[ \t]*\(\)[ \t]*=>[ \t]*import\([\x27\x22]\.\./views/ModuleView\.vue[\x27\x22]\)')
    if (-not $fallback.Success) { throw 'Cannot locate ModuleView router fallback. No changes made.' }
    $routeNl = if ($router.Contains("`r`n")) { "`r`n" } else { "`n" }
    $indent = $fallback.Groups['indent'].Value
    $newRoute = $indent + ": mod.id === 'lecturerconfirmation' ? () => import('../views/LecturerSupervisionView.vue')" + $routeNl
    $router = $router.Insert($fallback.Index, $newRoute)
}

if (-not $modules.Contains($typeNew) -or -not $modules.Contains($entryId) -or
    -not $router.Contains("mod.id === 'lecturerconfirmation'")) {
    throw 'Patch preflight validation failed. No changes made.'
}

$oldModules = [System.IO.File]::ReadAllText($modulesPath, [System.Text.Encoding]::UTF8)
$oldRouter = [System.IO.File]::ReadAllText($routerPath, [System.Text.Encoding]::UTF8)
if ($modules -eq $oldModules -and $router -eq $oldRouter) {
    Write-Host 'v0.6.7 confirmation module and route are already installed.'
    exit 0
}
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$modulesBackup = "$modulesPath.bak-v067-$stamp"
$routerBackup = "$routerPath.bak-v067-$stamp"
Copy-Item -LiteralPath $modulesPath -Destination $modulesBackup
Copy-Item -LiteralPath $routerPath -Destination $routerBackup
try {
    $utf8NoBom = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText($modulesPath, $modules, $utf8NoBom)
    [System.IO.File]::WriteAllText($routerPath, $router, $utf8NoBom)
} catch {
    Copy-Item -LiteralPath $modulesBackup -Destination $modulesPath -Force
    Copy-Item -LiteralPath $routerBackup -Destination $routerPath -Force
    throw
}
Write-Host 'v0.6.7 installed: Student menu > Huong dan > Xac nhan giang vien.'
Write-Host "Backups: $modulesBackup ; $routerBackup"
