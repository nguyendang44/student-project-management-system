using StudentProjects.Api.Services;
using Xunit;

namespace StudentProjects.IntegrationTests;

public sealed class AiReviewContextTests
{
    private const string EndpointPath = "backend/StudentProjects.Api/Endpoints/GlobalLecturerCapacityEndpoints.cs";
    private const string EndpointSource = """
        public static class GlobalLecturerCapacityEndpoints
        {
            public static void MapGlobalLecturerCapacityEndpoints(IEndpointRouteBuilder app)
            {
                var group = app.MapGroup("/api/v1/lecturer-capacities/global")
                    .RequireAuthorization(p => p.RequireRole("Student", "Lecturer", "Admin"));
                group.MapGet("", List);
                group.MapPut("/{lecturerId:guid}", Update);
            }
            private static async Task<IResult> List(Db db, CancellationToken ct)
            {
                var people = await db.Users.ToListAsync(ct);
                var items = people.Select(p =>
                {
                    return new { Name = p.Name };
                }).ToList();
                return Results.Ok(items);
            }
            private static async Task<IResult> Update(Guid id, ClaimsPrincipal principal)
            {
                if (!principal.IsInRole("Admin")) return Results.Forbid();
                return Results.Ok();
            }
        }
        """;

    [Fact]
    public void Method_context_includes_route_group_and_complete_List_handler()
    {
        var file = new SourceFile(EndpointPath, EndpointSource);
        var snapshot = new RepositorySnapshot(new string('a', 40), [file], 0);
        var (prompt, sent) = AiSourceContextBuilder.Build(snapshot, "Backend API", [file], 15000, 140);
        Assert.Contains("RequireAuthorization", prompt);
        Assert.Contains("var items = people.Select", prompt);
        Assert.Contains("return Results.Ok(items);", prompt);
        Assert.True(sent[EndpointPath].Contains(6));
        Assert.True(sent[EndpointPath].Contains(15));
    }

    [Fact]
    public void Finds_existing_authorization_on_route_group()
    {
        var file = new SourceFile(EndpointPath, EndpointSource);
        Assert.True(AiAuthorizationContextGuard.HasRouteOrHandlerAuthorization(file, 13));
    }

    [Fact]
    public void Finds_existing_role_check_inside_update_handler()
    {
        var file = new SourceFile(EndpointPath, EndpointSource);
        Assert.True(AiAuthorizationContextGuard.HasRouteOrHandlerAuthorization(file, 20));
    }

    [Fact]
    public void Rejects_Select_claim_when_endpoint_group_is_authorized()
    {
        var file = new SourceFile(EndpointPath, EndpointSource);
        var finding = new AiFinding("security", "MEDIUM", EndpointPath, 13,
            "var items = people.Select(p =>",
            "Đoạn này không có kiểm tra quyền truy cập và có thể để lộ dữ liệu.",
            "Thêm kiểm tra quyền truy cập trước khi trả kết quả.");
        Assert.Null(AiAuthorizationContextGuard.Screen(finding, file));
    }

    [Fact]
    public void Rejects_missing_permission_claim_when_route_context_is_unknown()
    {
        var file = new SourceFile("backend/Api/Endpoints/Other.cs", """
            public static class Other
            {
                private static IResult List(Db db)
                {
                    return Results.Ok(db.Users);
                }
            }
            """);
        var finding = new AiFinding("security", "HIGH", file.Path, 5,
            "return Results.Ok(db.Users);", "Thiếu kiểm tra phân quyền.",
            "Thêm kiểm tra quyền truy cập.");
        Assert.Null(AiAuthorizationContextGuard.Screen(finding, file));
    }

    [Fact]
    public void Does_not_remove_a_different_direct_security_issue()
    {
        var file = new SourceFile("backend/Api/Secrets.cs", "public const string ApiKey = \"example-secret\";");
        var finding = new AiFinding("security", "HIGH", file.Path, 1,
            "public const string ApiKey", "Hardcoded API key visible in source.",
            "Move secrets out of source control.");
        Assert.Equal(finding, AiAuthorizationContextGuard.Screen(finding, file));
    }

    [Fact]
    public void Direct_AllowAnonymous_configuration_is_not_silently_suppressed()
    {
        var file = new SourceFile("backend/Api/Endpoints/Public.cs", """
            public static class Example
            {
                public static void Map(IEndpointRouteBuilder app)
                {
                    app.MapGet("/admin", Get).AllowAnonymous();
                }
                private static IResult Get() { return Results.Ok("sensitive"); }
            }
            """);
        var finding = new AiFinding("security", "HIGH", file.Path, 5,
            "app.MapGet(\"/admin\", Get).AllowAnonymous();",
            "Endpoint cho phép truy cập mà không cần phân quyền.",
            "Kiểm tra tính phù hợp của AllowAnonymous.");
        Assert.Equal("LOW", AiAuthorizationContextGuard.Screen(finding, file)?.Severity);
    }

    [Fact]
    public void Selected_line_sets_exclude_other_lines()
    {
        var file = new SourceFile(EndpointPath, EndpointSource);
        var (prompt, sent) = AiSourceContextBuilder.Build(
            new RepositorySnapshot(new string('a', 40), [file], 0), "Backend API", [file], 15000, 140);
        Assert.True(sent[EndpointPath].Contains(13));
        Assert.False(sent[EndpointPath].Contains(999));
        Assert.DoesNotContain("999 |", prompt);
    }
}
