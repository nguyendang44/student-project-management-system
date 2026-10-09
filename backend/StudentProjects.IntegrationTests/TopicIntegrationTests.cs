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

public sealed class TopicIntegrationTests
{
    private const string Password = "SecureTopicTests!2026";
    private const string JwtKey = "TEST-ONLY-TOPIC-JWT-2026-LONGER-THAN-32-BYTES-KEY";
    private sealed class TopicTestApp : WebApplicationFactory<Program>
    {
        private readonly string databaseName = "topic-tests-" + Guid.NewGuid();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = "Testing-Not-Used",
                    ["Jwt:Key"] = JwtKey,
                    ["Jwt:Issuer"] = "StudentProjects",
                    ["Jwt:Audience"] = "StudentProjects-Frontend"
                }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDbContextOptionsConfiguration<StudentProjectsDbContext>>();
                services.RemoveAll<DbContextOptions<StudentProjectsDbContext>>();
                services.AddDbContext<StudentProjectsDbContext>(opts => opts.UseInMemoryDatabase(databaseName));
            });
        }
        public async Task SeedAsync()
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<StudentProjectsDbContext>();
            await db.Database.EnsureCreatedAsync();
            foreach (var (email, role) in new[] { ("admin@tests.local", RoleIds.Admin),
                     ("lecturer@tests.local", RoleIds.Lecturer), ("otherlecturer@tests.local", RoleIds.Lecturer), ("student@tests.local", RoleIds.Student),
                     ("other@tests.local", RoleIds.Student) })
            {
                var user = new User { FullName = email, Email = email, RoleId = role };
                user.PasswordHash = new PasswordHasher<User>().HashPassword(user, Password);
                db.Users.Add(user);
            }
            await db.SaveChangesAsync();
        }
    }
    private static async Task<HttpClient> Login(TopicTestApp app, string email)
    {
        var client = app.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = (await response.Content.ReadFromJsonAsync<JsonDocument>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.RootElement.GetProperty("accessToken").GetString());
        return client;
    }
    private static object Draft(string title) => new { title, description = "Design, research and implementation",
        objective = "Implement an academic project", expectedContent = "Architecture and demonstration",
        proposedTechnology = "C#, Vue" };
    private static async Task<Guid> Id(HttpResponseMessage response)
    {
        using var json = (await response.Content.ReadFromJsonAsync<JsonDocument>())!;
        return json.RootElement.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task AdminCanDeleteUnregisteredTopicsRegardlessOfCreator()
    {
        using var app = new TopicTestApp(); await app.SeedAsync();
        using var admin = await Login(app, "admin@tests.local");
        using var lecturer = await Login(app, "lecturer@tests.local");
        using var student = await Login(app, "student@tests.local");
        foreach (var (creator, label) in new[] {
            (admin, "System or Admin topic"), (lecturer, "Lecturer topic"), (student, "Student draft") })
        {
            var topicId = await Id(await creator.PostAsJsonAsync("/api/v1/topics", Draft(label)));
            using var info = (await admin.GetFromJsonAsync<JsonDocument>($"/api/v1/topics/{topicId}"))!;
            Assert.True(info.RootElement.GetProperty("canDelete").GetBoolean());
            Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/topics/{topicId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/topics/{topicId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"/api/v1/topics/{topicId}")).StatusCode);
        }
    }

    [Fact]
    public async Task LecturerCanDeleteOwnTopicButNotOtherCreatorsTopics()
    {
        using var app = new TopicTestApp(); await app.SeedAsync();
        using var admin = await Login(app, "admin@tests.local");
        using var lecturer = await Login(app, "lecturer@tests.local");
        using var otherLecturer = await Login(app, "otherlecturer@tests.local");
        using var student = await Login(app, "student@tests.local");
        var own = await Id(await lecturer.PostAsJsonAsync("/api/v1/topics", Draft("Lecturer-owned")));
        var other = await Id(await otherLecturer.PostAsJsonAsync("/api/v1/topics", Draft("Other lecturer-owned")));
        var studentTopic = await Id(await student.PostAsJsonAsync("/api/v1/topics", Draft("Student draft")));
        var adminTopic = await Id(await admin.PostAsJsonAsync("/api/v1/topics", Draft("Admin topic")));
        Assert.Equal(HttpStatusCode.Forbidden, (await student.DeleteAsync($"/api/v1/topics/{own}")).StatusCode);
        foreach (var id in new[] {other, studentTopic, adminTopic})
            Assert.Equal(HttpStatusCode.Forbidden, (await lecturer.DeleteAsync($"/api/v1/topics/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await lecturer.DeleteAsync($"/api/v1/topics/{own}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/v1/topics/{other}")).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeletingUnassignedTopic_RemovesAllRegistrationHistory(bool cancel)
    {
        using var app = new TopicTestApp(); await app.SeedAsync();
        using var admin = await Login(app, "admin@tests.local");
        using var lecturer = await Login(app, "lecturer@tests.local");
        using var student = await Login(app, "student@tests.local");
        var topicId = await Id(await lecturer.PostAsJsonAsync("/api/v1/topics", Draft("Registration history")));
        var periodId = await Id(await admin.PostAsJsonAsync("/api/v1/registration-periods", new {
            name = "Active", startsAt = DateTimeOffset.UtcNow.AddDays(-1),
            endsAt = DateTimeOffset.UtcNow.AddDays(1), isOpen = true }));
        Assert.Equal(HttpStatusCode.OK,
            (await lecturer.PatchAsJsonAsync($"/api/v1/topics/{topicId}/registration", new {isOpen=true})).StatusCode);
        var registrationId = await Id(await student.PostAsJsonAsync("/api/v1/topic-registrations", new { topicId, registrationPeriodId = periodId }));
        if (cancel)
            Assert.Equal(HttpStatusCode.OK, (await student.PostAsync($"/api/v1/topic-registrations/{registrationId}/cancel", null)).StatusCode);
        using var info = (await admin.GetFromJsonAsync<JsonDocument>($"/api/v1/topics/{topicId}"))!;
        Assert.True(info.RootElement.GetProperty("canDelete").GetBoolean());
        Assert.Equal(HttpStatusCode.NoContent, (await lecturer.DeleteAsync($"/api/v1/topics/{topicId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"/api/v1/topics/{topicId}")).StatusCode);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudentProjectsDbContext>();
        Assert.False(await db.TopicRegistrations.AnyAsync(r => r.TopicId == topicId));
        Assert.False(await db.TopicStateHistories.AnyAsync(h => h.TopicId == topicId));
        Assert.False(await db.Topics.AnyAsync(t => t.Id == topicId));
        using var requests = (await student.GetFromJsonAsync<JsonDocument>("/api/v1/topic-registrations"))!;
        Assert.DoesNotContain(requests.RootElement.EnumerateArray(), r => r.GetProperty("id").GetGuid() == registrationId);
    }

    [Fact]
    public async Task ApprovedStudentProposalIsAssignedAndCannotBeDeleted()
    {
        using var app = new TopicTestApp(); await app.SeedAsync();
        using var admin = await Login(app, "admin@tests.local");
        using var student = await Login(app, "student@tests.local");
        using var lecturer = await Login(app, "lecturer@tests.local");
        var id = await Id(await student.PostAsJsonAsync("/api/v1/topic-proposals", Draft("Owned proposal")));
        Assert.Equal(HttpStatusCode.OK, (await lecturer.PostAsync($"/api/v1/topic-proposals/{id}/approve", null)).StatusCode);
        using var info = (await admin.GetFromJsonAsync<JsonDocument>($"/api/v1/topics/{id}"))!;
        Assert.False(info.RootElement.GetProperty("canDelete").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/v1/topics/{id}")).StatusCode);
    }

    [Fact]
    public async Task StudentProposal_LecturerRejects_StudentResubmits_LecturerApproves()
    {
        using var app = new TopicTestApp(); await app.SeedAsync();
        using var student = await Login(app, "student@tests.local");
        using var lecturer = await Login(app, "lecturer@tests.local");
        var create = await student.PostAsJsonAsync("/api/v1/topic-proposals", Draft("AI Supported Project"));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var id = await Id(create);
        Assert.Equal(HttpStatusCode.BadRequest, (await lecturer.PostAsJsonAsync($"/api/v1/topic-proposals/{id}/reject", new { reason = "" })).StatusCode);
        var rejected = await lecturer.PostAsJsonAsync($"/api/v1/topic-proposals/{id}/reject", new { reason = "Needs revision" });
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        using (var rejectedBody = (await rejected.Content.ReadFromJsonAsync<JsonDocument>())!)
            Assert.Equal("REJECTED", rejectedBody.RootElement.GetProperty("status").GetString());
        using (var studentView = (await student.GetFromJsonAsync<JsonDocument>($"/api/v1/topics/{id}"))!)
            Assert.Equal("REJECTED", studentView.RootElement.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await lecturer.PostAsync($"/api/v1/topic-proposals/{id}/approve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await student.PatchAsJsonAsync($"/api/v1/topics/{id}", Draft("Improved AI Project"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await student.PostAsync($"/api/v1/topic-proposals/{id}/submit", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await lecturer.PostAsync($"/api/v1/topic-proposals/{id}/approve", null)).StatusCode);
        using var final = (await student.GetFromJsonAsync<JsonDocument>($"/api/v1/topics/{id}"))!;
        Assert.Equal("APPROVED", final.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task RoleAndOwnerAreCheckedOnServer()
    {
        using var app = new TopicTestApp(); await app.SeedAsync();
        using var admin = await Login(app, "admin@tests.local");
        using var student = await Login(app, "student@tests.local");
        using var other = await Login(app, "other@tests.local");
        var create = await student.PostAsJsonAsync("/api/v1/topics", Draft("Private Draft"));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var id = await Id(create);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/topics/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PatchAsJsonAsync($"/api/v1/topics/{id}", Draft("Hacked"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await student.PostAsJsonAsync("/api/v1/registration-periods", new
        { name = "Fall", startsAt = DateTimeOffset.UtcNow.AddDays(-1), endsAt = DateTimeOffset.UtcNow.AddDays(2), isOpen = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/api/v1/registration-periods", new
        { name = "Fall", startsAt = DateTimeOffset.UtcNow.AddDays(-1), endsAt = DateTimeOffset.UtcNow.AddDays(2), isOpen = true })).StatusCode);
    }

    [Fact]
    public async Task StudentRegister_CannotDuplicate_CanCancelAndReregister()
    {
        using var app = new TopicTestApp(); await app.SeedAsync();
        using var admin = await Login(app, "admin@tests.local");
        using var student = await Login(app, "student@tests.local");
        var topic = await admin.PostAsJsonAsync("/api/v1/topics", Draft("Cloud Computing"));
        var topicId = await Id(topic);
        var period = await admin.PostAsJsonAsync("/api/v1/registration-periods", new
        { name = "Open Period", startsAt = DateTimeOffset.UtcNow.AddDays(-1), endsAt = DateTimeOffset.UtcNow.AddDays(1), isOpen = true });
        var periodId = await Id(period);
        Assert.Equal(HttpStatusCode.Conflict, (await student.PostAsJsonAsync("/api/v1/topic-registrations", new { topicId, registrationPeriodId = periodId })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PatchAsJsonAsync($"/api/v1/topics/{topicId}/registration", new { isOpen = true })).StatusCode);
        var reg = await student.PostAsJsonAsync("/api/v1/topic-registrations", new { topicId, registrationPeriodId = periodId });
        Assert.Equal(HttpStatusCode.Created, reg.StatusCode);
        var registrationId = await Id(reg);
        Assert.Equal(HttpStatusCode.Conflict, (await student.PostAsJsonAsync("/api/v1/topic-registrations", new { topicId, registrationPeriodId = periodId })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await student.PostAsync($"/api/v1/topic-registrations/{registrationId}/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await student.PostAsJsonAsync("/api/v1/topic-registrations", new { topicId, registrationPeriodId = periodId })).StatusCode);
    }

    [Fact]
    public async Task ClosedPeriodBlocksRegistrationAndOnlyAdminCanOpen()
    {
        using var app = new TopicTestApp(); await app.SeedAsync();
        using var admin = await Login(app, "admin@tests.local");
        using var student = await Login(app, "student@tests.local");
        var topicId = await Id(await admin.PostAsJsonAsync("/api/v1/topics", Draft("IoT Topic")));
        Assert.Equal(HttpStatusCode.Forbidden, (await student.PatchAsJsonAsync($"/api/v1/topics/{topicId}/registration", new { isOpen = true })).StatusCode);
        await admin.PatchAsJsonAsync($"/api/v1/topics/{topicId}/registration", new { isOpen = true });
        var periodId = await Id(await admin.PostAsJsonAsync("/api/v1/registration-periods", new
        { name = "Closed", startsAt = DateTimeOffset.UtcNow.AddDays(-1), endsAt = DateTimeOffset.UtcNow.AddDays(2), isOpen = false }));
        Assert.Equal(HttpStatusCode.Conflict, (await student.PostAsJsonAsync("/api/v1/topic-registrations", new { topicId, registrationPeriodId = periodId })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PatchAsJsonAsync($"/api/v1/registration-periods/{periodId}/status", new { isOpen = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await student.PostAsJsonAsync("/api/v1/topic-registrations", new { topicId, registrationPeriodId = periodId })).StatusCode);
    }

    [Fact]
    public async Task ApprovedStudentProposal_IsReservedForAuthor_AndCannotBeReopened()
    {
        using var app = new TopicTestApp(); await app.SeedAsync();
        using var student = await Login(app, "student@tests.local");
        using var lecturer = await Login(app, "lecturer@tests.local");
        using var admin = await Login(app, "admin@tests.local");
        using var other = await Login(app, "other@tests.local");
        var id = await Id(await student.PostAsJsonAsync("/api/v1/topic-proposals", Draft("My own thesis")));
        Assert.Equal(HttpStatusCode.OK, (await lecturer.PostAsync($"/api/v1/topic-proposals/{id}/approve", null)).StatusCode);
        using var info = (await student.GetFromJsonAsync<JsonDocument>($"/api/v1/topics/{id}"))!;
        Assert.Equal("APPROVED", info.RootElement.GetProperty("status").GetString());
        Assert.False(info.RootElement.GetProperty("isRegistrationOpen").GetBoolean());
        var owner = info.RootElement.GetProperty("reservedForStudentUserId").GetGuid();
        var dbUserId = info.RootElement.GetProperty("proposedByUserId").GetGuid();
        Assert.Equal(dbUserId, owner);
        using var mine = (await student.GetFromJsonAsync<JsonDocument>("/api/v1/topics/mine"))!;
        Assert.Contains(mine.RootElement.EnumerateArray(), x => x.GetProperty("id").GetGuid() == id);
        Assert.Equal(HttpStatusCode.Conflict,
            (await admin.PatchAsJsonAsync($"/api/v1/topics/{id}/registration", new { isOpen = true })).StatusCode);
        var periodId = await Id(await admin.PostAsJsonAsync("/api/v1/registration-periods", new
        { name = "Test", startsAt = DateTimeOffset.UtcNow.AddDays(-1), endsAt = DateTimeOffset.UtcNow.AddDays(1), isOpen = true }));
        Assert.Equal(HttpStatusCode.Conflict,
            (await other.PostAsJsonAsync("/api/v1/topic-registrations", new { topicId = id, registrationPeriodId = periodId })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await other.PatchAsJsonAsync($"/api/v1/topics/{id}", Draft("Changed by another student"))).StatusCode);
    }

    [Fact]
    public async Task LecturerAcceptsPublishedTopic_LocksForSingleStudent_RejectsOtherRequests()
    {
        using var app = new TopicTestApp(); await app.SeedAsync();
        using var admin = await Login(app, "admin@tests.local");
        using var lecturer = await Login(app, "lecturer@tests.local");
        using var wrongLecturer = await Login(app, "otherlecturer@tests.local");
        using var first = await Login(app, "student@tests.local");
        using var second = await Login(app, "other@tests.local");
        var topicId = await Id(await lecturer.PostAsJsonAsync("/api/v1/topics", Draft("Open topic")));
        Assert.Equal(HttpStatusCode.OK,
            (await lecturer.PatchAsJsonAsync($"/api/v1/topics/{topicId}/registration", new { isOpen = true })).StatusCode);
        var periodId = await Id(await admin.PostAsJsonAsync("/api/v1/registration-periods", new
        { name = "Fall", startsAt = DateTimeOffset.UtcNow.AddDays(-1), endsAt = DateTimeOffset.UtcNow.AddDays(2), isOpen = true }));
        var firstId = await Id(await first.PostAsJsonAsync("/api/v1/topic-registrations", new { topicId, registrationPeriodId = periodId }));
        var secondId = await Id(await second.PostAsJsonAsync("/api/v1/topic-registrations", new { topicId, registrationPeriodId = periodId }));
        Assert.Equal(HttpStatusCode.Forbidden,
            (await wrongLecturer.PostAsync($"/api/v1/topic-registrations/{firstId}/accept", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await first.PostAsync($"/api/v1/topic-registrations/{firstId}/accept", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await lecturer.PostAsync($"/api/v1/topic-registrations/{firstId}/accept", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await lecturer.PostAsync($"/api/v1/topic-registrations/{secondId}/accept", null)).StatusCode);
        using var topic = (await first.GetFromJsonAsync<JsonDocument>($"/api/v1/topics/{topicId}"))!;
        Assert.False(topic.RootElement.GetProperty("isRegistrationOpen").GetBoolean());
        Assert.NotEqual(Guid.Empty, topic.RootElement.GetProperty("reservedForStudentUserId").GetGuid());
        Assert.Equal(HttpStatusCode.Conflict,
            (await admin.PatchAsJsonAsync($"/api/v1/topics/{topicId}/registration", new { isOpen = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await first.PostAsync($"/api/v1/topic-registrations/{firstId}/cancel", null)).StatusCode);
        using var list = (await second.GetFromJsonAsync<JsonDocument>("/api/v1/topic-registrations"))!;
        Assert.Equal("REJECTED", list.RootElement[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task AdminPublishedTopic_CanBeAcceptedByLecturer()
    {
        using var app = new TopicTestApp(); await app.SeedAsync();
        using var admin = await Login(app, "admin@tests.local");
        using var lecturer = await Login(app, "lecturer@tests.local");
        using var student = await Login(app, "student@tests.local");
        var topicId = await Id(await admin.PostAsJsonAsync("/api/v1/topics", Draft("Admin published")));
        Assert.Equal(HttpStatusCode.OK,
            (await admin.PatchAsJsonAsync($"/api/v1/topics/{topicId}/registration", new { isOpen = true })).StatusCode);
        var periodId = await Id(await admin.PostAsJsonAsync("/api/v1/registration-periods", new
        { name = "Spring", startsAt = DateTimeOffset.UtcNow.AddDays(-1), endsAt = DateTimeOffset.UtcNow.AddDays(2), isOpen = true }));
        var regId = await Id(await student.PostAsJsonAsync("/api/v1/topic-registrations", new { topicId, registrationPeriodId = periodId }));
        Assert.Equal(HttpStatusCode.OK,
            (await lecturer.PostAsync($"/api/v1/topic-registrations/{regId}/accept", null)).StatusCode);
        using var topic = (await admin.GetFromJsonAsync<JsonDocument>($"/api/v1/topics/{topicId}"))!;
        Assert.False(topic.RootElement.GetProperty("isRegistrationOpen").GetBoolean());
        Assert.NotEqual(Guid.Empty, topic.RootElement.GetProperty("reservedForStudentUserId").GetGuid());
    }

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

    [Fact]
    public async Task WithdrawingAcceptedPublishedTopic_ReleasesRegistrationAndAutomaticallyReopens()
    {
        using var app = new TopicTestApp(); await app.SeedAsync();
        using var student = await Login(app, "student@tests.local");
        using var other = await Login(app, "other@tests.local");
        using var lecturer = await Login(app, "lecturer@tests.local");
        using var admin = await Login(app, "admin@tests.local");
        var firstTopicId = await Id(await lecturer.PostAsJsonAsync("/api/v1/topics", Draft("First topic")));
        var nextTopicId = await Id(await lecturer.PostAsJsonAsync("/api/v1/topics", Draft("Next topic")));
        await lecturer.PatchAsJsonAsync($"/api/v1/topics/{firstTopicId}/registration", new { isOpen = true });
        await lecturer.PatchAsJsonAsync($"/api/v1/topics/{nextTopicId}/registration", new { isOpen = true });
        var periodId = await Id(await admin.PostAsJsonAsync("/api/v1/registration-periods", new
        { name = "Period", startsAt = DateTimeOffset.UtcNow.AddDays(-1), endsAt = DateTimeOffset.UtcNow.AddDays(1), isOpen = true }));
        var firstRegistrationId = await Id(await student.PostAsJsonAsync("/api/v1/topic-registrations", new { topicId = firstTopicId, registrationPeriodId = periodId }));
        Assert.Equal(HttpStatusCode.OK, (await lecturer.PostAsync($"/api/v1/topic-registrations/{firstRegistrationId}/accept", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await student.PostAsync($"/api/v1/topic-registrations/{firstRegistrationId}/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await student.PostAsJsonAsync("/api/v1/topic-registrations", new { topicId = nextTopicId, registrationPeriodId = periodId })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PostAsync($"/api/v1/topics/{firstTopicId}/withdraw", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await student.PostAsync($"/api/v1/topics/{firstTopicId}/withdraw", null)).StatusCode);
        using var mine = (await student.GetFromJsonAsync<JsonDocument>("/api/v1/topics/mine"))!;
        Assert.DoesNotContain(mine.RootElement.EnumerateArray(), item => item.GetProperty("id").GetGuid() == firstTopicId);
        using var topicAfterWithdraw = (await student.GetFromJsonAsync<JsonDocument>($"/api/v1/topics/{firstTopicId}"))!;
        Assert.True(topicAfterWithdraw.RootElement.GetProperty("isRegistrationOpen").GetBoolean());
        Assert.Equal("PUBLISHED", topicAfterWithdraw.RootElement.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, topicAfterWithdraw.RootElement.GetProperty("reservedForStudentUserId").ValueKind);
        Assert.Equal(HttpStatusCode.Created, (await student.PostAsJsonAsync("/api/v1/topic-registrations", new { topicId = nextTopicId, registrationPeriodId = periodId })).StatusCode);
    }

    [Fact]
    public async Task TwoSelfProposals_CannotBothBeApproved_WithdrawalAllowsNextApproval()
    {
        using var app = new TopicTestApp(); await app.SeedAsync();
        using var student = await Login(app, "student@tests.local");
        using var lecturer = await Login(app, "lecturer@tests.local");
        var x = await Id(await student.PostAsJsonAsync("/api/v1/topic-proposals", Draft("Topic x")));
        var y = await Id(await student.PostAsJsonAsync("/api/v1/topic-proposals", Draft("Topic y")));
        Assert.Equal(HttpStatusCode.OK, (await lecturer.PostAsync($"/api/v1/topic-proposals/{x}/approve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await lecturer.PostAsync($"/api/v1/topic-proposals/{y}/approve", null)).StatusCode);
        using (var mine = (await student.GetFromJsonAsync<JsonDocument>("/api/v1/topics/mine"))!)
            Assert.Single(mine.RootElement.EnumerateArray());
        Assert.Equal(HttpStatusCode.Conflict,
            (await student.PostAsJsonAsync("/api/v1/topic-proposals", Draft("Another proposed topic"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await student.PostAsync($"/api/v1/topics/{x}/withdraw", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await lecturer.PostAsync($"/api/v1/topic-proposals/{y}/approve", null)).StatusCode);
        using var after = (await student.GetFromJsonAsync<JsonDocument>("/api/v1/topics/mine"))!;
        Assert.Single(after.RootElement.EnumerateArray());
        Assert.Equal(y, after.RootElement[0].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task AcceptedPublishedTopic_PreventsApprovingOtherSelfProposal()
    {
        using var app = new TopicTestApp(); await app.SeedAsync();
        using var admin = await Login(app, "admin@tests.local");
        using var lecturer = await Login(app, "lecturer@tests.local");
        using var student = await Login(app, "student@tests.local");
        var proposedId = await Id(await student.PostAsJsonAsync("/api/v1/topic-proposals", Draft("Student proposal")));
        var topicId = await Id(await lecturer.PostAsJsonAsync("/api/v1/topics", Draft("Published lecturer topic")));
        Assert.Equal(HttpStatusCode.OK, (await lecturer.PatchAsJsonAsync($"/api/v1/topics/{topicId}/registration", new { isOpen = true })).StatusCode);
        var periodId = await Id(await admin.PostAsJsonAsync("/api/v1/registration-periods", new
        { name = "Open period", startsAt = DateTimeOffset.UtcNow.AddDays(-1), endsAt = DateTimeOffset.UtcNow.AddDays(2), isOpen = true }));
        var registrationId = await Id(await student.PostAsJsonAsync("/api/v1/topic-registrations", new { topicId, registrationPeriodId = periodId }));
        Assert.Equal(HttpStatusCode.OK, (await lecturer.PostAsync($"/api/v1/topic-registrations/{registrationId}/accept", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await lecturer.PostAsync($"/api/v1/topic-proposals/{proposedId}/approve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await student.PostAsync($"/api/v1/topics/{topicId}/withdraw", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await lecturer.PostAsync($"/api/v1/topic-proposals/{proposedId}/approve", null)).StatusCode);
    }

    [Fact]
    public async Task SameStudent_CannotAcceptTwoPublishedTopicsInDifferentPeriods()
    {
        using var app = new TopicTestApp(); await app.SeedAsync();
        using var admin = await Login(app, "admin@tests.local");
        using var lecturer = await Login(app, "lecturer@tests.local");
        using var student = await Login(app, "student@tests.local");
        var x = await Id(await lecturer.PostAsJsonAsync("/api/v1/topics", Draft("Published topic X")));
        var y = await Id(await lecturer.PostAsJsonAsync("/api/v1/topics", Draft("Published topic Y")));
        Assert.Equal(HttpStatusCode.OK, (await lecturer.PatchAsJsonAsync($"/api/v1/topics/{x}/registration", new { isOpen = true })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await lecturer.PatchAsJsonAsync($"/api/v1/topics/{y}/registration", new { isOpen = true })).StatusCode);
        var p1 = await Id(await admin.PostAsJsonAsync("/api/v1/registration-periods", new
        { name = "Period 1", startsAt = DateTimeOffset.UtcNow.AddDays(-1), endsAt = DateTimeOffset.UtcNow.AddDays(2), isOpen = true }));
        var p2 = await Id(await admin.PostAsJsonAsync("/api/v1/registration-periods", new
        { name = "Period 2", startsAt = DateTimeOffset.UtcNow.AddDays(-1), endsAt = DateTimeOffset.UtcNow.AddDays(2), isOpen = true }));
        var r1 = await Id(await student.PostAsJsonAsync("/api/v1/topic-registrations", new { topicId = x, registrationPeriodId = p1 }));
        var r2 = await Id(await student.PostAsJsonAsync("/api/v1/topic-registrations", new { topicId = y, registrationPeriodId = p2 }));
        Assert.Equal(HttpStatusCode.OK, (await lecturer.PostAsync($"/api/v1/topic-registrations/{r1}/accept", null)).StatusCode);
        using (var mine = (await student.GetFromJsonAsync<JsonDocument>("/api/v1/topic-registrations"))!)
        {
            Assert.Single(mine.RootElement.EnumerateArray());
            Assert.DoesNotContain(mine.RootElement.EnumerateArray(), x => x.GetProperty("id").GetGuid() == r2);
        }
        Assert.Equal(HttpStatusCode.NotFound, (await lecturer.PostAsync($"/api/v1/topic-registrations/{r2}/accept", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await student.PostAsJsonAsync("/api/v1/topic-registrations", new { topicId = y, registrationPeriodId = p2 })).StatusCode);
    }

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

    [Fact]
    public async Task AdminCreatedTopic_AutoReopensAfterAcceptedStudentWithdraws_AnyLecturerCanEdit()
    {
        using var app = new TopicTestApp(); await app.SeedAsync();
        using var admin = await Login(app, "admin@tests.local");
        using var lecturer = await Login(app, "lecturer@tests.local");
        using var student = await Login(app, "student@tests.local");
        using var other = await Login(app, "other@tests.local");

        var topicId = await Id(await admin.PostAsJsonAsync("/api/v1/topics", Draft("Staff-created topic")));
        Assert.Equal(HttpStatusCode.OK,
            (await admin.PatchAsJsonAsync($"/api/v1/topics/{topicId}/registration", new { isOpen = true })).StatusCode);
        var periodId = await Id(await admin.PostAsJsonAsync("/api/v1/registration-periods", new
        { name = "Active", startsAt = DateTimeOffset.UtcNow.AddDays(-1), endsAt = DateTimeOffset.UtcNow.AddDays(1), isOpen = true }));
        var first = await Id(await student.PostAsJsonAsync("/api/v1/topic-registrations", new { topicId, registrationPeriodId = periodId }));
        Assert.Equal(HttpStatusCode.OK, (await lecturer.PostAsync($"/api/v1/topic-registrations/{first}/accept", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await student.PostAsync($"/api/v1/topics/{topicId}/withdraw", null)).StatusCode);

        // No staff action is required to republish or open it after withdrawal.
        using (var reopened = (await other.GetFromJsonAsync<JsonDocument>($"/api/v1/topics/{topicId}"))!)
        {
            Assert.Equal("PUBLISHED", reopened.RootElement.GetProperty("status").GetString());
            Assert.True(reopened.RootElement.GetProperty("isRegistrationOpen").GetBoolean());
            Assert.Equal(JsonValueKind.Null, reopened.RootElement.GetProperty("reservedForStudentUserId").ValueKind);
        }
        Assert.Equal(HttpStatusCode.OK,
            (await lecturer.PatchAsJsonAsync($"/api/v1/topics/{topicId}", Draft("Lecturer revised the vacant topic"))).StatusCode);
        var second = await Id(await other.PostAsJsonAsync("/api/v1/topic-registrations", new { topicId, registrationPeriodId = periodId }));
        Assert.Equal(HttpStatusCode.OK, (await lecturer.PostAsync($"/api/v1/topic-registrations/{second}/accept", null)).StatusCode);
        using var topic = (await other.GetFromJsonAsync<JsonDocument>($"/api/v1/topics/{topicId}"))!;
        Assert.Equal("Lecturer revised the vacant topic", topic.RootElement.GetProperty("title").GetString());
        Assert.False(topic.RootElement.GetProperty("isRegistrationOpen").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, topic.RootElement.GetProperty("reservedForStudentUserId").ValueKind);
        using var oldRegs = (await student.GetFromJsonAsync<JsonDocument>("/api/v1/topic-registrations"))!;
        Assert.Equal("CANCELLED", oldRegs.RootElement[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task Student_CanRegisterSeveralDifferentTopicsInSamePeriod_ButNotDuplicateSameTopic()
    {
        using var app = new TopicTestApp(); await app.SeedAsync();
        using var admin = await Login(app, "admin@tests.local");
        using var student = await Login(app, "student@tests.local");
        var a = await Id(await admin.PostAsJsonAsync("/api/v1/topics", Draft("Choice A")));
        var b = await Id(await admin.PostAsJsonAsync("/api/v1/topics", Draft("Choice B")));
        Assert.Equal(HttpStatusCode.OK, (await admin.PatchAsJsonAsync($"/api/v1/topics/{a}/registration", new { isOpen = true })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PatchAsJsonAsync($"/api/v1/topics/{b}/registration", new { isOpen = true })).StatusCode);
        var period = await Id(await admin.PostAsJsonAsync("/api/v1/registration-periods", new
        { name = "One Period", startsAt = DateTimeOffset.UtcNow.AddDays(-1), endsAt = DateTimeOffset.UtcNow.AddDays(1), isOpen = true }));
        var first = await student.PostAsJsonAsync("/api/v1/topic-registrations", new { topicId = a, registrationPeriodId = period });
        var second = await student.PostAsJsonAsync("/api/v1/topic-registrations", new { topicId = b, registrationPeriodId = period });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await student.PostAsJsonAsync("/api/v1/topic-registrations", new { topicId = b, registrationPeriodId = period })).StatusCode);
        using var myRequests = (await student.GetFromJsonAsync<JsonDocument>("/api/v1/topic-registrations"))!;
        Assert.Equal(2, myRequests.RootElement.EnumerateArray().Count(x => x.GetProperty("status").GetString() == "PENDING"));
    }

    [Fact]
    public async Task LecturerAcceptsOneTopic_RemovesStudentOtherRegistrations()
    {
        using var app = new TopicTestApp(); await app.SeedAsync();
        using var admin = await Login(app, "admin@tests.local");
        using var lecturer = await Login(app, "lecturer@tests.local");
        using var student = await Login(app, "student@tests.local");
        using var other = await Login(app, "other@tests.local");
        var a = await Id(await lecturer.PostAsJsonAsync("/api/v1/topics", Draft("Option A")));
        var b = await Id(await lecturer.PostAsJsonAsync("/api/v1/topics", Draft("Option B")));
        await lecturer.PatchAsJsonAsync($"/api/v1/topics/{a}/registration", new { isOpen = true });
        await lecturer.PatchAsJsonAsync($"/api/v1/topics/{b}/registration", new { isOpen = true });
        var period = await Id(await admin.PostAsJsonAsync("/api/v1/registration-periods", new
        { name = "Same Period", startsAt = DateTimeOffset.UtcNow.AddDays(-1), endsAt = DateTimeOffset.UtcNow.AddDays(1), isOpen = true }));
        var firstId = await Id(await student.PostAsJsonAsync("/api/v1/topic-registrations", new { topicId = a, registrationPeriodId = period }));
        var secondId = await Id(await student.PostAsJsonAsync("/api/v1/topic-registrations", new { topicId = b, registrationPeriodId = period }));
        var competingId = await Id(await other.PostAsJsonAsync("/api/v1/topic-registrations", new { topicId = a, registrationPeriodId = period }));
        Assert.Equal(HttpStatusCode.OK, (await lecturer.PostAsync($"/api/v1/topic-registrations/{firstId}/accept", null)).StatusCode);
        using (var mine = (await student.GetFromJsonAsync<JsonDocument>("/api/v1/topic-registrations"))!)
        {
            Assert.Equal("ACCEPTED", mine.RootElement.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == firstId).GetProperty("status").GetString());
            Assert.Single(mine.RootElement.EnumerateArray());
            Assert.DoesNotContain(mine.RootElement.EnumerateArray(), x => x.GetProperty("id").GetGuid() == secondId);
        }
        using (var otherRequests = (await other.GetFromJsonAsync<JsonDocument>("/api/v1/topic-registrations"))!)
            Assert.Equal("REJECTED", otherRequests.RootElement.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == competingId).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await lecturer.PostAsync($"/api/v1/topic-registrations/{secondId}/accept", null)).StatusCode);
        using (var unassigned = (await student.GetFromJsonAsync<JsonDocument>($"/api/v1/topics/{b}"))!)
            Assert.True(unassigned.RootElement.GetProperty("isRegistrationOpen").GetBoolean());
        Assert.Equal(HttpStatusCode.Created,
            (await other.PostAsJsonAsync("/api/v1/topic-registrations", new { topicId = b, registrationPeriodId = period })).StatusCode);
    }

    [Fact]
    public async Task LecturerApprovesSelfProposal_DeletesAllPreviousRegistrations()
    {
        using var app = new TopicTestApp(); await app.SeedAsync();
        using var admin = await Login(app, "admin@tests.local");
        using var lecturer = await Login(app, "lecturer@tests.local");
        using var student = await Login(app, "student@tests.local");
        var a = await Id(await admin.PostAsJsonAsync("/api/v1/topics", Draft("Offered A")));
        var b = await Id(await admin.PostAsJsonAsync("/api/v1/topics", Draft("Offered B")));
        await admin.PatchAsJsonAsync($"/api/v1/topics/{a}/registration", new { isOpen = true });
        await admin.PatchAsJsonAsync($"/api/v1/topics/{b}/registration", new { isOpen = true });
        var period = await Id(await admin.PostAsJsonAsync("/api/v1/registration-periods", new
        { name = "Open", startsAt = DateTimeOffset.UtcNow.AddDays(-1), endsAt = DateTimeOffset.UtcNow.AddDays(1), isOpen = true }));
        var firstId = await Id(await student.PostAsJsonAsync("/api/v1/topic-registrations", new { topicId = a, registrationPeriodId = period }));
        var secondId = await Id(await student.PostAsJsonAsync("/api/v1/topic-registrations", new { topicId = b, registrationPeriodId = period }));
        var self = await Id(await student.PostAsJsonAsync("/api/v1/topic-proposals", Draft("Self proposed")));
        Assert.Equal(HttpStatusCode.OK, (await lecturer.PostAsync($"/api/v1/topic-proposals/{self}/approve", null)).StatusCode);
        using var mine = (await student.GetFromJsonAsync<JsonDocument>("/api/v1/topic-registrations"))!;
        Assert.Empty(mine.RootElement.EnumerateArray());
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StudentProjectsDbContext>();
            Assert.False(await db.TopicRegistrations.AnyAsync(r => r.Id == firstId || r.Id == secondId));
        }
        using var owned = (await student.GetFromJsonAsync<JsonDocument>("/api/v1/topics/mine"))!;
        Assert.Single(owned.RootElement.EnumerateArray());
        Assert.Equal(self, owned.RootElement[0].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Approval_DeletesRejectedAndCancelledHistoryButKeepsOnlyAccepted()
    {
        using var app = new TopicTestApp(); await app.SeedAsync();
        using var admin = await Login(app, "admin@tests.local");
        using var lecturer = await Login(app, "lecturer@tests.local");
        using var student = await Login(app, "student@tests.local");
        var topics = new Guid[3];
        for (int i = 0; i < 3; i++)
        {
            topics[i] = await Id(await lecturer.PostAsJsonAsync("/api/v1/topics", Draft($"Choose {i}")));
            Assert.Equal(HttpStatusCode.OK,
                (await lecturer.PatchAsJsonAsync($"/api/v1/topics/{topics[i]}/registration", new {isOpen=true})).StatusCode);
        }
        var period = await Id(await admin.PostAsJsonAsync("/api/v1/registration-periods", new {
            name = "Open", startsAt = DateTimeOffset.UtcNow.AddDays(-1),
            endsAt = DateTimeOffset.UtcNow.AddDays(1), isOpen = true }));
        var cancelled = await Id(await student.PostAsJsonAsync("/api/v1/topic-registrations", new {topicId=topics[0],registrationPeriodId=period}));
        var rejected = await Id(await student.PostAsJsonAsync("/api/v1/topic-registrations", new {topicId=topics[1],registrationPeriodId=period}));
        var accepted = await Id(await student.PostAsJsonAsync("/api/v1/topic-registrations", new {topicId=topics[2],registrationPeriodId=period}));
        Assert.Equal(HttpStatusCode.OK, (await student.PostAsync($"/api/v1/topic-registrations/{cancelled}/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await lecturer.PostAsync($"/api/v1/topic-registrations/{rejected}/reject", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await lecturer.PostAsync($"/api/v1/topic-registrations/{accepted}/accept", null)).StatusCode);
        using var mine = (await student.GetFromJsonAsync<JsonDocument>("/api/v1/topic-registrations"))!;
        Assert.Single(mine.RootElement.EnumerateArray());
        Assert.Equal(accepted, mine.RootElement[0].GetProperty("id").GetGuid());
        Assert.Equal("ACCEPTED", mine.RootElement[0].GetProperty("status").GetString());
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudentProjectsDbContext>();
        Assert.False(await db.TopicRegistrations.AnyAsync(r => r.Id == cancelled || r.Id == rejected));
        Assert.True(await db.TopicRegistrations.AnyAsync(r => r.Id == accepted));
    }

    [Fact]
    public async Task DeleteTopic_CleansMultipleRegistrationsWithoutAffectingOtherTopics()
    {
        using var app = new TopicTestApp(); await app.SeedAsync();
        using var admin = await Login(app, "admin@tests.local");
        using var lecturer = await Login(app, "lecturer@tests.local");
        using var first = await Login(app, "student@tests.local");
        using var second = await Login(app, "other@tests.local");
        var discarded = await Id(await lecturer.PostAsJsonAsync("/api/v1/topics", Draft("Delete me")));
        var keep = await Id(await lecturer.PostAsJsonAsync("/api/v1/topics", Draft("Keep me")));
        foreach (var id in new[] { discarded, keep })
            Assert.Equal(HttpStatusCode.OK, (await lecturer.PatchAsJsonAsync($"/api/v1/topics/{id}/registration", new {isOpen=true})).StatusCode);
        var period = await Id(await admin.PostAsJsonAsync("/api/v1/registration-periods", new {
            name = "Open", startsAt = DateTimeOffset.UtcNow.AddDays(-1),
            endsAt = DateTimeOffset.UtcNow.AddDays(1), isOpen = true }));
        var cancelled = await Id(await first.PostAsJsonAsync("/api/v1/topic-registrations", new {topicId=discarded,registrationPeriodId=period}));
        var rejected = await Id(await second.PostAsJsonAsync("/api/v1/topic-registrations", new {topicId=discarded,registrationPeriodId=period}));
        var remain = await Id(await first.PostAsJsonAsync("/api/v1/topic-registrations", new {topicId=keep,registrationPeriodId=period}));
        Assert.Equal(HttpStatusCode.OK, (await first.PostAsync($"/api/v1/topic-registrations/{cancelled}/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await lecturer.PostAsync($"/api/v1/topic-registrations/{rejected}/reject", null)).StatusCode);
        using (var item = (await admin.GetFromJsonAsync<JsonDocument>($"/api/v1/topics/{discarded}"))!)
            Assert.True(item.RootElement.GetProperty("canDelete").GetBoolean());
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/topics/{discarded}")).StatusCode);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudentProjectsDbContext>();
        Assert.False(await db.TopicRegistrations.AnyAsync(r => r.TopicId == discarded));
        Assert.False(await db.TopicStateHistories.AnyAsync(h => h.TopicId == discarded));
        Assert.True(await db.TopicRegistrations.AnyAsync(r => r.Id == remain));
        Assert.True(await db.Topics.AnyAsync(t => t.Id == keep));
    }

    [Fact]
    public async Task AfterStudentWithdrawsAcceptedTopic_AdminCanDeleteTopicAndAllHistory()
    {
        using var app = new TopicTestApp(); await app.SeedAsync();
        using var admin = await Login(app, "admin@tests.local");
        using var lecturer = await Login(app, "lecturer@tests.local");
        using var first = await Login(app, "student@tests.local");
        var topic = await Id(await lecturer.PostAsJsonAsync("/api/v1/topics", Draft("Reassigned then deleted")));
        await lecturer.PatchAsJsonAsync($"/api/v1/topics/{topic}/registration", new {isOpen=true});
        var period = await Id(await admin.PostAsJsonAsync("/api/v1/registration-periods", new {
            name = "Open", startsAt = DateTimeOffset.UtcNow.AddDays(-1),
            endsAt = DateTimeOffset.UtcNow.AddDays(1), isOpen = true }));
        var registration = await Id(await first.PostAsJsonAsync("/api/v1/topic-registrations", new {topicId=topic,registrationPeriodId=period}));
        Assert.Equal(HttpStatusCode.OK, (await lecturer.PostAsync($"/api/v1/topic-registrations/{registration}/accept", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/v1/topics/{topic}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await first.PostAsync($"/api/v1/topics/{topic}/withdraw", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/topics/{topic}")).StatusCode);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudentProjectsDbContext>();
        Assert.False(await db.TopicRegistrations.AnyAsync(r => r.Id == registration));
        Assert.False(await db.Topics.AnyAsync(t => t.Id == topic));
    }
}
