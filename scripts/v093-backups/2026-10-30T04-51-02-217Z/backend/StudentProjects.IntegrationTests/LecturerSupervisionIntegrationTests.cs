using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StudentProjects.Domain.Entities;
using StudentProjects.Infrastructure.Persistence;
using Xunit;

namespace StudentProjects.IntegrationTests;

public sealed class LecturerSupervisionIntegrationTests
{
    private const string Password = "V06-Integration-Test-Password!";
    private sealed class TestServer : WebApplicationFactory<Program>
    {
        private readonly string name = "v06-" + Guid.NewGuid();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "Tests-Use-InMemory",
                ["Jwt:Key"] = "v06-test-only-jwt-key-at-least-32-bytes-long-2026",
                ["Jwt:Issuer"] = "StudentProjects",
                ["Jwt:Audience"] = "StudentProjects-Frontend"
            }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDbContextOptionsConfiguration<StudentProjectsDbContext>>();
                services.RemoveAll<DbContextOptions<StudentProjectsDbContext>>();
                services.AddDbContext<StudentProjectsDbContext>(options => options.UseInMemoryDatabase(name));
            });
        }
        public async Task Seed()
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<StudentProjectsDbContext>();
            await db.Database.EnsureCreatedAsync();
            foreach (var (email, role) in new[]
            {
                ("v06-admin@test.local", RoleIds.Admin),
                ("v06-lecturer@test.local", RoleIds.Lecturer),
                ("v06-lecturer2@test.local", RoleIds.Lecturer),
                ("v06-student@test.local", RoleIds.Student),
                ("v06-student2@test.local", RoleIds.Student)
            })
            {
                var user = new User { Email = email, FullName = email, RoleId = role };
                user.PasswordHash = new PasswordHasher<User>().HashPassword(user, Password);
                db.Users.Add(user);
            }
            await db.SaveChangesAsync();
        }
    }
    private static async Task<HttpClient> Login(TestServer app, string email)
    {
        var client = app.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = (await response.Content.ReadFromJsonAsync<JsonDocument>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", doc.RootElement.GetProperty("accessToken").GetString());
        return client;
    }
    private static async Task<Guid> GetId(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"Unexpected HTTP status: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        using var doc = (await response.Content.ReadFromJsonAsync<JsonDocument>())!;
        return doc.RootElement.GetProperty("id").GetGuid();
    }
    private static async Task<Guid> Period(HttpClient admin)
        => await GetId(await admin.PostAsJsonAsync("/api/v1/registration-periods", new {
            name = "v0.6 period", startsAt = DateTimeOffset.UtcNow.AddDays(-1),
            endsAt = DateTimeOffset.UtcNow.AddDays(3), isOpen = true }));
    private static async Task<Guid> Proposal(HttpClient student, HttpClient lecturer)
    {
        var topic = await GetId(await student.PostAsJsonAsync("/api/v1/topic-proposals", new {
            title = "Owned project", description = "An assigned project", objective = "Learning",
            expectedContent = "Demonstration", proposedTechnology = "Vue and C#" }));
        Assert.Equal(HttpStatusCode.OK, (await lecturer.PostAsync($"/api/v1/topic-proposals/{topic}/approve", null)).StatusCode);
        return topic;
    }
    private static async Task SetMax(HttpClient lecturer, Guid periodId, int max)
        => Assert.Equal(HttpStatusCode.OK, (await lecturer.PutAsJsonAsync("/api/v1/lecturer-capacity/me", new {
            registrationPeriodId = periodId, maxStudents = max })).StatusCode);
    private static async Task<Guid> Submit(HttpClient student, Guid topicId, Guid lecturerId, Guid periodId)
        => await GetId(await student.PostAsJsonAsync("/api/v1/lecturer-requests", new {
            topicId, lecturerUserId = lecturerId, registrationPeriodId = periodId }));
    private static async Task<Guid> UserId(TestServer app, string email)
    {
        using var scope = app.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<StudentProjectsDbContext>().Users
            .Where(x => x.Email == email).Select(x => x.Id).SingleAsync();
    }

    [Fact]
    public async Task CapacityIsGlobalAcrossPeriodsAndCannotDropBelowAssigned()
    {
        using var app = new TestServer(); await app.Seed();
        using var admin = await Login(app, "v06-admin@test.local");
        using var lecturer = await Login(app, "v06-lecturer@test.local");
        using var student = await Login(app, "v06-student@test.local");
        var periodId = await Period(admin);
        var lecturerId = await UserId(app, "v06-lecturer@test.local");
        await SetMax(lecturer, periodId, 1);
        var topicId = await Proposal(student, lecturer);
        var requestId = await Submit(student, topicId, lecturerId, periodId);
        Assert.Equal(HttpStatusCode.OK, (await lecturer.PostAsync($"/api/v1/lecturer-requests/{requestId}/accept", null)).StatusCode);
        await Choose(student, requestId);
        Assert.Equal(HttpStatusCode.Conflict, (await lecturer.PutAsJsonAsync("/api/v1/lecturer-capacity/me", new {
            registrationPeriodId = periodId, maxStudents = 0 })).StatusCode);
        using var capacities = (await student.GetFromJsonAsync<JsonDocument>($"/api/v1/lecturer-capacity?periodId={periodId}"))!;
        var mine = capacities.RootElement.EnumerateArray().First(x => x.GetProperty("lecturerUserId").GetGuid() == lecturerId);
        Assert.Equal(0, mine.GetProperty("remaining").GetInt32());
        Assert.Equal("FULL", mine.GetProperty("status").GetString());
        var period2 = await Period(admin);
        using var carried = (await student.GetFromJsonAsync<JsonDocument>($"/api/v1/lecturer-capacity?periodId={period2}"))!;
        var global = carried.RootElement.EnumerateArray().Single(x => x.GetProperty("lecturerUserId").GetGuid() == lecturerId);
        Assert.Equal(1, global.GetProperty("maxStudents").GetInt32());
        Assert.Equal(1, global.GetProperty("currentStudents").GetInt32());
        Assert.Equal(0, global.GetProperty("remaining").GetInt32());
    }

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
        Assert.Equal(HttpStatusCode.OK, (await lecturer.PostAsync($"/api/v1/lecturer-requests/{first}/accept", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await lecturer2.PostAsync($"/api/v1/lecturer-requests/{second}/accept", null)).StatusCode);
        using (var noProject = (await student.GetFromJsonAsync<JsonDocument>("/api/v1/projects"))!)
            Assert.Empty(noProject.RootElement.EnumerateArray());
        using (var waiting = (await student.GetFromJsonAsync<JsonDocument>("/api/v1/lecturer-requests"))!)
            Assert.All(waiting.RootElement.EnumerateArray(), r => Assert.Equal("OFFERED", r.GetProperty("status").GetString()));
        var projectId = await Choose(student, second);
        using var list = (await student.GetFromJsonAsync<JsonDocument>("/api/v1/lecturer-requests"))!;
        var rows = list.RootElement.EnumerateArray().ToArray();
        Assert.Equal("CANCELLED", rows.Single(r => r.GetProperty("id").GetGuid() == first).GetProperty("status").GetString());
        Assert.Equal("ACCEPTED", rows.Single(r => r.GetProperty("id").GetGuid() == second).GetProperty("status").GetString());
        using var projects = (await student.GetFromJsonAsync<JsonDocument>("/api/v1/projects"))!;
        Assert.Equal(projectId, projects.RootElement.EnumerateArray().Single().GetProperty("id").GetGuid());
        Assert.Equal(secondLecturerId, projects.RootElement.EnumerateArray().Single().GetProperty("lecturerUserId").GetGuid());
        Assert.Equal(HttpStatusCode.Conflict, (await student.PostAsync($"/api/v1/lecturer-requests/{first}/select", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await student.PostAsync($"/api/v1/topics/{topicId}/withdraw", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await student.GetAsync($"/api/v1/topics/{topicId}")).StatusCode);
        using var remaining = (await student.GetFromJsonAsync<JsonDocument>($"/api/v1/lecturer-capacity?periodId={periodId}"))!;
        Assert.Equal(1, remaining.RootElement.EnumerateArray().Single(x => x.GetProperty("lecturerUserId").GetGuid() == secondLecturerId)
            .GetProperty("remaining").GetInt32());
    }

    [Fact]
    public async Task RequestRequiresOwnedTopicAndConfiguredAvailableCapacity()
    {
        using var app = new TestServer(); await app.Seed();
        using var admin = await Login(app, "v06-admin@test.local");
        using var lecturer = await Login(app, "v06-lecturer@test.local");
        using var student = await Login(app, "v06-student@test.local");
        using var other = await Login(app, "v06-student2@test.local");
        var periodId = await Period(admin);
        var lecturerId = await UserId(app, "v06-lecturer@test.local");
        var topicId = await Proposal(student, lecturer);
        var body = new { topicId, lecturerUserId = lecturerId, registrationPeriodId = periodId };
        Assert.Equal(HttpStatusCode.Conflict, (await other.PostAsJsonAsync("/api/v1/lecturer-requests", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await student.PostAsJsonAsync("/api/v1/lecturer-requests", body)).StatusCode);
        await SetMax(lecturer, periodId, 1);
        Assert.Equal(HttpStatusCode.Created, (await student.PostAsJsonAsync("/api/v1/lecturer-requests", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await student.PostAsJsonAsync("/api/v1/lecturer-requests", body)).StatusCode);
    }

    [Fact]
    public async Task OnlyTargetLecturerCanReview_RejectRequiresReason()
    {
        using var app = new TestServer(); await app.Seed();
        using var admin = await Login(app, "v06-admin@test.local");
        using var lecturer = await Login(app, "v06-lecturer@test.local");
        using var lecturer2 = await Login(app, "v06-lecturer2@test.local");
        using var student = await Login(app, "v06-student@test.local");
        var periodId = await Period(admin);
        var lecturerId = await UserId(app, "v06-lecturer@test.local");
        await SetMax(lecturer, periodId, 2);
        var topicId = await Proposal(student, lecturer);
        var id = await Submit(student, topicId, lecturerId, periodId);
        Assert.Equal(HttpStatusCode.Forbidden, (await lecturer2.PostAsync($"/api/v1/lecturer-requests/{id}/accept", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await student.PostAsync($"/api/v1/lecturer-requests/{id}/accept", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await lecturer.PostAsJsonAsync($"/api/v1/lecturer-requests/{id}/reject", new { reason = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await lecturer.PostAsJsonAsync($"/api/v1/lecturer-requests/{id}/reject", new { reason = "Another area of expertise" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await lecturer.PostAsync($"/api/v1/lecturer-requests/{id}/accept", null)).StatusCode);
    }

    [Fact]
    public async Task CapacityFullPreventsFurtherSubmissionAndAcceptance()
    {
        using var app = new TestServer(); await app.Seed();
        using var admin = await Login(app, "v06-admin@test.local");
        using var lecturer = await Login(app, "v06-lecturer@test.local");
        using var student = await Login(app, "v06-student@test.local");
        using var student2 = await Login(app, "v06-student2@test.local");
        var periodId = await Period(admin);
        var lecturerId = await UserId(app, "v06-lecturer@test.local");
        await SetMax(lecturer, periodId, 1);
        var topic1 = await Proposal(student, lecturer);
        var topic2 = await Proposal(student2, lecturer);
        var first = await Submit(student, topic1, lecturerId, periodId);
        var second = await Submit(student2, topic2, lecturerId, periodId);
        Assert.Equal(HttpStatusCode.OK, (await lecturer.PostAsync($"/api/v1/lecturer-requests/{first}/accept", null)).StatusCode);
        await Choose(student, first);
        Assert.Equal(HttpStatusCode.Conflict, (await lecturer.PostAsync($"/api/v1/lecturer-requests/{second}/accept", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await student2.PostAsJsonAsync("/api/v1/lecturer-requests", new {
            topicId = topic2, lecturerUserId = lecturerId, registrationPeriodId = periodId })).StatusCode);
    }

    [Fact]
    public async Task StudentsAndLecturersOnlySeeTheirOwnRequestsAndProjects()
    {
        using var app = new TestServer(); await app.Seed();
        using var admin = await Login(app, "v06-admin@test.local");
        using var lecturer = await Login(app, "v06-lecturer@test.local");
        using var lecturer2 = await Login(app, "v06-lecturer2@test.local");
        using var student = await Login(app, "v06-student@test.local");
        using var student2 = await Login(app, "v06-student2@test.local");
        var periodId = await Period(admin);
        var lecturerId = await UserId(app, "v06-lecturer@test.local");
        await SetMax(lecturer, periodId, 1);
        var topicId = await Proposal(student, lecturer);
        var requestId = await Submit(student, topicId, lecturerId, periodId);
        using var unseen = (await lecturer2.GetFromJsonAsync<JsonDocument>("/api/v1/lecturer-requests"))!;
        Assert.Empty(unseen.RootElement.EnumerateArray());
        using var unseenStudent = (await student2.GetFromJsonAsync<JsonDocument>("/api/v1/lecturer-requests"))!;
        Assert.Empty(unseenStudent.RootElement.EnumerateArray());
        Assert.Equal(HttpStatusCode.OK, (await lecturer.PostAsync($"/api/v1/lecturer-requests/{requestId}/accept", null)).StatusCode);
        await Choose(student, requestId);
        using var projectList = (await student.GetFromJsonAsync<JsonDocument>("/api/v1/projects"))!;
        var projectId = projectList.RootElement.EnumerateArray().Single().GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await student2.GetAsync($"/api/v1/projects/{projectId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await lecturer2.GetAsync($"/api/v1/projects/{projectId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/v1/projects/{projectId}")).StatusCode);
    }
    private static object JointDraft(string title) => new {
        title, description = "Full description", objective = "Learning goal",
        expectedContent = "Expected work", proposedTechnology = "Vue + C#" };

    private static async Task<Guid> Combined(HttpClient student, Guid? topicId, Guid lecturerId, Guid periodId, object? draft = null)
        => await GetId(await student.PostAsJsonAsync("/api/v1/lecturer-requests/combined", new {
            topicId, lecturerUserId = lecturerId, registrationPeriodId = periodId,
            draft = draft ?? (topicId.HasValue ? null : JointDraft("New student proposal")) }));

    [Fact]
    public async Task JointProposal_Accept_GrantsOwnershipAndCreatesProject()
    {
        using var app = new TestServer(); await app.Seed();
        using var admin = await Login(app, "v06-admin@test.local");
        using var lecturer = await Login(app, "v06-lecturer@test.local");
        using var student = await Login(app, "v06-student@test.local");
        var periodId = await Period(admin);
        var lecturerId = await UserId(app, "v06-lecturer@test.local");
        await SetMax(lecturer, periodId, 1);
        var requestId = await Combined(student, null, lecturerId, periodId);
        using var before = (await student.GetFromJsonAsync<JsonDocument>("/api/v1/topics/mine"))!;
        Assert.Empty(before.RootElement.EnumerateArray());
        Assert.Equal(HttpStatusCode.OK, (await lecturer.PostAsync($"/api/v1/lecturer-requests/{requestId}/accept", null)).StatusCode);
        await Choose(student, requestId);
        using var after = (await student.GetFromJsonAsync<JsonDocument>("/api/v1/topics/mine"))!;
        Assert.Single(after.RootElement.EnumerateArray());
        Assert.Equal("APPROVED", after.RootElement.EnumerateArray().Single().GetProperty("status").GetString());
        using var project = (await student.GetFromJsonAsync<JsonDocument>("/api/v1/projects"))!;
        Assert.Single(project.RootElement.EnumerateArray());
        Assert.Equal(HttpStatusCode.Conflict,
            (await lecturer.PostAsync($"/api/v1/lecturer-requests/{requestId}/accept", null)).StatusCode);
    }

    [Fact]
    public async Task JointProposal_Revision_StudentResubmitsToSameLecturer()
    {
        using var app = new TestServer(); await app.Seed();
        using var admin = await Login(app, "v06-admin@test.local");
        using var lecturer = await Login(app, "v06-lecturer@test.local");
        using var lecturer2 = await Login(app, "v06-lecturer2@test.local");
        using var student = await Login(app, "v06-student@test.local");
        using var student2 = await Login(app, "v06-student2@test.local");
        var periodId = await Period(admin);
        var lecturerId = await UserId(app, "v06-lecturer@test.local");
        await SetMax(lecturer, periodId, 2);
        var requestId = await Combined(student, null, lecturerId, periodId);
        Assert.Equal(HttpStatusCode.Forbidden, (await lecturer2.PostAsJsonAsync(
            $"/api/v1/lecturer-requests/{requestId}/request-revision", new { reason = "Fix" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await lecturer.PostAsJsonAsync(
            $"/api/v1/lecturer-requests/{requestId}/request-revision", new { reason = "Add concrete goals" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await lecturer.PostAsync($"/api/v1/lecturer-requests/{requestId}/accept", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await student2.PostAsJsonAsync(
            $"/api/v1/lecturer-requests/{requestId}/resubmit", new { draft = JointDraft("Stolen") })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await student.PostAsJsonAsync(
            $"/api/v1/lecturer-requests/{requestId}/resubmit", new { draft = JointDraft("Revised student topic") })).StatusCode);
        using var studentView = (await student.GetFromJsonAsync<JsonDocument>("/api/v1/lecturer-requests"))!;
        var application = studentView.RootElement.EnumerateArray().Single();
        Assert.Equal("PENDING", application.GetProperty("status").GetString());
        Assert.Equal("Revised student topic", application.GetProperty("draftTitle").GetString());
        Assert.Equal(HttpStatusCode.OK, (await lecturer.PostAsync($"/api/v1/lecturer-requests/{requestId}/accept", null)).StatusCode);
        await Choose(student, requestId);
        using var projects = (await student.GetFromJsonAsync<JsonDocument>("/api/v1/projects"))!;
        Assert.Equal("Revised student topic", projects.RootElement.EnumerateArray().Single().GetProperty("topicTitle").GetString());
    }

    [Fact]
    public async Task JointApplication_PublicTopicRevision_DoesNotModifyOtherStudentsTopic()
    {
        using var app = new TestServer(); await app.Seed();
        using var admin = await Login(app, "v06-admin@test.local");
        using var lecturer = await Login(app, "v06-lecturer@test.local");
        using var student = await Login(app, "v06-student@test.local");
        using var student2 = await Login(app, "v06-student2@test.local");
        var periodId = await Period(admin);
        var lecturerId = await UserId(app, "v06-lecturer@test.local");
        await SetMax(lecturer, periodId, 2);
        var topicId = await GetId(await admin.PostAsJsonAsync("/api/v1/topics", new {
            title = "Common topic", description = "Shared topic", objective = "Original goal",
            expectedContent = "Original expected", proposedTechnology = "Original tech" }));
        Assert.Equal(HttpStatusCode.OK, (await admin.PatchAsJsonAsync($"/api/v1/topics/{topicId}/registration", new { isOpen = true })).StatusCode);
        var requestId = await Combined(student, topicId, lecturerId, periodId);
        Assert.Equal(HttpStatusCode.OK, (await lecturer.PostAsJsonAsync(
            $"/api/v1/lecturer-requests/{requestId}/request-revision", new { reason = "Change objectives" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await student.PostAsJsonAsync(
            $"/api/v1/lecturer-requests/{requestId}/resubmit", new { draft = JointDraft("Customized topic") })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await lecturer.PostAsync($"/api/v1/lecturer-requests/{requestId}/accept", null)).StatusCode);
        await Choose(student, requestId);
        using var original = (await student2.GetFromJsonAsync<JsonDocument>($"/api/v1/topics/{topicId}"))!;
        Assert.Equal("Common topic", original.RootElement.GetProperty("title").GetString());
        Assert.True(original.RootElement.GetProperty("isRegistrationOpen").GetBoolean());
        using var projects = (await student.GetFromJsonAsync<JsonDocument>("/api/v1/projects"))!;
        var item = projects.RootElement.EnumerateArray().Single();
        Assert.Equal("Customized topic", item.GetProperty("topicTitle").GetString());
        Assert.NotEqual(topicId, item.GetProperty("topicId").GetGuid());
    }

    [Fact]
    public async Task JointApplication_Reject_IsFinalAndDoesNotAssignTopic()
    {
        using var app = new TestServer(); await app.Seed();
        using var admin = await Login(app, "v06-admin@test.local");
        using var lecturer = await Login(app, "v06-lecturer@test.local");
        using var student = await Login(app, "v06-student@test.local");
        var periodId = await Period(admin);
        var lecturerId = await UserId(app, "v06-lecturer@test.local");
        await SetMax(lecturer, periodId, 1);
        var requestId = await Combined(student, null, lecturerId, periodId);
        Assert.Equal(HttpStatusCode.OK, (await lecturer.PostAsJsonAsync(
            $"/api/v1/lecturer-requests/{requestId}/reject", new { reason = "Not suitable" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await student.PostAsJsonAsync(
            $"/api/v1/lecturer-requests/{requestId}/resubmit", new { draft = JointDraft("Retry") })).StatusCode);
        using var projects = (await student.GetFromJsonAsync<JsonDocument>("/api/v1/projects"))!;
        Assert.Empty(projects.RootElement.EnumerateArray());
        using var mine = (await student.GetFromJsonAsync<JsonDocument>("/api/v1/topics/mine"))!;
        Assert.Empty(mine.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task JointApplication_TwoStudentsCompeteForTopic_OnlyOneMayWin()
    {
        using var app = new TestServer(); await app.Seed();
        using var admin = await Login(app, "v06-admin@test.local");
        using var lecturer = await Login(app, "v06-lecturer@test.local");
        using var student = await Login(app, "v06-student@test.local");
        using var student2 = await Login(app, "v06-student2@test.local");
        var periodId = await Period(admin);
        var lecturerId = await UserId(app, "v06-lecturer@test.local");
        await SetMax(lecturer, periodId, 2);
        var topicId = await GetId(await admin.PostAsJsonAsync("/api/v1/topics", new {
            title = "Shared existing topic", description = "Can be picked by both", objective = "Goal",
            expectedContent = "Deliver", proposedTechnology = "Tech" }));
        Assert.Equal(HttpStatusCode.OK, (await admin.PatchAsJsonAsync($"/api/v1/topics/{topicId}/registration", new { isOpen = true })).StatusCode);
        var first = await Combined(student, topicId, lecturerId, periodId);
        var second = await Combined(student2, topicId, lecturerId, periodId);
        Assert.Equal(HttpStatusCode.OK, (await lecturer.PostAsync($"/api/v1/lecturer-requests/{first}/accept", null)).StatusCode);
        await Choose(student, first);
        // The second applicant must not receive the topic when it is already assigned.
        var outcome = await lecturer.PostAsync($"/api/v1/lecturer-requests/{second}/accept", null);
        Assert.True(outcome.StatusCode == HttpStatusCode.Conflict || outcome.StatusCode == HttpStatusCode.NotFound);
        using var projects = (await admin.GetFromJsonAsync<JsonDocument>("/api/v1/projects"))!;
        Assert.Single(projects.RootElement.EnumerateArray());
    }

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
        await Choose(student, requestId);
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

    private static async Task<Guid> Choose(HttpClient student, Guid requestId)
    {
        using var response = await student.PostAsync($"/api/v1/lecturer-requests/{requestId}/select", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = (await response.Content.ReadFromJsonAsync<JsonDocument>())!;
        Assert.Equal("ACCEPTED", json.RootElement.GetProperty("status").GetString());
        return json.RootElement.GetProperty("projectId").GetGuid();
    }

    [Fact]
    public async Task StudentCannotSelectAnotherStudentsOfferOrUnapprovedRequest()
    {
        using var app = new TestServer(); await app.Seed();
        using var admin = await Login(app, "v06-admin@test.local");
        using var lecturer = await Login(app, "v06-lecturer@test.local");
        using var student = await Login(app, "v06-student@test.local");
        using var stranger = await Login(app, "v06-student2@test.local");
        var period = await Period(admin);
        var lecturerId = await UserId(app, "v06-lecturer@test.local");
        await SetMax(lecturer, period, 2);
        var request = await Combined(student, null, lecturerId, period);
        Assert.Equal(HttpStatusCode.Conflict, (await student.PostAsync($"/api/v1/lecturer-requests/{request}/select", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.PostAsync($"/api/v1/lecturer-requests/{request}/select", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await lecturer.PostAsync($"/api/v1/lecturer-requests/{request}/accept", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.PostAsync($"/api/v1/lecturer-requests/{request}/select", null)).StatusCode);
        await Choose(student, request);
        Assert.Equal(HttpStatusCode.Conflict, (await student.PostAsync($"/api/v1/lecturer-requests/{request}/select", null)).StatusCode);
    }

    [Fact]
    public async Task SelfProposedTopicCanReceiveTwoOffersBeforeStudentChooses()
    {
        using var app = new TestServer(); await app.Seed();
        using var admin = await Login(app, "v06-admin@test.local");
        using var lecturer = await Login(app, "v06-lecturer@test.local");
        using var lecturer2 = await Login(app, "v06-lecturer2@test.local");
        using var student = await Login(app, "v06-student@test.local");
        var period = await Period(admin);
        var l1 = await UserId(app, "v06-lecturer@test.local");
        var l2 = await UserId(app, "v06-lecturer2@test.local");
        await SetMax(lecturer, period, 10); await SetMax(lecturer2, period, 10);
        var first = await Combined(student, null, l1, period);
        using var rows = (await student.GetFromJsonAsync<JsonDocument>("/api/v1/lecturer-requests"))!;
        var topicId = rows.RootElement.EnumerateArray().Single().GetProperty("topicId").GetGuid();
        var second = await Combined(student, topicId, l2, period);
        Assert.Equal(HttpStatusCode.OK, (await lecturer.PostAsync($"/api/v1/lecturer-requests/{first}/accept", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await lecturer2.PostAsync($"/api/v1/lecturer-requests/{second}/accept", null)).StatusCode);
        using (var before = (await student.GetFromJsonAsync<JsonDocument>("/api/v1/topics/mine"))!)
            Assert.Empty(before.RootElement.EnumerateArray());
        await Choose(student, second);
        using var after = (await student.GetFromJsonAsync<JsonDocument>("/api/v1/lecturer-requests"))!;
        Assert.Equal("CANCELLED", after.RootElement.EnumerateArray().Single(r => r.GetProperty("id").GetGuid() == first).GetProperty("status").GetString());
        using var projects = (await student.GetFromJsonAsync<JsonDocument>("/api/v1/projects"))!;
        Assert.Equal(l2, projects.RootElement.EnumerateArray().Single().GetProperty("lecturerUserId").GetGuid());
        Assert.Equal(topicId, projects.RootElement.EnumerateArray().Single().GetProperty("topicId").GetGuid());
    }

    [Fact]
    public async Task PeriodTwoInheritsCapacityAndCountsPeriodOneProjects()
    {
        using var app = new TestServer(); await app.Seed();
        using var admin = await Login(app, "v06-admin@test.local");
        using var lecturer = await Login(app, "v06-lecturer@test.local");
        using var student = await Login(app, "v06-student@test.local");
        var firstPeriod = await Period(admin);
        var lecturerId = await UserId(app, "v06-lecturer@test.local");
        await SetMax(lecturer, firstPeriod, 10);
        var topicId = await Proposal(student, lecturer);
        var request = await Submit(student, topicId, lecturerId, firstPeriod);
        Assert.Equal(HttpStatusCode.OK, (await lecturer.PostAsync($"/api/v1/lecturer-requests/{request}/accept", null)).StatusCode);
        await Choose(student, request);
        var secondPeriod = await Period(admin);
        using var capacities = (await lecturer.GetFromJsonAsync<JsonDocument>($"/api/v1/lecturer-capacity?periodId={secondPeriod}"))!;
        var value = capacities.RootElement.EnumerateArray().Single(c => c.GetProperty("lecturerUserId").GetGuid() == lecturerId);
        Assert.Equal(10, value.GetProperty("maxStudents").GetInt32());
        Assert.Equal(1, value.GetProperty("currentStudents").GetInt32());
        Assert.Equal(9, value.GetProperty("remaining").GetInt32());
        await SetMax(lecturer, secondPeriod, 10);
        using var repeated = (await lecturer.GetFromJsonAsync<JsonDocument>($"/api/v1/lecturer-capacity?periodId={firstPeriod}"))!;
        Assert.Equal(9, repeated.RootElement.EnumerateArray().Single(c => c.GetProperty("lecturerUserId").GetGuid() == lecturerId)
            .GetProperty("remaining").GetInt32());
    }
}
