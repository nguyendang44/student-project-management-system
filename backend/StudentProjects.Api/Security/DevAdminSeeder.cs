using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StudentProjects.Domain.Entities;
using StudentProjects.Infrastructure.Persistence;

namespace StudentProjects.Api.Security;

public static class DevAdminSeeder
{
    public static async Task SeedAsync(WebApplication app)
    {
        if (!app.Environment.IsDevelopment()) return;
        var email = app.Configuration["AuthBootstrap:Email"]?.Trim().ToLowerInvariant();
        var password = app.Configuration["AuthBootstrap:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;
        if (password.Length < 12) throw new InvalidOperationException("Bootstrap admin password must contain at least 12 characters.");
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<StudentProjectsDbContext>();
        if (await db.Users.AnyAsync(x => x.Email == email)) return; // never overwrite an existing password
        if (!await db.Roles.AnyAsync(x => x.Id == RoleIds.Admin))
            throw new InvalidOperationException("Apply EF Core migrations first: dotnet ef database update.");
        var user = new User
        {
            Email = email, FullName = "Local Admin", RoleId = RoleIds.Admin, IsActive = true
        };
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        user.PasswordHash = hasher.HashPassword(user, password);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        app.Logger.LogInformation("Created local development admin: {Email}", email);
    }
}
