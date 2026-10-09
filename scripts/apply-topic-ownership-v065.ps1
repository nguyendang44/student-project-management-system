# Applies a guarded server-side ownership check to the CURRENT API source.
# Does not replace TopicEndpoints.cs or modify SQL Server.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$file = Join-Path $root 'backend\StudentProjects.Api\Endpoints\TopicEndpoints.cs'
if (!(Test-Path -LiteralPath $file)) { throw "Missing $file" }
$code = [System.IO.File]::ReadAllText($file)
$start = [regex]::Match($code, 'private\s+static\s+async\s+Task<IResult>\s+SetRegistration\s*\(')
if (!$start.Success) { throw 'Cannot identify SetRegistration method; no changes made.' }
$brace = $code.IndexOf('{', $start.Index)
if ($brace -lt 0) { throw 'Cannot identify method body; no changes made.' }
$depth = 0; $last = -1
for ($i = $brace; $i -lt $code.Length; $i++) {
    if ($code[$i] -eq '{') { $depth++ }
    elseif ($code[$i] -eq '}') { $depth--; if ($depth -eq 0) { $last = $i; break } }
}
if ($last -lt 0) { throw 'Cannot identify method end; no changes made.' }
$method = $code.Substring($brace, $last - $brace + 1)
$guard = 'if (Role(principal) != "Admin" && topic.ProposedByUserId != Actor(principal)) return Results.Forbid();'
if ($method.Contains($guard)) { Write-Host 'Ownership guard already present. No changes needed.'; exit 0 }
if (-not ($method -match '\bClaimsPrincipal\s+principal\b') -and $code.Substring($start.Index,$brace-$start.Index) -notmatch '\bClaimsPrincipal\s+principal\b') { throw 'Unexpected SetRegistration signature; no changes made.' }
if ($method -notmatch 'if\s*\(topic\s+is\s+null\)\s*return\s+Results\.NotFound\(\);') {
    throw 'Expected topic-not-found guard missing. Stop and review current API; no changes made.'
}
$matched = [regex]::Match($method, 'if\s*\(topic\s+is\s+null\)\s*return\s+Results\.NotFound\(\);')
$insertAt = $matched.Index + $matched.Length
$updatedMethod = $method.Insert($insertAt, "`r`n        // v0.6.5: only the creator or Admin may open/close this topic.`r`n        $guard")
$updated = $code.Substring(0,$brace) + $updatedMethod + $code.Substring($last+1)
$backup = "$file.before-v065.bak"
if (!(Test-Path -LiteralPath $backup)) { Copy-Item $file $backup }
[System.IO.File]::WriteAllText($file,$updated,(New-Object System.Text.UTF8Encoding($false)))
Write-Host 'OK: SetRegistration now checks topic creator or Admin at the Backend.'
Write-Host "Backup: $backup"
