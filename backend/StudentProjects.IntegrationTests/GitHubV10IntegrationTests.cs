using System.Net;
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
using StudentProjects.Api.Services;
using StudentProjects.Domain.Entities;
using StudentProjects.Domain.Enums;
using StudentProjects.Infrastructure.Persistence;
using Xunit;

namespace StudentProjects.IntegrationTests;

public sealed class GitHubV10IntegrationTests
{
    private const string Password = "v010-test-password-only";
    private const string Key = "v010-TEST-ONLY-JWT-KEY-STRONG-ENOUGH-32-BYTES-NOT-PRODUCTION";
    private static readonly Guid StudentA = Guid.Parse("01000000-0000-4000-8000-000000000001");
    private static readonly Guid StudentB = Guid.Parse("01000000-0000-4000-8000-000000000002");
    private static readonly Guid LecturerA = Guid.Parse("01000000-0000-4000-8000-000000000003");
    private static readonly Guid LecturerB = Guid.Parse("01000000-0000-4000-8000-000000000004");
    private static readonly Guid Admin = Guid.Parse("01000000-0000-4000-8000-000000000005");
    private static readonly Guid ProjectA = Guid.Parse("01000000-0000-4000-8000-000000000006");
    private static readonly Guid ProjectB = Guid.Parse("01000000-0000-4000-8000-000000000007");

