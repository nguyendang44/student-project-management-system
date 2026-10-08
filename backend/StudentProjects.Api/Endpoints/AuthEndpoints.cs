using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StudentProjects.Api.Security;
using StudentProjects.Application.Contracts;
using StudentProjects.Domain.Entities;
using StudentProjects.Infrastructure.Persistence;

namespace StudentProjects.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth");
        group.MapPost("/login", Login).AllowAnonymous().RequireRateLimiting("auth-login");
        group.MapPost("/logout", Logout).RequireAuthorization();
        group.MapGet("/me", Me).RequireAuthorization();
    }

    private static async Task<IResult> Login(
        LoginRequest? request, StudentProjectsDbContext db,
        IPasswordHasher<User> hasher, TokenService tokens, CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password) || request.Email.Length > 256 || request.Password.Length > 1024)
            return Results.BadRequest(new { code = "INVALID_REQUEST", message = "Email and password are required." });

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.Include(x => x.Role).SingleOrDefaultAsync(x => x.Email == email, ct);
        // Return the same response for missing, disabled, or incorrectly authenticated accounts.
        if (user is null || !user.IsActive)
            return Results.Json(new { code = "INVALID_CREDENTIALS", message = "Email or password is incorrect." }, statusCode: 401);
        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
            return Results.Json(new { code = "INVALID_CREDENTIALS", message = "Email or password is incorrect." }, statusCode: 401);

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = hasher.HashPassword(user, request.Password);
            user.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        var (token, expiresAt) = tokens.Create(user);
        return Results.Ok(new LoginResponse(token, expiresAt, user.Id, user.Email, user.FullName, user.Role.Name));
    }

    private static async Task<IResult> Me(ClaimsPrincipal principal, StudentProjectsDbContext db, CancellationToken ct)
    {
        if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();
        var user = await db.Users.AsNoTracking().Include(x => x.Role).SingleOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null || !user.IsActive) return Results.Unauthorized();
        return Results.Ok(new CurrentUserResponse(user.Id, user.Email, user.FullName, user.Role.Name));
    }

    private static async Task<IResult> Logout(ClaimsPrincipal principal, StudentProjectsDbContext db, CancellationToken ct)
    {
        if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId && x.IsActive, ct);
        if (user is null) return Results.Unauthorized();
        // Simple v1 revocation: all tokens belonging to the account become invalid.
        user.TokenVersion++;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid id)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(value, out id);
    }
}
