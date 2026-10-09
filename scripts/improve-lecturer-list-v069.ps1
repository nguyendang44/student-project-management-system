$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$target = Join-Path $projectRoot 'frontend\src\views\LecturerDirectoryView.vue'
if (-not (Test-Path -LiteralPath $target -PathType Leaf)) {
    throw "Cannot find LecturerDirectoryView.vue at $target. Extract the ZIP into the project root first."
}

$source = [System.IO.File]::ReadAllText($target, [System.Text.Encoding]::UTF8)
if ($source.Contains('lecturer-directory-spaced')) {
    Write-Host 'v0.6.9 lecturer list layout already applied. Nothing to change.' -ForegroundColor Yellow
    exit 0
}

# Scope the modification to the specific Họ tên / Chuyên môn table.
$tableMatch = [regex]::Match($source, '(?s)<table\b[^>]*>.*?</table>')
if (-not $tableMatch.Success) {
    throw 'Could not locate the lecturer directory table. No file changed.'
}
$table = $tableMatch.Value
if ($table -notmatch '(?s)<th\b[^>]*>\s*Họ tên\s*</th>\s*<th\b[^>]*>\s*Chuyên môn\s*</th>') {
    throw 'The table columns differ from Họ tên / Chuyên môn. No file changed.'
}
$openTag = [regex]::Match($table, '^<table\b[^>]*>')
if (-not $openTag.Success) { throw 'Table opening tag not found.' }
$newOpenTag = $openTag.Value
$classMatch = [regex]::Match($newOpenTag, 'class\s*=\s*"([^"]*)"')
if ($classMatch.Success) {
    $newOpenTag = $newOpenTag.Substring(0, $classMatch.Index) + 'class="' + $classMatch.Groups[1].Value + ' lecturer-directory-spaced"' + $newOpenTag.Substring($classMatch.Index + $classMatch.Length)
} else {
    $newOpenTag = $newOpenTag.Substring(0, $newOpenTag.Length - 1) + ' class="lecturer-directory-spaced">'
}
$revisedTable = $newOpenTag + $table.Substring($openTag.Length)
$revisedSource = $source.Substring(0, $tableMatch.Index) + $revisedTable + $source.Substring($tableMatch.Index + $tableMatch.Length)
$css = @'

<style scoped>
/* v0.6.9: lecturer directory only. Distinct, breathable cells without affecting other tables. */
.lecturer-directory-spaced {
  width: 100%;
  border-collapse: separate;
  border-spacing: 0 12px;
  table-layout: fixed;
}
.lecturer-directory-spaced th {
  padding: 14px 22px;
  background: #f4f7fc;
  color: #566783;
  border-bottom: 1px solid #e3e9f3;
}
.lecturer-directory-spaced th:first-child { width: 42%; }
.lecturer-directory-spaced tbody td {
  padding: 19px 22px;
  border-top: 1px solid #dce4f0;
  border-bottom: 1px solid #dce4f0;
  background: #fafdff;
  color: #344560;
  font-size: 14px;
  line-height: 1.6;
  vertical-align: middle;
  overflow-wrap: anywhere;
}
.lecturer-directory-spaced tbody td:first-child {
  border-left: 1px solid #dce4f0;
  border-radius: 10px 0 0 10px;
  font-weight: 650;
}
.lecturer-directory-spaced tbody td + td { border-left: 1px solid #e3e9f3; }
.lecturer-directory-spaced tbody td:last-child {
  border-right: 1px solid #dce4f0;
  border-radius: 0 10px 10px 0;
}
.lecturer-directory-spaced tbody td:only-child { border-radius: 10px; }
.lecturer-directory-spaced tbody tr:hover td { background: #f1f6ff; }
@media (max-width: 600px) {
  .lecturer-directory-spaced th { padding: 12px 14px; }
  .lecturer-directory-spaced tbody td { padding: 15px 14px; }
}
</style>
'@
$revisedSource = $revisedSource.TrimEnd() + "`r`n" + $css + "`r`n"

$backupFolder = Join-Path $projectRoot 'scripts\backups-v069'
New-Item -ItemType Directory -Force -Path $backupFolder | Out-Null
$backup = Join-Path $backupFolder ('LecturerDirectoryView.vue.' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.bak')
Copy-Item -LiteralPath $target -Destination $backup -ErrorAction Stop
[System.IO.File]::WriteAllText($target, $revisedSource, (New-Object System.Text.UTF8Encoding($false)))
Write-Host 'Applied v0.6.9 spacing/borders to LecturerDirectoryView.vue.' -ForegroundColor Green
Write-Host "Backup: $backup"
Write-Host 'All other pages and API/SQL files remain unchanged.'
