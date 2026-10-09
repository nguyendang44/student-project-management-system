[CmdletBinding()]
param([switch]$Preview)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$apiFile = Join-Path $root 'backend\StudentProjects.Api\Endpoints\UserManagementEndpoints.cs'
$testFile = Join-Path $root 'backend\StudentProjects.IntegrationTests\UserManagementIntegrationTests.cs'
$vueFile = Join-Path $root 'frontend\src\views\OwnProfileView.vue'
$vueReplacement = Join-Path $PSScriptRoot 'OwnProfileView.admin-readonly.vue'
foreach ($p in @($apiFile,$testFile,$vueFile,$vueReplacement)) { if (-not (Test-Path -LiteralPath $p)) { throw "File not found: $p" } }

$utf8 = New-Object System.Text.UTF8Encoding($false)
function Read-File([string]$path) { return [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8) }
function Replace-TestMethod([string]$source,[string]$oldName,[string]$newName,[string]$newMethod) {
    $oldMarker = '    public async Task ' + $oldName + '()'
    $newMarker = '    public async Task ' + $newName + '()'
    if ($source.Contains($newMarker)) { return $source }
    $start = $source.IndexOf($oldMarker, [StringComparison]::Ordinal)
    if ($start -lt 0) { throw "Test method not found or changed: $oldName" }
    $end = $source.IndexOf('    [Fact]', $start + $oldMarker.Length, [StringComparison]::Ordinal)
    if ($end -lt 0) { throw "Cannot locate next test boundary after $oldName" }
    return $source.Substring(0,$start) + $newMethod.TrimEnd() + "`r`n`r`n" + $source.Substring($end)
}

$api = Read-File $apiFile
$tests = Read-File $testFile
$view = Read-File $vueFile
$readonlyView = Read-File $vueReplacement

$oldRoute = 'users.MapPut("/me/profile", UpdateOwnProfile).RequireAuthorization(p => p.RequireRole("Student", "Lecturer"));'
$newRoute = 'users.MapPut("/me/profile", () => Results.Forbid()).RequireAuthorization();'
if ($api.Contains($oldRoute)) {
    $api = $api.Replace($oldRoute, $newRoute)
} elseif (-not $api.Contains($newRoute)) {
    throw 'Cannot safely locate PUT /users/me/profile authorization. No files were modified.'
}

# Remove the previous unrestricted self-edit implementation. Admin uses PUT /users/{id}.
$oldMethodStart = '    private static async Task<IResult> UpdateOwnProfile('
$nextMethodStart = '    private static async Task<IResult> LecturerDirectory('
$start = $api.IndexOf($oldMethodStart, [StringComparison]::Ordinal)
if ($start -ge 0) {
    $end = $api.IndexOf($nextMethodStart, $start, [StringComparison]::Ordinal)
    if ($end -lt 0) { throw 'Cannot safely locate the end of UpdateOwnProfile. No files were modified.' }
    $api = $api.Substring(0,$start) + $api.Substring($end)
}
if (-not $api.Contains('users.MapPut("/{id:guid}", Update).RequireAuthorization(p => p.RequireRole("Admin"));')) {
    throw 'Admin-only user-edit endpoint not found. No files were modified.'
}
if (-not $api.Contains('users.MapGet("/me/profile", GetOwnProfile)')) {
    throw 'Read-only own-profile endpoint not found. No files were modified.'
}

