// V090_REGRESSION_TESTS
// Integration tests: real JWT endpoints + EF Core InMemory. No connection to StudentProjectsDev.
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
using Microsoft.Extensions.Hosting;
using StudentProjects.Domain.Entities;
using StudentProjects.Domain.Enums;
using StudentProjects.Infrastructure.Persistence;
using Xunit;

namespace StudentProjects.IntegrationTests;

public sealed class V09SecurityAndProjectLifecycleTests
{
    private const string Password = "V09-Testing-Only!123";
    private const string JwtKey = "V09-INTEGRATION-JWT-KEY-NOT-FOR-PRODUCTION-2026-STRONG-LENGTH";
    private static readonly Guid StudentA = Guid.Parse("90a00000-0000-4000-8000-000000000001");
    private static readonly Guid StudentB = Guid.Parse("90a00000-0000-4000-8000-000000000002");
    private static readonly Guid LecturerA = Guid.Parse("90a00000-0000-4000-8000-000000000003");
    private static readonly Guid LecturerB = Guid.Parse("90a00000-0000-4000-8000-000000000004");
    private static readonly Guid Admin = Guid.Parse("90a00000-0000-4000-8000-000000000005");
    private static readonly Guid ProjectA = Guid.Parse("90b00000-0000-4000-8000-000000000001");
    private static readonly Guid ProjectB = Guid.Parse("90b00000-0000-4000-8000-000000000002");
    private static readonly Guid MilestoneA = Guid.Parse("90c00000-0000-4000-8000-000000000001");
    private static readonly Guid MilestoneB = Guid.Parse("90c00000-0000-4000-8000-000000000002");
    private static readonly Guid NotificationA = Guid.Parse("90d00000-0000-4000-8000-000000000001");
    private static readonly Guid NotificationB = Guid.Parse("90d00000-0000-4000-8000-000000000002");
    private static readonly Guid GuidanceA = Guid.Parse("90e00000-0000-4000-8000-000000000001");

