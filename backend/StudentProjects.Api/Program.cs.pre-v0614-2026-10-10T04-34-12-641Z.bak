using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StudentProjects.Api.Endpoints;
using StudentProjects.Api.Security;
using StudentProjects.Domain.Entities;
using StudentProjects.Infrastructure;
using StudentProjects.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5103");
builder.Services.AddOpenApi();
builder.Services.AddInfrastructureSkeleton(builder.Configuration);
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth-login", http => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true
        }));
});

// JWT options are built after the test host has applied its configuration overrides.
// Do not capture the key/issuer/audience during Program's early startup phase.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    var key = builder.Configuration["Jwt:Key"];
    var issuer = builder.Configuration["Jwt:Issuer"];
    var audience = builder.Configuration["Jwt:Audience"];
    if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32 ||
        string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience))
        throw new InvalidOperationException("Configure Jwt:Key (32+ bytes), Jwt:Issuer, Jwt:Audience via user-secrets or environment.");

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
        ValidateIssuer = true, ValidIssuer = issuer,
        ValidateAudience = true, ValidAudience = audience,
        ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true,
        ClockSkew = TimeSpan.FromSeconds(30)
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async ctx =>
        {
            var sub = ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier) ??
                      ctx.Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);
            var role = ctx.Principal?.FindFirstValue(ClaimTypes.Role);
            var version = ctx.Principal?.FindFirstValue("token_version");
            if (!Guid.TryParse(sub, out var userId) ||
                !int.TryParse(version, out var tokenVersion) || string.IsNullOrWhiteSpace(role))
            {
                ctx.Fail("Invalid token claims."); return;
            }
            var db = ctx.HttpContext.RequestServices.GetRequiredService<StudentProjectsDbContext>();
            var user = await db.Users.AsNoTracking().Include(x => x.Role).SingleOrDefaultAsync(x => x.Id == userId);
            if (user is null || !user.IsActive || user.TokenVersion != tokenVersion || user.Role.Name != role)
                ctx.Fail("Token has been revoked, the role has changed, or the account is disabled.");
        }
    };
});
builder.Services.AddAuthorization();
var app = builder.Build();
// Fail fast in every environment if essential configuration is still missing.
// Configuration is complete here, including WebApplicationFactory's test overrides.
if (string.IsNullOrWhiteSpace(app.Configuration.GetConnectionString("Default")))
    throw new InvalidOperationException("Configure ConnectionStrings:Default for SQL Server.");
var startupKey = app.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(startupKey) || Encoding.UTF8.GetByteCount(startupKey) < 32 ||
    string.IsNullOrWhiteSpace(app.Configuration["Jwt:Issuer"]) ||
    string.IsNullOrWhiteSpace(app.Configuration["Jwt:Audience"]))
    throw new InvalidOperationException("Configure Jwt:Key (32+ bytes), Jwt:Issuer, Jwt:Audience via user-secrets or environment.");
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "StudentProjects API + Auth, Users and Topic Management v0.5 + Lecturer Supervision v0.6" }));
app.MapAuthEndpoints();
app.MapUserManagementEndpoints();
app.MapSkeletonEndpoints();
app.MapTopicEndpoints();
app.MapLecturerSupervisionEndpoints();
await DevAdminSeeder.SeedAsync(app);
app.Run();

public partial class Program { } // WebApplicationFactory test entry point