$studentTest = @'
    public async Task Student_CannotManageAccounts_OrEditOwnProfile()
    {
        using var app = new TestApp();
        await app.SeedAsync();
        using var student = await Authenticated(app, "student@test.local");
        Assert.Equal(HttpStatusCode.Forbidden, (await student.GetAsync("/api/v1/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await student.PostAsJsonAsync("/api/v1/users", new
        {
            fullName = "Wrong", email = "fail@test.local", password = Password, role = "Student", studentCode = "X1"
        })).StatusCode);
        var before = await student.GetFromJsonAsync<JsonDocument>("/api/v1/users/me/profile");
        Assert.Equal("ST100", before!.RootElement.GetProperty("studentCode").GetString());
        var response = await student.PutAsJsonAsync("/api/v1/users/me/profile",
            new { fullName = "Changed Student", faculty = "Computer Science", specialty = (string?)null });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var after = (await student.GetFromJsonAsync<JsonDocument>("/api/v1/users/me/profile"))!;
        Assert.Equal("Test Student", after.RootElement.GetProperty("fullName").GetString());
        Assert.Equal("ST100", after.RootElement.GetProperty("studentCode").GetString());
        Assert.Equal(JsonValueKind.Null, after.RootElement.GetProperty("faculty").ValueKind);
        var id = after.RootElement.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Forbidden, (await student.GetAsync($"/api/v1/users/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await student.PatchAsJsonAsync($"/api/v1/users/{id}/status", new { isActive = false })).StatusCode);
    }
'@
$lecturerTest = @'
    public async Task Lecturer_CannotEditOwnProfile_AdminCanUpdateSpecialty_DirectoryShowsActive()
    {
        using var app = new TestApp();
        await app.SeedAsync();
        using var lecturer = await Authenticated(app, "lecturer@test.local");
        using var admin = await Authenticated(app, "admin@test.local");
        var edit = await lecturer.PutAsJsonAsync("/api/v1/users/me/profile",
            new { fullName = "Professor L", specialty = "Data Science", faculty = (string?)null });
        Assert.Equal(HttpStatusCode.Forbidden, edit.StatusCode);
        using var own = (await lecturer.GetFromJsonAsync<JsonDocument>("/api/v1/users/me/profile"))!;
        Assert.Equal("Test Lecturer", own.RootElement.GetProperty("fullName").GetString());
        var id = own.RootElement.GetProperty("id").GetGuid();
        var adminEdit = await admin.PutAsJsonAsync($"/api/v1/users/{id}", new
        {
            fullName = "Professor L", email = "lecturer@test.local", specialty = "Data Science"
        });
        Assert.Equal(HttpStatusCode.OK, adminEdit.StatusCode);
        using var directory = (await lecturer.GetFromJsonAsync<JsonDocument>("/api/v1/lecturers"))!;
        Assert.Single(directory.RootElement.EnumerateArray());
        Assert.Equal("Data Science", directory.RootElement[0].GetProperty("specialty").GetString());
        Assert.Equal(HttpStatusCode.OK, (await admin.PatchAsJsonAsync($"/api/v1/users/{id}/status", new { isActive = false })).StatusCode);
        using var another = await Authenticated(app, "student@test.local");
        using var noLecturers = (await another.GetFromJsonAsync<JsonDocument>("/api/v1/lecturers"))!;
        Assert.Empty(noLecturers.RootElement.EnumerateArray());
    }
'@
$tests = Replace-TestMethod $tests 'Student_CannotManageAccounts_ButCanUpdateOnlyOwnAllowedFields' 'Student_CannotManageAccounts_OrEditOwnProfile' $studentTest
$tests = Replace-TestMethod $tests 'Lecturer_UpdatesSpecialty_DirectoryShowsOnlyActiveLecturers' 'Lecturer_CannotEditOwnProfile_AdminCanUpdateSpecialty_DirectoryShowsActive' $lecturerTest

$changes = @(
    @{ Path = $apiFile; Old = (Read-File $apiFile); New = $api },
    @{ Path = $testFile; Old = (Read-File $testFile); New = $tests },
    @{ Path = $vueFile; Old = $view; New = $readonlyView }
) | Where-Object { $_.Old -cne $_.New }
if ($changes.Count -eq 0) { Write-Host 'Already applied. No changes needed.'; exit 0 }
Write-Host ('Files to update: ' + $changes.Count)
foreach ($c in $changes) { Write-Host ('  ' + $c.Path) }
if ($Preview) { Write-Host 'PREVIEW ONLY, nothing written.'; exit 0 }

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backups = @()
try {
    foreach ($c in $changes) {
        $backup = $c.Path + '.backup-adminonly-' + $stamp
        Copy-Item -LiteralPath $c.Path -Destination $backup -ErrorAction Stop
        $backups += @{ File = $c.Path; Backup = $backup }
    }
    foreach ($c in $changes) {
        [System.IO.File]::WriteAllText($c.Path, $c.New, $utf8)
    }
} catch {
    foreach ($b in $backups) { Copy-Item -LiteralPath $b.Backup -Destination $b.File -Force }
    throw
}
Write-Host 'APPLIED: Student and Lecturer can view only. Admin may edit accounts from User Management.'
Write-Host 'Previous files backed up beside originals with suffix .backup-adminonly-...'