    private sealed class FakeGitHub : IGitHubPublicClient
    {
        public Task<GitHubRepositoryInfo> GetRepositoryAsync(string owner, string repo, CancellationToken ct) =>
            Task.FromResult(new GitHubRepositoryInfo($"https://github.com/{owner}/{repo}", $"{owner}/{repo}",
                "main", "A test repo", DateTimeOffset.UtcNow, false));
        public Task<IReadOnlyList<string>> GetBranchesAsync(string owner, string repo, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>(new[] { "main", "feature-demo" });
        public Task<IReadOnlyList<GitHubCommitInfo>> GetCommitsAsync(string owner, string repo, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<GitHubCommitInfo>>(new[] {
                new GitHubCommitInfo(new string('a', 40), "Initial work", "Tester", DateTimeOffset.UtcNow,
                    $"https://github.com/{owner}/{repo}/commit/{new string('a',40)}")
            });
    }

    private sealed class TestApp : WebApplicationFactory<Program>
    {
        private readonly string dbName = $"v010-{Guid.NewGuid():N}";
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = Key, ["Jwt:Issuer"] = "StudentProjects",
                ["Jwt:Audience"] = "StudentProjects-Frontend", ["ConnectionStrings:Default"] = "Testing-Not-Used"
            }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDbContextOptionsConfiguration<StudentProjectsDbContext>>();
                services.RemoveAll<DbContextOptions<StudentProjectsDbContext>>();
                services.AddDbContext<StudentProjectsDbContext>(options => options.UseInMemoryDatabase(dbName));
                services.RemoveAll<IGitHubPublicClient>();
                services.AddSingleton<IGitHubPublicClient, FakeGitHub>();
                foreach (var descriptor in services.Where(d => d.ServiceType == typeof(IHostedService) &&
                    d.ImplementationType?.Name == "V08NotifierWorker").ToArray()) services.Remove(descriptor);
            });
        }
        public async Task SeedAsync()
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<StudentProjectsDbContext>();
            await db.Database.EnsureCreatedAsync();
            Add(db, StudentA, "a@v010.test", RoleIds.Student);
            Add(db, StudentB, "b@v010.test", RoleIds.Student);
            Add(db, LecturerA, "lecturer-a@v010.test", RoleIds.Lecturer);
            Add(db, LecturerB, "lecturer-b@v010.test", RoleIds.Lecturer);
            Add(db, Admin, "admin@v010.test", RoleIds.Admin);
            var periodId = Guid.NewGuid();
            db.RegistrationPeriods.Add(new RegistrationPeriod { Id = periodId, Name = "v010 test", IsOpen = true,
                StartsAt = DateTimeOffset.UtcNow.AddDays(-1), EndsAt = DateTimeOffset.UtcNow.AddDays(1) });
            var topicA = Guid.NewGuid(); var topicB = Guid.NewGuid();
            db.Topics.AddRange(new Topic { Id = topicA, Title = "A", Description = "A", ProposedByUserId = LecturerA,
                Status = TopicStatus.APPROVED }, new Topic { Id = topicB, Title = "B", Description = "B", ProposedByUserId = LecturerB,
                Status = TopicStatus.APPROVED });
            db.Projects.AddRange(new Project { Id = ProjectA, TopicId = topicA, RegistrationPeriodId = periodId,
                StudentUserId = StudentA, LecturerUserId = LecturerA, Status = ProjectStatus.IN_PROGRESS },
                new Project { Id = ProjectB, TopicId = topicB, RegistrationPeriodId = periodId,
                    StudentUserId = StudentB, LecturerUserId = LecturerB, Status = ProjectStatus.IN_PROGRESS });
            await db.SaveChangesAsync();
        }
        private static void Add(StudentProjectsDbContext db, Guid id, string email, Guid role)
        {
            var user = new User { Id = id, Email = email, FullName = email, RoleId = role, IsActive = true };
            user.PasswordHash = new PasswordHasher<User>().HashPassword(user, Password);
            db.Users.Add(user);
            if (role == RoleIds.Student) db.Students.Add(new StudentProfile { UserId = id, StudentCode = email });
            if (role == RoleIds.Lecturer) db.Lecturers.Add(new LecturerProfile { UserId = id });
        }
    }

    private static async Task<HttpClient> Login(TestApp app, string email)
    {
        var client = app.CreateClient();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var body = (await login.Content.ReadFromJsonAsync<JsonDocument>())!;
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", body.RootElement.GetProperty("accessToken").GetString());
        return client;
    }

    [Fact]
    public async Task Student_can_link_and_view_but_other_student_cannot_access()
    {
        using var app = new TestApp(); await app.SeedAsync();
        using var student = await Login(app, "a@v010.test");
        using var outsider = await Login(app, "b@v010.test");
        var response = await student.PostAsJsonAsync($"/api/v1/v10/projects/{ProjectA}/repositories",
            new { repositoryUrl = "https://github.com/example/demo" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var created = (await response.Content.ReadFromJsonAsync<JsonDocument>())!;
        var id = created.RootElement.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await student.GetAsync($"/api/v1/v10/projects/{ProjectA}/repositories")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync($"/api/v1/v10/projects/{ProjectA}/repositories")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync($"/api/v1/v10/repositories/{id}/commits")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync($"/api/v1/v10/repositories/{id}/branches")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.DeleteAsync($"/api/v1/v10/repositories/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.PostAsJsonAsync($"/api/v1/v10/projects/{ProjectA}/repositories",
            new { repositoryUrl = "https://github.com/example/other" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await student.GetAsync($"/api/v1/v10/repositories/{id}/commits")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await student.GetAsync($"/api/v1/v10/repositories/{id}/branches")).StatusCode);
    }

    [Fact]
    public async Task Lecturer_can_read_but_cannot_change_student_links()
    {
        using var app = new TestApp(); await app.SeedAsync();
        using var student = await Login(app, "a@v010.test");
        using var lecturer = await Login(app, "lecturer-a@v010.test");
        using var wrongLecturer = await Login(app, "lecturer-b@v010.test");
        var response = await student.PostAsJsonAsync($"/api/v1/v10/projects/{ProjectA}/repositories",
            new { repositoryUrl = "https://github.com/example/demo" });
        using var created = (await response.Content.ReadFromJsonAsync<JsonDocument>())!;
        var id = created.RootElement.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await lecturer.GetAsync($"/api/v1/v10/projects/{ProjectA}/repositories")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await lecturer.DeleteAsync($"/api/v1/v10/repositories/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await wrongLecturer.GetAsync($"/api/v1/v10/projects/{ProjectA}/repositories")).StatusCode);
    }

    [Theory]
    [InlineData("http://github.com/example/demo")]
    [InlineData("https://github.com.evil.test/example/demo")]
    [InlineData("https://evil.test/example/demo")]
    [InlineData("https://github.com/example/demo?redirect=1")]
    [InlineData("https://github.com/example/demo/other")]
    [InlineData("https://github.com@example.org/example/demo")]
    public async Task Rejects_noncanonical_repository_urls(string url)
    {
        using var app = new TestApp(); await app.SeedAsync();
        using var student = await Login(app, "a@v010.test");
        var response = await student.PostAsJsonAsync($"/api/v1/v10/projects/{ProjectA}/repositories", new { repositoryUrl = url });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_link_is_rejected_and_unlink_is_idempotent_in_database()
    {
        using var app = new TestApp(); await app.SeedAsync();
        using var student = await Login(app, "a@v010.test");
        var url = $"/api/v1/v10/projects/{ProjectA}/repositories";
        var first = await student.PostAsJsonAsync(url, new { repositoryUrl = "https://github.com/example/demo" });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        using var created = (await first.Content.ReadFromJsonAsync<JsonDocument>())!;
        var id = created.RootElement.GetProperty("id").GetGuid();
        var duplicate = await student.PostAsJsonAsync(url, new { repositoryUrl = "https://github.com/example/demo" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await student.DeleteAsync($"/api/v1/v10/repositories/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await student.DeleteAsync($"/api/v1/v10/repositories/{id}")).StatusCode);
    }
}
