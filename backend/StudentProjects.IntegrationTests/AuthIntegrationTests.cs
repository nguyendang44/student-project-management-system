using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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

public sealed class AuthIntegrationTests
{
    private const string Email = "student@test.local";
    private const string Password = "StudentSecret!123";
    private static readonly Guid StudentId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private const string TestingJwtKey = "TEST-ONLY-AUTH-INTEGRATION-KEY-CHANGE-IN-PROD-2026-ABC";

    private sealed class TestServer : WebApplicationFactory<Program>
    {
        private readonly string _dbName = $"auth-integration-{Guid.NewGuid()}";
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Key"] = TestingJwtKey,
                    ["Jwt:Issuer"] = "StudentProjects",
                    ["Jwt:Audience"] = "StudentProjects-Frontend",
                    ["ConnectionStrings:Default"] = "Testing-Not-Used"
                }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDbContextOptionsConfiguration<StudentProjectsDbContext>>();
                services.RemoveAll<DbContextOptions<StudentProjectsDbContext>>();
                services.AddDbContext<StudentProjectsDbContext>(o =>
                    o.UseInMemoryDatabase(_dbName));
            });
        }

        public async Task SeedAsync()
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<StudentProjectsDbContext>();
            await db.Database.EnsureCreatedAsync();
            var user = new User
            {
                Id = StudentId, Email = Email, FullName = "Integration Student", RoleId = RoleIds.Student,
                TokenVersion = 0, IsActive = true
            };
            user.PasswordHash = new PasswordHasher<User>().HashPassword(user, Password);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }
    }

    private static async Task<string> LoginAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = Email, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonDocument>();
        Assert.NotNull(json);
        Assert.Equal("Student", json.RootElement.GetProperty("role").GetString());
        var token = json.RootElement.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        return token!;
    }

    [Fact]
    public async Task InvalidCredentials_Return401()
    {
        using var server = new TestServer();
        using var client = server.CreateClient();
        await server.SeedAsync();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = Email, password = "wrong" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AnonymousRoleProtectedEndpoint_Return401()
    {
        using var server = new TestServer();
        using var client = server.CreateClient();
        var response = await client.GetAsync("/api/v1/users");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LoginMeRoleAndLogout_EnforcedByServer()
    {
        using var server = new TestServer();
        using var client = server.CreateClient();
        await server.SeedAsync();
        var token = await LoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var me = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var allowedSkeleton = await client.GetAsync("/api/v1/dashboard");
        Assert.Equal(HttpStatusCode.NotImplemented, allowedSkeleton.StatusCode);
        var notAllowed = await client.GetAsync("/api/v1/users");
        Assert.Equal(HttpStatusCode.Forbidden, notAllowed.StatusCode);
        var logout = await client.PostAsync("/api/v1/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        var revoked = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
    }

    [Fact]
    public async Task DisabledAccount_RejectsExistingJwt()
    {
        using var server = new TestServer();
        using var client = server.CreateClient();
        await server.SeedAsync();
        var token = await LoginAsync(client);
        using (var scope = server.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StudentProjectsDbContext>();
            var user = await db.Users.SingleAsync(x => x.Id == StudentId);
            user.IsActive = false;
            await db.SaveChangesAsync();
        }
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/v1/auth/login", new { email = Email, password = Password })).StatusCode);
    }
}
