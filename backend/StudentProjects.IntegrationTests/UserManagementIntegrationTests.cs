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

public sealed class UserManagementIntegrationTests
{
    private const string Password = "UserManagement!1234";
    private const string Key = "TEST-ONLY-USER-MANAGEMENT-JWT-KEY-2026-VERY-LONG-VALID";

    private sealed class TestApp : WebApplicationFactory<Program>
    {
        private readonly string database = "user-tests-" + Guid.NewGuid();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, settings) => settings.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = "Testing-Not-Used",
                    ["Jwt:Key"] = Key,
                    ["Jwt:Issuer"] = "StudentProjects",
                    ["Jwt:Audience"] = "StudentProjects-Frontend"
                }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDbContextOptionsConfiguration<StudentProjectsDbContext>>();
                services.RemoveAll<DbContextOptions<StudentProjectsDbContext>>();
                services.AddDbContext<StudentProjectsDbContext>(options => options.UseInMemoryDatabase(database));
            });
        }
        public async Task SeedAsync()
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<StudentProjectsDbContext>();
            await db.Database.EnsureCreatedAsync();
            await AddUserAsync(db, "admin@test.local", "Test Admin", RoleIds.Admin);
            await AddUserAsync(db, "student@test.local", "Test Student", RoleIds.Student, "ST100");
            await AddUserAsync(db, "lecturer@test.local", "Test Lecturer", RoleIds.Lecturer);
        }
        private static async Task AddUserAsync(StudentProjectsDbContext db, string email, string name, Guid role, string? code = null)
        {
            var user = new User { Email = email, FullName = name, RoleId = role };
            user.PasswordHash = new PasswordHasher<User>().HashPassword(user, Password);
            db.Users.Add(user);
            if (role == RoleIds.Student) db.Students.Add(new StudentProfile { UserId = user.Id, StudentCode = code! });
            if (role == RoleIds.Lecturer) db.Lecturers.Add(new LecturerProfile { UserId = user.Id });
            await db.SaveChangesAsync();
        }
    }

    private static async Task<HttpClient> Authenticated(TestApp app, string email)
    {
        var client = app.CreateClient();
        var result = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        using var data = (await result.Content.ReadFromJsonAsync<JsonDocument>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", data.RootElement.GetProperty("accessToken").GetString());
        return client;
    }

    [Fact]
    public async Task Admin_CreatesSearchesUpdatesAndDisablesStudent()
    {
        using var app = new TestApp();
        using var client = app.CreateClient();
        await app.SeedAsync();
        using var admin = await Authenticated(app, "admin@test.local");
        var create = await admin.PostAsJsonAsync("/api/v1/users", new
        {
            fullName = "New Student", email = "newstudent@test.local", password = Password,
            role = "Student", studentCode = "ST200", faculty = "Engineering"
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        using var created = (await create.Content.ReadFromJsonAsync<JsonDocument>())!;
        var id = created.RootElement.GetProperty("id").GetGuid();
        Assert.False(created.RootElement.TryGetProperty("passwordHash", out _));
        var list = await admin.GetFromJsonAsync<JsonDocument>("/api/v1/users?role=Student&search=ST200&page=1&pageSize=10");
        Assert.Equal(1, list!.RootElement.GetProperty("total").GetInt32());
        var update = await admin.PutAsJsonAsync($"/api/v1/users/{id}", new
        {
            fullName = "Updated Student", email = "newstudent@test.local", studentCode = "ST200", faculty = "IT"
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal("Updated Student", (await update.Content.ReadFromJsonAsync<JsonDocument>())!.RootElement.GetProperty("fullName").GetString());
        using var newStudent = await Authenticated(app, "newstudent@test.local");
        var disabled = await admin.PatchAsJsonAsync($"/api/v1/users/{id}/status", new { isActive = false });
        Assert.Equal(HttpStatusCode.OK, disabled.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await newStudent.GetAsync("/api/v1/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "newstudent@test.local", password = Password })).StatusCode);
        var reactivated = await admin.PatchAsJsonAsync($"/api/v1/users/{id}/status", new { isActive = true });
        Assert.Equal(HttpStatusCode.OK, reactivated.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await newStudent.GetAsync("/api/v1/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Student_CannotManageAccounts_ButCanUpdateOnlyOwnAllowedFields()
    {
        using var app = new TestApp();
        await app.SeedAsync();
        using var student = await Authenticated(app, "student@test.local");
        Assert.Equal(HttpStatusCode.Forbidden, (await student.GetAsync("/api/v1/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await student.PostAsJsonAsync("/api/v1/users", new
        {
            fullName = "Wrong", email = "fail@test.local", password = Password, role = "Student", studentCode = "X1"
        })).StatusCode);
        var own = await student.GetFromJsonAsync<JsonDocument>("/api/v1/users/me/profile");
        Assert.Equal("ST100", own!.RootElement.GetProperty("studentCode").GetString());
        var updated = await student.PutAsJsonAsync("/api/v1/users/me/profile",
            new { fullName = "Changed Student", faculty = "Computer Science", specialty = (string?)null });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        using var result = (await updated.Content.ReadFromJsonAsync<JsonDocument>())!;
        Assert.Equal("ST100", result.RootElement.GetProperty("studentCode").GetString());
        Assert.Equal("Computer Science", result.RootElement.GetProperty("faculty").GetString());
        var id = result.RootElement.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Forbidden, (await student.GetAsync($"/api/v1/users/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await student.PatchAsJsonAsync($"/api/v1/users/{id}/status", new { isActive = false })).StatusCode);
    }

    [Fact]
    public async Task Admin_DuplicateEmailAndStudentCodeRejected_AndCannotCreateAdmin()
    {
        using var app = new TestApp();
        await app.SeedAsync();
        using var admin = await Authenticated(app, "admin@test.local");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/users", new
        {
            fullName = "Extra Admin", email = "another@test.local", password = Password, role = "Admin"
        })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/v1/users", new
        {
            fullName = "Duplicate", email = "student@test.local", password = Password,
            role = "Student", studentCode = "ST999"
        })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/v1/users", new
        {
            fullName = "Duplicate", email = "brandnew@test.local", password = Password,
            role = "Student", studentCode = "ST100"
        })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/users", new
        {
            fullName = "Short Password", email = "short@test.local", password = "1234",
            role = "Student", studentCode = "ST301"
        })).StatusCode);
    }

    [Fact]
    public async Task Lecturer_UpdatesSpecialty_DirectoryShowsOnlyActiveLecturers()
    {
        using var app = new TestApp();
        await app.SeedAsync();
        using var lecturer = await Authenticated(app, "lecturer@test.local");
        using var admin = await Authenticated(app, "admin@test.local");
        var edit = await lecturer.PutAsJsonAsync("/api/v1/users/me/profile",
            new { fullName = "Professor L", specialty = "Data Science", faculty = (string?)null });
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        using var directory = (await lecturer.GetFromJsonAsync<JsonDocument>("/api/v1/lecturers"))!;
        Assert.Single(directory.RootElement.EnumerateArray());
        Assert.Equal("Data Science", directory.RootElement[0].GetProperty("specialty").GetString());
        var id = (await lecturer.GetFromJsonAsync<JsonDocument>("/api/v1/users/me/profile"))!.RootElement.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await admin.PatchAsJsonAsync($"/api/v1/users/{id}/status", new { isActive = false })).StatusCode);
        using var another = await Authenticated(app, "student@test.local");
        using var noLecturers = (await another.GetFromJsonAsync<JsonDocument>("/api/v1/lecturers"))!;
        Assert.Empty(noLecturers.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task AnonymousEndpointsRequireAuthentication()
    {
        using var app = new TestApp();
        using var client = app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/users/me/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/lecturers")).StatusCode);
    }
}
