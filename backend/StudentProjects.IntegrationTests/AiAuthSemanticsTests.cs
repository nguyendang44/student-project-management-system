using StudentProjects.Api.Services;
using Xunit;

namespace StudentProjects.IntegrationTests;

public sealed class AiAuthSemanticsTests
{
    private const string AuthPath = "backend/StudentProjects.Api/Endpoints/AuthEndpoints.cs";
    private const string AuthSource = """
        public static class AuthEndpoints
        {
            public static void MapAuthEndpoints(IEndpointRouteBuilder app)
            {
                var group = app.MapGroup("/api/v1/auth");
                group.MapPost("/login", Login).AllowAnonymous().RequireRateLimiting("auth-login");
                group.MapPost("/logout", Logout).RequireAuthorization();
            }
            private static async Task<IResult> Login(LoginRequest request)
            {
                if (request is null) return Results.BadRequest();
                var result = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
                if (result == PasswordVerificationResult.Failed)
                    return Results.Json(new { code = "INVALID_CREDENTIALS" }, statusCode: 401);
                return Results.Ok(new { accessToken = "test" });
            }
            private static IResult Logout() { return Results.NoContent(); }
        }
        """;

    [Fact]
    public void Intended_public_login_is_not_a_missing_authentication_vulnerability()
    {
        var source = new SourceFile(AuthPath, AuthSource);
        var finding = new AiFinding("security", "HIGH", AuthPath, 6,
            "group.MapPost(\"/login\", Login).AllowAnonymous().RequireRateLimiting(\"auth-login\");",
            "Endpoint /login không yêu cầu xác thực, người chưa đăng nhập có thể đăng nhập.",
            "Yêu cầu xác thực cho endpoint /login.");
        Assert.True(AiPublicAuthRoutePolicy.IsUnsupportedLoginGateClaim(source, finding));
        Assert.Null(AiAuthorizationContextGuard.Screen(finding, source));
    }

    [Fact]
    public void A_public_admin_route_is_not_silently_discarded()
    {
        var path = "backend/StudentProjects.Api/Endpoints/OtherEndpoints.cs";
        var source = new SourceFile(path, """
            public static class OtherEndpoints
            {
                public static void Map(IEndpointRouteBuilder app)
                {
                    app.MapPost("/admin", Dangerous).AllowAnonymous();
                }
                private static IResult Dangerous() { return Results.Ok(); }
            }
            """);
        var finding = new AiFinding("security", "HIGH", path, 5,
            "app.MapPost(\"/admin\", Dangerous).AllowAnonymous();",
            "Endpoint không yêu cầu xác thực.", "Cần kiểm tra quyền truy cập.");
        Assert.Equal("LOW", AiAuthorizationContextGuard.Screen(finding, source)?.Severity);
    }

    [Fact]
    public void Login_without_verified_credentials_is_not_assumed_safe()
    {
        var source = new SourceFile(AuthPath, """
            public static class AuthEndpoints
            {
                public static void MapAuthEndpoints(IEndpointRouteBuilder app)
                {
                    app.MapPost("/login", Login).AllowAnonymous();
                }
                private static IResult Login() { return Results.Ok(new { token = "unverified" }); }
            }
            """);
        var finding = new AiFinding("security", "HIGH", AuthPath, 5,
            "app.MapPost(\"/login\", Login).AllowAnonymous();",
            "Endpoint /login không yêu cầu xác thực.", "Yêu cầu xác thực.");
        Assert.False(AiPublicAuthRoutePolicy.IsUnsupportedLoginGateClaim(source, finding));
        Assert.NotNull(AiAuthorizationContextGuard.Screen(finding, source));
    }

    [Fact]
    public void A_purposefully_public_register_route_is_not_flagged_just_for_being_anonymous()
    {
        var source = new SourceFile(AuthPath, """
            public static class AuthEndpoints
            {
                public static void Map(IEndpointRouteBuilder app)
                {
                    app.MapPost("/register", Register).AllowAnonymous();
                }
                private static IResult Register() { return Results.Ok(); }
            }
            """);
        var finding = new AiFinding("security", "HIGH", AuthPath, 5,
            "app.MapPost(\"/register\", Register).AllowAnonymous();",
            "Endpoint /register không yêu cầu xác thực.",
            "Yêu cầu xác thực cho /register.");
        Assert.True(AiPublicAuthRoutePolicy.IsUnsupportedLoginGateClaim(source, finding));
        Assert.Null(AiAuthorizationContextGuard.Screen(finding, source));
    }

    [Fact]
    public void Narrative_never_echoes_unverified_security_claims()
    {
        var raw = new AiReport("Auth có nhiều lỗ hổng nghiêm trọng.",
            "Phải thêm xác thực cho /login và sửa phân quyền.", []);
        var report = AiReviewNarrativePolicy.Compose("Auth", [AuthPath], raw);
        Assert.DoesNotContain("lỗ hổng", report.Summary);
        Assert.DoesNotContain("thêm xác thực", report.DevelopmentWork);
        Assert.Contains("Không có phát hiện", report.Summary);
        Assert.Contains("AuthEndpoints.cs", report.DevelopmentWork);
        Assert.Empty(report.Findings);
    }

    [Fact]
    public void Narrative_only_counts_screened_findings()
    {
        var finding = new AiFinding("quality", "LOW", AuthPath, 2,
            "public static class AuthEndpoints", "Dài dòng", "Rà soát");
        var report = AiReviewNarrativePolicy.Compose("Auth", [AuthPath],
            new AiReport("Chắc chắn bảo mật lỗi.", "Cần fix security", [finding]));
        Assert.Single(report.Findings);
        Assert.Contains("Có 1 nhận xét", report.Summary);
        Assert.DoesNotContain("Chắc chắn", report.Summary);
    }
}