    private sealed class TestApp : WebApplicationFactory<Program>
    {
        private readonly string _database = $"v09-regression-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Jwt:Key"] = JwtKey,
                    ["Jwt:Issuer"] = "StudentProjects",
                    ["Jwt:Audience"] = "StudentProjects-Frontend",
                    ["ConnectionStrings:Default"] = "Testing-Not-Used"
                }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDbContextOptionsConfiguration<StudentProjectsDbContext>>();
                services.RemoveAll<DbContextOptions<StudentProjectsDbContext>>();
                services.AddDbContext<StudentProjectsDbContext>(options => options.UseInMemoryDatabase(_database));
                // Prevent the scheduled notification worker from racing test setup; the
                // notification endpoint itself can still refresh events on demand.
                foreach (var descriptor in services.Where(d =>
                    d.ServiceType == typeof(IHostedService) &&
                    d.ImplementationType?.Name == "V08NotifierWorker").ToArray())
                    services.Remove(descriptor);
            });
        }

        public async Task SeedAsync()
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<StudentProjectsDbContext>();
            await db.Database.EnsureCreatedAsync();
            AddUser(db, StudentA, "student-a@v09.test", RoleIds.Student);
            AddUser(db, StudentB, "student-b@v09.test", RoleIds.Student);
            AddUser(db, LecturerA, "lecturer-a@v09.test", RoleIds.Lecturer);
            AddUser(db, LecturerB, "lecturer-b@v09.test", RoleIds.Lecturer);
            AddUser(db, Admin, "admin@v09.test", RoleIds.Admin);

            var periodId = Guid.NewGuid();
            db.RegistrationPeriods.Add(new RegistrationPeriod
            {
                Id = periodId, Name = "V09 isolated test", IsOpen = true,
                StartsAt = DateTimeOffset.UtcNow.AddDays(-30),
                EndsAt = DateTimeOffset.UtcNow.AddDays(30)
            });
            var topicA = Guid.NewGuid();
            var topicB = Guid.NewGuid();
            db.Topics.AddRange(
                new Topic { Id = topicA, Title = "Topic A", Description = "A", ProposedByUserId = LecturerA,
                    Status = TopicStatus.APPROVED, IsRegistrationOpen = true },
                new Topic { Id = topicB, Title = "Topic B", Description = "B", ProposedByUserId = LecturerB,
                    Status = TopicStatus.APPROVED, IsRegistrationOpen = true });
            db.Projects.AddRange(
                new Project { Id = ProjectA, TopicId = topicA, RegistrationPeriodId = periodId,
                    StudentUserId = StudentA, LecturerUserId = LecturerA, Status = ProjectStatus.IN_PROGRESS },
                new Project { Id = ProjectB, TopicId = topicB, RegistrationPeriodId = periodId,
                    StudentUserId = StudentB, LecturerUserId = LecturerB, Status = ProjectStatus.IN_PROGRESS });
            db.Milestones.AddRange(
                new Milestone { Id = MilestoneA, ProjectId = ProjectA, Title = "Milestone A",
                    StartAt = DateTimeOffset.UtcNow.AddDays(-1), DeadlineAt = DateTimeOffset.UtcNow.AddDays(30),
                    ProposalStatus = "CONFIRMED", Status = MilestoneStatus.PENDING },
                new Milestone { Id = MilestoneB, ProjectId = ProjectB, Title = "Milestone B",
                    StartAt = DateTimeOffset.UtcNow.AddDays(-1), DeadlineAt = DateTimeOffset.UtcNow.AddDays(30),
                    ProposalStatus = "CONFIRMED", Status = MilestoneStatus.PENDING });
            db.Notifications.AddRange(
                new Notification { Id = NotificationA, RecipientUserId = StudentA,
                    Type = $"V08:DUE:{MilestoneA:N}:20261231:7", Message = "Student A only" },
                new Notification { Id = NotificationB, RecipientUserId = StudentB,
                    Type = $"V08:DUE:{MilestoneB:N}:20261231:7", Message = "Student B only" });
            db.GuidanceSessions.Add(new GuidanceSession
            {
                Id = GuidanceA, ProjectId = ProjectA, CreatedByUserId = StudentA,
                MeetingAt = DateTimeOffset.UtcNow.AddDays(-1), Summary = "V09 guidance", Feedback = "",
                NextActions = "Next step"
            });
            await db.SaveChangesAsync();
        }

        private static void AddUser(StudentProjectsDbContext db, Guid id, string email, Guid role)
        {
            var user = new User { Id = id, FullName = email, Email = email, RoleId = role, IsActive = true };
            user.PasswordHash = new PasswordHasher<User>().HashPassword(user, Password);
            db.Users.Add(user);
            if (role == RoleIds.Student)
                db.Students.Add(new StudentProfile { UserId = id, StudentCode = $"ST-{id.ToString("N")[^6..]}" });
            if (role == RoleIds.Lecturer)
                db.Lecturers.Add(new LecturerProfile { UserId = id });
        }
    }

    private static async Task<HttpClient> Login(TestApp app, string email)
    {
        var client = app.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = (await response.Content.ReadFromJsonAsync<JsonDocument>())!;
        var jwt = json.RootElement.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrEmpty(jwt));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        return client;
    }

    [Fact]
    public async Task AnonymousRequestsToProjectAndNotifications_AreRejected()
    {
        using var app = new TestApp();
        using var client = app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/v1/v07/projects")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/v1/v08/notifications")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync($"/api/v1/v08/projects/{ProjectA}/guidance")).StatusCode);
    }

    [Fact]
    public async Task StudentAndLecturerProjectLists_AreScoped_AdminCanSeeBoth()
    {
        using var app = new TestApp();
        await app.SeedAsync();
        using var a = await Login(app, "student-a@v09.test");
        using var b = await Login(app, "student-b@v09.test");
        using var lecturer = await Login(app, "lecturer-a@v09.test");
        using var admin = await Login(app, "admin@v09.test");
        await AssertProjects(a, ProjectA);
        await AssertProjects(b, ProjectB);
        await AssertProjects(lecturer, ProjectA);
        await AssertProjects(admin, ProjectA, ProjectB);
    }

    private static async Task AssertProjects(HttpClient client, params Guid[] expected)
    {
        using var json = (await client.GetFromJsonAsync<JsonDocument>("/api/v1/v07/projects"))!;
        var ids = json.RootElement.EnumerateArray().Select(e => e.GetProperty("id").GetGuid()).ToHashSet();
        Assert.Equal(expected.OrderBy(x => x), ids.OrderBy(x => x));
    }

    [Fact]
    public async Task ProjectMilestonesAndGuidance_CannotBeReadOrModifiedAcrossUsers()
    {
        using var app = new TestApp();
        await app.SeedAsync();
        using var otherStudent = await Login(app, "student-b@v09.test");
        using var otherLecturer = await Login(app, "lecturer-b@v09.test");
        using var ownerStudent = await Login(app, "student-a@v09.test");
        using var ownerLecturer = await Login(app, "lecturer-a@v09.test");
        using var admin = await Login(app, "admin@v09.test");
        var milestonesUrl = $"/api/v1/v07/projects/{ProjectA}/milestones";
        var guidanceUrl = $"/api/v1/v08/projects/{ProjectA}/guidance";
        var payload = new { meetingAt = DateTimeOffset.UtcNow, summary = "Illegal operation", feedback = "", nextActions = "" };

        Assert.Equal(HttpStatusCode.Forbidden, (await otherStudent.GetAsync(milestonesUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await otherLecturer.GetAsync(milestonesUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await otherStudent.GetAsync($"/api/v1/v07/milestones/{MilestoneA}/history")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await otherStudent.GetAsync(guidanceUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await otherLecturer.GetAsync(guidanceUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await otherStudent.PostAsJsonAsync(guidanceUrl, payload)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await otherLecturer.PostAsJsonAsync(guidanceUrl, payload)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await admin.PostAsJsonAsync(guidanceUrl, payload)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await otherLecturer.PostAsync($"/api/v1/v08/guidance/{GuidanceA}/confirm", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await ownerStudent.PostAsync($"/api/v1/v08/guidance/{GuidanceA}/confirm", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await admin.PostAsync($"/api/v1/v08/guidance/{GuidanceA}/confirm", null)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await ownerStudent.GetAsync(guidanceUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ownerLecturer.GetAsync(guidanceUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(guidanceUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await ownerLecturer.PostAsync($"/api/v1/v08/guidance/{GuidanceA}/confirm", null)).StatusCode);
    }

    [Fact]
    public async Task NotificationReadAndMarkRead_IsRecipientOnly()
    {
        using var app = new TestApp();
        await app.SeedAsync();
        using var studentA = await Login(app, "student-a@v09.test");
        using var studentB = await Login(app, "student-b@v09.test");
        using (var json = (await studentA.GetFromJsonAsync<JsonDocument>("/api/v1/v08/notifications"))!)
        {
            var items = json.RootElement.GetProperty("items").EnumerateArray().ToList();
            Assert.Contains(items, e => e.GetProperty("id").GetGuid() == NotificationA);
            Assert.DoesNotContain(items, e => e.GetProperty("id").GetGuid() == NotificationB);
        }
        Assert.Equal(HttpStatusCode.NotFound,
            (await studentA.PostAsync($"/api/v1/v08/notifications/{NotificationB}/read", null)).StatusCode);
        // EF Core InMemory cannot translate ExecuteUpdateAsync used by read-all;
        // test the single-item endpoint here and cover read-all in manual SQL QA.
        Assert.Equal(HttpStatusCode.NoContent,
            (await studentA.PostAsync($"/api/v1/v08/notifications/{NotificationA}/read", null)).StatusCode);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudentProjectsDbContext>();
        Assert.True((await db.Notifications.SingleAsync(n => n.Id == NotificationA)).IsRead);
        Assert.False((await db.Notifications.SingleAsync(n => n.Id == NotificationB)).IsRead);
        Assert.Equal(HttpStatusCode.NoContent,
            (await studentB.PostAsync($"/api/v1/v08/notifications/{NotificationB}/read", null)).StatusCode);
    }

    [Fact]
    public async Task StudentSubmission_LecturerReview_ProjectCompletes_WithoutCrossRoleWrites()
    {
        using var app = new TestApp();
        await app.SeedAsync();
        using var student = await Login(app, "student-a@v09.test");
        using var lecturer = await Login(app, "lecturer-a@v09.test");
        using var outsider = await Login(app, "student-b@v09.test");
        using var admin = await Login(app, "admin@v09.test");
        var progressUrl = $"/api/v1/v07/milestones/{MilestoneA}/progress";
        var submitUrl = $"/api/v1/v07/milestones/{MilestoneA}/submit";
        var reviewUrl = $"/api/v1/v07/milestones/{MilestoneA}/review";
        var progress = new { percentComplete = 65, workDone = "Implemented feature A", note = "Tests pending" };
        Assert.Equal(HttpStatusCode.Forbidden,
            (await outsider.PostAsJsonAsync(progressUrl, progress)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await admin.PostAsJsonAsync(progressUrl, progress)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await student.PostAsJsonAsync(progressUrl, progress)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await lecturer.PostAsJsonAsync(submitUrl,
                new { resultSummary = "Unauthorized submit", documentReference = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await student.PostAsJsonAsync(submitUrl,
                new { resultSummary = "Feature A completed", documentReference = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await student.PostAsJsonAsync(reviewUrl,
                new { isApproved = true, feedback = "Self approval" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await admin.PostAsJsonAsync(reviewUrl,
                new { isApproved = true, feedback = "Admin approval" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await lecturer.PostAsJsonAsync(reviewUrl,
                new { isApproved = true, feedback = "Passed" })).StatusCode);

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudentProjectsDbContext>();
        var milestone = await db.Milestones.AsNoTracking().SingleAsync(m => m.Id == MilestoneA);
        var project = await db.Projects.AsNoTracking().SingleAsync(p => p.Id == ProjectA);
        Assert.Equal(MilestoneStatus.APPROVED, milestone.Status);
        Assert.Equal(100, milestone.PercentComplete);
        Assert.Equal(ProjectStatus.COMPLETED, project.Status);
        Assert.Equal(1, await db.MilestoneSubmissions.CountAsync(s => s.MilestoneId == MilestoneA));
        Assert.Equal(1, await db.ProjectEvaluations.CountAsync(r => r.MilestoneId == MilestoneA));
        Assert.Equal(HttpStatusCode.Conflict,
            (await student.PostAsJsonAsync(submitUrl,
                new { resultSummary = "Should not resubmit approved milestone", documentReference = "" })).StatusCode);
    }
}
