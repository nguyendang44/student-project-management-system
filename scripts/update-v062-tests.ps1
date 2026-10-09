# v0.6.2: Update only known test methods; keep all user-added tests intact.
# No database changes. Run after copying the application patch.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$testRoot = Join-Path $root 'backend\StudentProjects.IntegrationTests'
function BackupAndRead([string]$file) {
  $path = Join-Path $testRoot $file
  if (!(Test-Path $path)) { throw "Missing test file: $path" }
  $backup = "$path.before-v062.bak"
  if (!(Test-Path $backup)) { Copy-Item -LiteralPath $path -Destination $backup }
  return [System.IO.File]::ReadAllText($path)
}
function ReplaceMethod([string]$data, [string]$old, [string]$newName, [string]$body) {
  $pattern = '(?ms)^    \[Fact\]\r?\n    public async Task ' + [regex]::Escape($old) + '\(\)\r?\n    \{.*?(?=^    \[Fact\]|\z)'
  if ($data.Contains('public async Task ' + $newName + '()') -and $old -ne $newName) { return $data }
  $regex = [regex]::new($pattern)
  if (!$regex.IsMatch($data)) { throw "Expected test method not found: $old (nothing written)" }
  $replace = $body.TrimEnd() + [Environment]::NewLine + [Environment]::NewLine
  return $regex.Replace($data, [System.Text.RegularExpressions.MatchEvaluator]{ param($m) $replace }, 1)
}
function AddTest([string]$data, [string]$name, [string]$body) {
  if ($data.Contains('public async Task ' + $name + '()')) { return $data }
  $i = $data.LastIndexOf('}')
  if ($i -lt 1) { throw "Cannot locate closing test class brace" }
  return $data.Substring(0, $i).TrimEnd() + [Environment]::NewLine + [Environment]::NewLine + $body.TrimEnd() + [Environment]::NewLine + $data.Substring($i)
}
$topic = BackupAndRead "TopicIntegrationTests.cs"
$topicBlock = @'
    [Fact]
    public async Task RelinquishedStudentProposal_IsDeletedAndCannotBeRepublished()
    {
        using var app = new TopicTestApp(); await app.SeedAsync();
        using var student = await Login(app, "student@tests.local");
        using var lecturer = await Login(app, "lecturer@tests.local");
        using var admin = await Login(app, "admin@tests.local");
        var id = await Id(await student.PostAsJsonAsync("/api/v1/topic-proposals", Draft("Private student topic")));
        Assert.Equal(HttpStatusCode.OK, (await lecturer.PostAsync($"/api/v1/topic-proposals/{id}/approve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await student.PostAsync($"/api/v1/topics/{id}/withdraw", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/topics/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PatchAsJsonAsync($"/api/v1/topics/{id}",
            Draft("Should not be republished"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PatchAsJsonAsync($"/api/v1/topics/{id}/registration",
            new { isOpen = true })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/topics/{id}/history")).StatusCode);
    }
'@
$topic = ReplaceMethod $topic "RelinquishedStudentProposal_LecturerEditsAndPublishes_OtherStudentCanOwnIt" "RelinquishedStudentProposal_IsDeletedAndCannotBeRepublished" $topicBlock
$topicBlock = @'
    [Fact]
    public async Task DeletedSelfProposal_CannotBeSelectedByAnotherStudent()
    {
        using var app = new TopicTestApp(); await app.SeedAsync();
        using var student = await Login(app, "student@tests.local");
        using var other = await Login(app, "other@tests.local");
        using var lecturer = await Login(app, "lecturer@tests.local");
        using var admin = await Login(app, "admin@tests.local");
        var id = await Id(await student.PostAsJsonAsync("/api/v1/topic-proposals", Draft("Never published")));
        Assert.Equal(HttpStatusCode.OK, (await lecturer.PostAsync($"/api/v1/topic-proposals/{id}/approve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await student.PostAsync($"/api/v1/topics/{id}/withdraw", null)).StatusCode);
        var periodId = await Id(await admin.PostAsJsonAsync("/api/v1/registration-periods", new
        { name = "Valid period", startsAt = DateTimeOffset.UtcNow.AddDays(-1), endsAt = DateTimeOffset.UtcNow.AddDays(1), isOpen = true }));
        Assert.Equal(HttpStatusCode.NotFound,
            (await other.PostAsJsonAsync("/api/v1/topic-registrations", new { topicId = id, registrationPeriodId = periodId })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/topics/{id}")).StatusCode);
    }
'@
$topic = ReplaceMethod $topic "AdminRepublishesRelinquishedTopic_AcceptedStudentWithdraws_LecturerReopens" "DeletedSelfProposal_CannotBeSelectedByAnotherStudent" $topicBlock
$topicBlock = @'
    [Fact]
    public async Task StudentProposal_WithdrawalPermanentlyDeletesTopicAndHistories()
    {
        using var app = new TopicTestApp(); await app.SeedAsync();
        using var student = await Login(app, "student@tests.local");
        using var other = await Login(app, "other@tests.local");
        using var lecturer = await Login(app, "lecturer@tests.local");
        var id = await Id(await student.PostAsJsonAsync("/api/v1/topic-proposals", Draft("Remove this topic")));
        Assert.Equal(HttpStatusCode.OK, (await lecturer.PostAsync($"/api/v1/topic-proposals/{id}/approve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PostAsync($"/api/v1/topics/{id}/withdraw", null)).StatusCode);
        using var result = (await student.PostAsync($"/api/v1/topics/{id}/withdraw", null));
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        using var payload = (await result.Content.ReadFromJsonAsync<JsonDocument>())!;
        Assert.Equal("DELETED", payload.RootElement.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await student.PostAsync($"/api/v1/topics/{id}/withdraw", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await lecturer.GetAsync($"/api/v1/topics/{id}/history")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/topics/{id}")).StatusCode);
        using var mine = (await student.GetFromJsonAsync<JsonDocument>("/api/v1/topics/mine"))!;
        Assert.DoesNotContain(mine.RootElement.EnumerateArray(), t => t.GetProperty("id").GetGuid() == id);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudentProjectsDbContext>();
        Assert.False(await db.Topics.AnyAsync(t => t.Id == id));
        Assert.False(await db.TopicStateHistories.AnyAsync(h => h.TopicId == id));
    }
'@
$topic = ReplaceMethod $topic "StudentProposal_AutoReopensImmediately_AuthorIsNotAutomaticallyReassigned" "StudentProposal_WithdrawalPermanentlyDeletesTopicAndHistories" $topicBlock
[System.IO.File]::WriteAllText((Join-Path $testRoot "TopicIntegrationTests.cs"), $topic, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "Updated TopicIntegrationTests.cs (backup saved once)."
$lect = BackupAndRead "LecturerSupervisionIntegrationTests.cs"
$lectBlock = @'
    [Fact]
    public async Task AcceptCreatesProjectAndCancelsOtherSupervisorRequests()
    {
        using var app = new TestServer(); await app.Seed();
        using var admin = await Login(app, "v06-admin@test.local");
        using var lecturer = await Login(app, "v06-lecturer@test.local");
        using var lecturer2 = await Login(app, "v06-lecturer2@test.local");
        using var student = await Login(app, "v06-student@test.local");
        var periodId = await Period(admin);
        var firstLecturerId = await UserId(app, "v06-lecturer@test.local");
        var secondLecturerId = await UserId(app, "v06-lecturer2@test.local");
        await SetMax(lecturer, periodId, 2); await SetMax(lecturer2, periodId, 1);
        var topicId = await Proposal(student, lecturer);
        var first = await Submit(student, topicId, firstLecturerId, periodId);
        var second = await Submit(student, topicId, secondLecturerId, periodId);
        using var result = (await lecturer.PostAsync($"/api/v1/lecturer-requests/{first}/accept", null));
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        using var accepted = (await result.Content.ReadFromJsonAsync<JsonDocument>())!;
        var projectId = accepted.RootElement.GetProperty("projectId").GetGuid();
        Assert.Equal(1, accepted.RootElement.GetProperty("cancelledOtherRequests").GetInt32());
        using var list = (await student.GetFromJsonAsync<JsonDocument>("/api/v1/lecturer-requests"))!;
        var rows = list.RootElement.EnumerateArray().ToArray();
        Assert.Equal("ACCEPTED", rows.Single(r => r.GetProperty("id").GetGuid() == first).GetProperty("status").GetString());
        Assert.Equal("CANCELLED", rows.Single(r => r.GetProperty("id").GetGuid() == second).GetProperty("status").GetString());
        using var projects = (await student.GetFromJsonAsync<JsonDocument>("/api/v1/projects"))!;
        Assert.Equal(projectId, projects.RootElement.EnumerateArray().Single().GetProperty("id").GetGuid());
        Assert.Equal("REGISTERED", projects.RootElement.EnumerateArray().Single().GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await lecturer2.PostAsync($"/api/v1/lecturer-requests/{second}/accept", null)).StatusCode);
        // A fresh REGISTERED self-created project may be cancelled and deleted.
        Assert.Equal(HttpStatusCode.OK, (await student.PostAsync($"/api/v1/topics/{topicId}/withdraw", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await student.GetAsync($"/api/v1/topics/{topicId}")).StatusCode);
        using var remaining = (await student.GetFromJsonAsync<JsonDocument>($"/api/v1/lecturer-capacity?periodId={periodId}"))!;
        Assert.Equal(2, remaining.RootElement.EnumerateArray().Single(x => x.GetProperty("lecturerUserId").GetGuid() == firstLecturerId)
            .GetProperty("remaining").GetInt32());
    }
'@
$lect = ReplaceMethod $lect "AcceptCreatesProjectAndCancelsOtherSupervisorRequests" "AcceptCreatesProjectAndCancelsOtherSupervisorRequests" $lectBlock
$extra0 = @'
    [Fact]
    public async Task JointPersonalProposal_CancelPending_DeletesTopicAndRequest()
    {
        using var app = new TestServer(); await app.Seed();
        using var admin = await Login(app, "v06-admin@test.local");
        using var lecturer = await Login(app, "v06-lecturer@test.local");
        using var student = await Login(app, "v06-student@test.local");
        using var other = await Login(app, "v06-student2@test.local");
        var periodId = await Period(admin);
        var lecturerId = await UserId(app, "v06-lecturer@test.local");
        await SetMax(lecturer, periodId, 2);
        var requestId = await Combined(student, null, lecturerId, periodId);
        using var list = (await student.GetFromJsonAsync<JsonDocument>("/api/v1/lecturer-requests"))!;
        var topicId = list.RootElement.EnumerateArray().Single(r => r.GetProperty("id").GetGuid() == requestId)
            .GetProperty("topicId").GetGuid();
        Assert.Equal(HttpStatusCode.Forbidden,
            (await other.PostAsync($"/api/v1/lecturer-requests/{requestId}/cancel", null)).StatusCode);
        using var result = await student.PostAsync($"/api/v1/lecturer-requests/{requestId}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        using var payload = (await result.Content.ReadFromJsonAsync<JsonDocument>())!;
        Assert.True(payload.RootElement.GetProperty("deletedTopic").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound,
            (await lecturer.PostAsync($"/api/v1/lecturer-requests/{requestId}/accept", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/topics/{topicId}")).StatusCode);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudentProjectsDbContext>();
        Assert.False(await db.Topics.AnyAsync(t => t.Id == topicId));
        Assert.False(await db.TopicStateHistories.AnyAsync(h => h.TopicId == topicId));
        Assert.False(await db.LecturerRequests.AnyAsync(r => r.Id == requestId));
    }
'@
$lect = AddTest $lect "JointPersonalProposal_CancelPending_DeletesTopicAndRequest" $extra0
$extra1 = @'
    [Fact]
    public async Task JointPersonalProposal_CancelRevisionRequired_DeletesTopic()
    {
        using var app = new TestServer(); await app.Seed();
        using var admin = await Login(app, "v06-admin@test.local");
        using var lecturer = await Login(app, "v06-lecturer@test.local");
        using var student = await Login(app, "v06-student@test.local");
        var periodId = await Period(admin);
        var lecturerId = await UserId(app, "v06-lecturer@test.local");
        await SetMax(lecturer, periodId, 2);
        var requestId = await Combined(student, null, lecturerId, periodId);
        Assert.Equal(HttpStatusCode.OK, (await lecturer.PostAsJsonAsync(
            $"/api/v1/lecturer-requests/{requestId}/request-revision", new {reason="Update draft"})).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await student.PostAsync(
            $"/api/v1/lecturer-requests/{requestId}/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await student.PostAsJsonAsync(
            $"/api/v1/lecturer-requests/{requestId}/resubmit", new { draft = JointDraft("Retry") })).StatusCode);
    }
'@
$lect = AddTest $lect "JointPersonalProposal_CancelRevisionRequired_DeletesTopic" $extra1
$extra2 = @'
    [Fact]
    public async Task PublicTopic_CancelJointRequest_DoesNotDeleteSharedTopic()
    {
        using var app = new TestServer(); await app.Seed();
        using var admin = await Login(app, "v06-admin@test.local");
        using var lecturer = await Login(app, "v06-lecturer@test.local");
        using var student = await Login(app, "v06-student@test.local");
        var periodId = await Period(admin);
        var lecturerId = await UserId(app, "v06-lecturer@test.local");
        await SetMax(lecturer, periodId, 2);
        var topicId = await GetId(await admin.PostAsJsonAsync("/api/v1/topics", new {
            title = "Public topic", description = "Staff topic", objective = "Goal",
            expectedContent = "Result", proposedTechnology = "Vue" }));
        Assert.Equal(HttpStatusCode.OK, (await admin.PatchAsJsonAsync(
            $"/api/v1/topics/{topicId}/registration", new { isOpen = true })).StatusCode);
        var requestId = await Combined(student, topicId, lecturerId, periodId);
        using var result = await student.PostAsync($"/api/v1/lecturer-requests/{requestId}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        using var payload = (await result.Content.ReadFromJsonAsync<JsonDocument>())!;
        Assert.False(payload.RootElement.GetProperty("deletedTopic").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/v1/topics/{topicId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await lecturer.PostAsync($"/api/v1/lecturer-requests/{requestId}/accept", null)).StatusCode);
    }
'@
$lect = AddTest $lect "PublicTopic_CancelJointRequest_DoesNotDeleteSharedTopic" $extra2
$extra3 = @'
    [Fact]
    public async Task AcceptedProject_CannotCancelRequest_ButStudentCanWithdrawFreshProject()
    {
        using var app = new TestServer(); await app.Seed();
        using var admin = await Login(app, "v06-admin@test.local");
        using var lecturer = await Login(app, "v06-lecturer@test.local");
        using var student = await Login(app, "v06-student@test.local");
        var periodId = await Period(admin);
        var lecturerId = await UserId(app, "v06-lecturer@test.local");
        await SetMax(lecturer, periodId, 2);
        var requestId = await Combined(student, null, lecturerId, periodId);
        Assert.Equal(HttpStatusCode.OK, (await lecturer.PostAsync(
            $"/api/v1/lecturer-requests/{requestId}/accept", null)).StatusCode);
        using var mine = (await student.GetFromJsonAsync<JsonDocument>("/api/v1/topics/mine"))!;
        var topicId = mine.RootElement.EnumerateArray().Single().GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Conflict, (await student.PostAsync(
            $"/api/v1/lecturer-requests/{requestId}/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await student.PostAsync(
            $"/api/v1/topics/{topicId}/withdraw", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await student.GetAsync($"/api/v1/topics/{topicId}")).StatusCode);
        using var empty = (await student.GetFromJsonAsync<JsonDocument>("/api/v1/projects"))!;
        Assert.Empty(empty.RootElement.EnumerateArray());
        using var available = (await student.GetFromJsonAsync<JsonDocument>($"/api/v1/lecturer-capacity?periodId={periodId}"))!;
        Assert.Equal(2, available.RootElement.EnumerateArray().Single(x => x.GetProperty("lecturerUserId").GetGuid() == lecturerId)
            .GetProperty("remaining").GetInt32());
    }
'@
$lect = AddTest $lect "AcceptedProject_CannotCancelRequest_ButStudentCanWithdrawFreshProject" $extra3
[System.IO.File]::WriteAllText((Join-Path $testRoot "LecturerSupervisionIntegrationTests.cs"), $lect, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "Updated LecturerSupervisionIntegrationTests.cs (backup saved once)."
Write-Host "OK: v0.6.2 regression tests updated without replacing unrelated tests."
