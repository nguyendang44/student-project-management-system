$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = Split-Path -Parent $PSScriptRoot
$target = Join-Path $projectRoot 'frontend\src\components\AppLayout.vue'
if (-not (Test-Path -LiteralPath $target -PathType Leaf)) {
    throw "AppLayout.vue not found at $target. Unzip the patch at the project root."
}

$content = [System.IO.File]::ReadAllText($target, [System.Text.Encoding]::UTF8)
$pattern = '(?s)<div\s+class=["'']notice["'']\s*>\s*<strong>\s*Authentication,\s*User Management và Topic Management đã triển khai API\.\s*</strong>\s*Một số module nghiệp vụ khác vẫn ở dạng skeleton\.\s*</div>'
$regex = [regex]::new($pattern)
$matches = $regex.Matches($content)
if ($matches.Count -eq 0) {
    if ($content.Contains('Authentication, User Management và Topic Management đã triển khai API.')) {
        throw 'Found the text but the HTML container has a different structure. Nothing was modified; please send AppLayout.vue for a safe patch.'
    }
    Write-Host 'This banner is absent. No changes required.'
    exit 0
}

$updated = $regex.Replace($content, '')
if ($updated.Contains('Authentication, User Management và Topic Management đã triển khai API.')) {
    throw 'The banner still exists after replacement. No changes made.'
}
$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backup = "$target.bak-no-api-banner-$timestamp"
Copy-Item -LiteralPath $target -Destination $backup
try {
    [System.IO.File]::WriteAllText($target, $updated, [System.Text.UTF8Encoding]::new($false))
} catch {
    Copy-Item -LiteralPath $backup -Destination $target -Force
    throw
}
Write-Host "Removed $($matches.Count) shared banner(s) in AppLayout.vue."
Write-Host "Backup: $backup"
Write-Host 'Other content, menu, frontend routes, Backend and database are unchanged.'
