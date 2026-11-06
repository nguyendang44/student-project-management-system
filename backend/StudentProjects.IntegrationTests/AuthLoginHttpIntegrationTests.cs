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
using StudentProjects.Domain.Entities;
using StudentProjects.Domain.Enums;
using StudentProjects.Infrastructure.Persistence;
using Xunit;

namespace StudentProjects.IntegrationTests;

/// <summary>End-to-end HTTP checks against the real /api/v1/auth/login handler.
/// Uses isolated EF InMemory and a test JWT key. No Ollama/GitHub/network needed.</summary>
public sealed class AuthLoginHttpIntegrationTests
{
    private const string Key = "v011-TEST-ONLY-KEY-DO-NOT-USE-IN-PRODUCTION-1234567890";
    private const string Password = "integration-test-valid-password";

    private sealed class TestApp : WebApplicationFactory<Program>
    {
        private readonly string database = $"v011-auth-{Guid.NewGuid():N}";
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Jwt:Key"] = Key,
                    ["Jwt:Issuer"] = "StudentProjects",
                    ["Jwt:Audience"] = "StudentProjects-Frontend",
                    ["ConnectionStrings:Default"] = "Tests-Use-InMemory"
                }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDbContextOptionsConfiguration<StudentProjectsDbContext>>();
                services.RemoveAll<DbContextOptions<StudentProjectsDbContext>>();
                services.AddDbContext<StudentProjectsDbContext>(options => options.UseInMemoryDatabase(database));
                foreach (var service in services.Where(s => s.ServiceType == typeof(IHostedService) &&
                    (s.ImplementationType?.Name is "V08NotifierWorker" or "CodeAnalysisWorker")).ToArray())
                    services.Remove(service);
            });
        }
        public async Task SeedAsync()
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<StudentProjectsDbContext>();
            await db.Database.EnsureCreatedAsync();
            var user = new User { Id = Guid.NewGuid(), Email = "valid@v011.test",
                FullName = "Valid tester", RoleId = RoleIds.Student, IsActive = true };
            user.PasswordHash = new PasswordHasher<User>().HashPassword(user, Password);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Anonymous_caller_can_reach_login_but_invalid_credentials_get_401()
    {
        using var app = new TestApp(); await app.SeedAsync();
        using var client = app.CreateClient(); // Deliberately no Authorization header
        var malformed = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "", password = "" });
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        var missing = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "missing@v011.test", password = Password });
        var wrong = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "valid@v011.test", password = "not-the-correct-password" });
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        using var missingJson = (await missing.Content.ReadFromJsonAsync<JsonDocument>())!;
        using var wrongJson = (await wrong.Content.ReadFromJsonAsync<JsonDocument>())!;
        Assert.Equal("INVALID_CREDENTIALS", missingJson.RootElement.GetProperty("code").GetString());
        Assert.Equal("INVALID_CREDENTIALS", wrongJson.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Anonymous_caller_with_valid_credentials_gets_token_while_logout_is_protected()
    {
        using var app = new TestApp(); await app.SeedAsync();
        using var client = app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsync("/api/v1/auth/logout", null)).StatusCode);
        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "valid@v011.test", password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = (await response.Content.ReadFromJsonAsync<JsonDocument>())!;
        var token = body.RootElement.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
    }
}
