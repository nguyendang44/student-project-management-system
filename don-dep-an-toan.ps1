# Chay tu thu muc goc student-project-management-system. Mac dinh CHI HIEN THI, KHONG XOA.
# PowerShell: .\don-dep-an-toan.ps1          (xem truoc)
# PowerShell: .\don-dep-an-toan.ps1 -Apply   (thuc hien)
param(
    [switch]$Apply,
    [string]$ProjectPath = $PSScriptRoot
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectPath).Path
if (!(Test-Path -LiteralPath (Join-Path $root 'frontend/package.json')) -or
    !(Test-Path -LiteralPath (Join-Path $root 'backend/StudentProjects.Api/StudentProjects.Api.csproj'))) {
    throw "Khong phai thu muc goc student-project-management-system: $root"
}

$targets = New-Object 'System.Collections.Generic.List[string]'
foreach ($rel in @('frontend/node_modules', 'frontend/dist', '.vs', 'backend/.vs')) {
    $p = Join-Path $root $rel
    if (Test-Path -LiteralPath $p) { $targets.Add($p) }
}
Get-ChildItem -LiteralPath (Join-Path $root 'backend') -Directory -ErrorAction SilentlyContinue | ForEach-Object {
    foreach ($name in @('bin','obj')) {
        $p = Join-Path $_.FullName $name
        if (Test-Path -LiteralPath $p) { $targets.Add($p) }
    }
}
Get-ChildItem -LiteralPath $root -Directory -Filter 'v011-git-progress-*' -ErrorAction SilentlyContinue |
    ForEach-Object { $targets.Add($_.FullName) }

# Chi xoa nhung backup da duoc kiem tra trong ZIP, khong quet xoa file source khac.
$oldBackups = @(
    'backend/StudentProjects.Api/Endpoints/UserManagementEndpoints.cs.backup-adminonly-20261009-121329',
    'backend/StudentProjects.IntegrationTests/UserManagementIntegrationTests.cs.backup-adminonly-20261009-121329',
    'frontend/src/app/modules.ts.bak-v067-20261009-115339',
    'frontend/src/app/router.ts.bak-v067-20261009-115339',
    'frontend/src/components/AppLayout.vue.bak-no-api-banner-20261009-120834',
    'frontend/src/views/OwnProfileView.vue.backup-adminonly-20261009-121329'
)
foreach ($rel in $oldBackups) {
    $p = Join-Path $root $rel
    if (Test-Path -LiteralPath $p) { $targets.Add($p) }
}

Write-Host "Du an: $root"
Write-Host "Tim thay $($targets.Count) thu muc/file se don. KHONG xoa .git, benchmark/results, SQL, code va test."
$targets | ForEach-Object { Write-Host " - $_" }
if (!$Apply) {
    Write-Host "CHE DO XEM TRUOC. Muon thuc hien: .\don-dep-an-toan.ps1 -Apply" -ForegroundColor Yellow
    return
}
foreach ($p in $targets) {
    try { Remove-Item -LiteralPath $p -Recurse -Force -ErrorAction Stop; Write-Host "Da xoa: $p" }
    catch { Write-Warning "Khong xoa duoc $p : $_" }
}
# Chan cac file backup moi nham git add sau nay.
$gi = Join-Path $root '.gitignore'
if (Test-Path -LiteralPath $gi) {
    $content = Get-Content -LiteralPath $gi -Raw
    if (!$content.Contains('*.backup-*')) { Add-Content -LiteralPath $gi -Value "`n# Old one-off backup copies`n*.backup-*`n*.bak-*" }
}
Write-Host 'Da don xong. Kiem tra git status truoc khi commit:' -ForegroundColor Green
Write-Host 'git status --short'
Write-Host 'Neu xoa backup da duoc Git theo doi, xac nhan roi git add -u.'
