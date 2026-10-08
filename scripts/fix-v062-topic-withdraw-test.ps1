# Updates one v0.6.2 regression test. Does not change application code or SQL Server.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$testFile = Join-Path $root 'backend\StudentProjects.IntegrationTests\TopicIntegrationTests.cs'
if (!(Test-Path -LiteralPath $testFile)) {
    throw "Test file not found: $testFile. Extract ZIP into project root first."
}

$source = [System.IO.File]::ReadAllText($testFile)
$methodName = 'WithdrawingApprovedSelfProposedTopic_ReleasesOwnershipAndAllowsRegistration'
$pattern = '(?ms)^    \[Fact\]\r?\n    public async Task ' + [regex]::Escape($methodName) + '\(\)\r?\n    \{.*?(?=^    \[Fact\]|\z)'
$regex = [regex]::new($pattern)
if ($regex.Matches($source).Count -ne 1) {
    throw "Expected one test method named $methodName; no changes made."
}

$updatedMethod = @'
    [Fact]
    public async Task WithdrawingApprovedSelfProposedTopic_ReleasesOwnershipAndAllowsRegistration()
    {
        using var app = new TopicTestApp(); await app.SeedAsync();
        using var student = await Login(app, "student@tests.local");
        using var other = await Login(app, "other@tests.local");
        using var lecturer = await Login(app, "lecturer@tests.local");
        using var admin = await Login(app, "admin@tests.local");

        var selfId = await Id(await student.PostAsJsonAsync("/api/v1/topic-proposals", Draft("Self topic")));
        Assert.Equal(HttpStatusCode.OK, (await lecturer.PostAsync($"/api/v1/topic-proposals/{selfId}/approve", null)).StatusCode);
        var publishedId = await Id(await lecturer.PostAsJsonAsync("/api/v1/topics", Draft("Open lecturer topic")));
        Assert.Equal(HttpStatusCode.OK, (await lecturer.PatchAsJsonAsync($"/api/v1/topics/{publishedId}/registration", new { isOpen = true })).StatusCode);
        var periodId = await Id(await admin.PostAsJsonAsync("/api/v1/registration-periods", new
        { name = "Open period", startsAt = DateTimeOffset.UtcNow.AddDays(-1), endsAt = DateTimeOffset.UtcNow.AddDays(1), isOpen = true }));

        // Student cannot register elsewhere while they own a self-proposed topic.
        Assert.Equal(HttpStatusCode.Conflict, (await student.PostAsJsonAsync("/api/v1/topic-registrations", new { topicId = publishedId, registrationPeriodId = periodId })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PostAsync($"/api/v1/topics/{selfId}/withdraw", null)).StatusCode);

        // New v0.6.2 rule: withdrawing permanently deletes the self-proposed topic.
        Assert.Equal(HttpStatusCode.OK, (await student.PostAsync($"/api/v1/topics/{selfId}/withdraw", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await student.PostAsync($"/api/v1/topics/{selfId}/withdraw", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await student.GetAsync($"/api/v1/topics/{selfId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/topics/{selfId}")).StatusCode);
        using var mine = (await student.GetFromJsonAsync<JsonDocument>("/api/v1/topics/mine"))!;
        Assert.DoesNotContain(mine.RootElement.EnumerateArray(), item => item.GetProperty("id").GetGuid() == selfId);

        // Ownership has been released: the student can register for a staff topic.
        Assert.Equal(HttpStatusCode.Created, (await student.PostAsJsonAsync("/api/v1/topic-registrations", new { topicId = publishedId, registrationPeriodId = periodId })).StatusCode);
    }
'@

$backup = "$testFile.before-v062-withdraw-test-hotfix.bak"
if (!(Test-Path -LiteralPath $backup)) {
    Copy-Item -LiteralPath $testFile -Destination $backup
}
$replacement = $updatedMethod.TrimEnd() + [Environment]::NewLine + [Environment]::NewLine
$updated = $regex.Replace($source, [System.Text.RegularExpressions.MatchEvaluator]{ param($m) $replacement }, 1)
[System.IO.File]::WriteAllText($testFile, $updated, (New-Object System.Text.UTF8Encoding($false)))
Write-Host 'OK: Updated only WithdrawingApprovedSelfProposedTopic_ReleasesOwnershipAndAllowsRegistration.'
Write-Host "Backup: $backup"
