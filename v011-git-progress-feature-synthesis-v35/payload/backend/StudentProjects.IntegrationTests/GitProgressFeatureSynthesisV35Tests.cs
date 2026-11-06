using StudentProjects.Api.Services;
using Xunit;

namespace StudentProjects.IntegrationTests;

public sealed class GitProgressFeatureSynthesisV35Tests
{
    [Fact]
    public void Groups_cross_file_changes_and_cites_real_added_lines()
    {
        var files = new[]
        {
            File("backend/StudentProjects.Api/Endpoints/CodeAnalysisV11Endpoints.cs", """
                +        api.MapPost("/repositories/{id:guid}/analyses", Start).RequireRateLimiting("analysis-start");
                +        api.MapGet("/repositories/{id:guid}/analyses", List);
                """),
            File("backend/StudentProjects.Api/Services/AiCodeReviewClient.cs", """
                +        using var request = new HttpRequestMessage(HttpMethod.Post, "api/generate");
                +        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
                """),
            File("backend/StudentProjects.Api/Services/CodeAnalysisWorker.cs", """
                +        var run = await db.CodeAnalysisRuns.FirstOrDefaultAsync(r => r.Status == "PENDING", ct);
                +        var ai = await services.GetRequiredService<AiCodeReviewClient>().AnalyzeAsync(snapshot, ct);
                """),
            File("frontend/src/views/CodeAnalysisV11View.vue", "+<button @click=\"start\">Phân tích</button>"),
            File("frontend/src/features/codeAnalysis/codeAnalysis.api.ts",
                "+  start: (id: string) => request<Run>(`/v11/repositories/${id}/analyses`, { method: 'POST' }),"),
            File("backend/StudentProjects.Infrastructure/Persistence/Migrations/V011.cs", """
                +            migrationBuilder.CreateTable(
                +                name: "CodeAnalysisRuns",
                +            migrationBuilder.CreateTable(
                +                name: "CodeAnalysisFindings",
                """),
            File("backend/StudentProjects.IntegrationTests/AiAuthSemanticsTests.cs", """
                +    public void Anonymous_login_route_is_allowed()
                +        Assert.True(result.AllowAnonymous);
                +        group.MapPost("/login", Login).AllowAnonymous();
                """)
        };
        var facts = GitProgressFeatureSynthesis.Extract(files);
        Assert.Contains(facts, x => x.Area == "API" && x.Description.Contains("POST /repositories/"));
        Assert.Contains(facts, x => x.Area == "AI/Backend" && x.Title.Contains("Kết nối"));
        Assert.Contains(facts, x => x.Area == "AI/Backend" && x.Title.Contains("Luồng"));
        Assert.Contains(facts, x => x.Area == "Frontend" && x.Citations.Select(c => c.File).Distinct().Count() == 2);
        Assert.Contains(facts, x => x.Area == "CSDL" && x.Description.Contains("CodeAnalysisRuns") &&
                                   x.Description.Contains("CodeAnalysisFindings"));
        Assert.Contains(facts, x => x.Area == "Kiểm thử" && x.Description.Contains("Anonymous_login_route_is_allowed"));
        Assert.DoesNotContain(facts.Where(x => x.Area == "API"), x => x.Description.Contains("/login"));
        Assert.All(facts, fact =>
        {
            Assert.NotEmpty(fact.Citations);
            Assert.All(fact.Citations, citation =>
            {
                Assert.Contains(files, f => f.Path == citation.File &&
                    GitProgressEvidencePolicy.EvidenceLines(f, 500).Contains(citation.Evidence));
                Assert.StartsWith("+", citation.Evidence);
            });
        });
    }

    [Fact]
    public void Route_in_test_is_not_reported_as_production()
    {
        var data = new[] { File("backend/StudentProjects.IntegrationTests/AuthTest.cs",
            "+group.MapPost(\"/login\", Login).AllowAnonymous().RequireRateLimiting(\"auth-login\");") };
        Assert.DoesNotContain(GitProgressFeatureSynthesis.Extract(data), x => x.Area == "API");
    }

    [Fact]
    public void Generic_save_and_lookup_are_not_features()
    {
        var data = new[] { File("backend/StudentProjects.Api/Services/CodeAnalysisWorker.cs", """
            +        await db.SaveChangesAsync(stoppingToken);
            +        var run = await db.CodeAnalysisRuns.FirstOrDefaultAsync(x => x.Id == id, ct);
            """) };
        Assert.Empty(GitProgressFeatureSynthesis.Extract(data));
    }

    [Fact]
    public void Generic_create_table_without_name_is_not_reported()
    {
        var data = new[] { File("backend/StudentProjects.Infrastructure/Persistence/Migrations/New.cs",
            "+            migrationBuilder.CreateTable(") };
        Assert.DoesNotContain(GitProgressFeatureSynthesis.Extract(data), f => f.Area == "CSDL");
    }

    [Fact]
    public void Ignores_secret_paths_generated_files_and_removed_code()
    {
        var files = new[]
        {
            File("backend/.env", "+api.MapGet(\"/fake\", Handler);"),
            File("backend/StudentProjects.Api/Endpoints/P.cs", "-api.MapGet(\"/old\", Old);"),
            File("backend/StudentProjects.Infrastructure/Persistence/Migrations/New.Designer.cs",
                "+migrationBuilder.CreateTable(name: \"Secrets\");"),
            File("README.md", "+## Features: all complete"),
            File("backend/StudentProjects.Api/Endpoints/Private.cs",
                "+var api_key = \"sk-ABCDEFGHIJKLMNOPQRSTxyz\";")
        };
        Assert.Empty(GitProgressFeatureSynthesis.Extract(files));
    }

    [Fact]
    public void Sees_frontend_actions_after_first_160_lines()
    {
        var padding = string.Concat(Enumerable.Range(0, 170)
            .Select(i => $"+const number{i} = getValue({i});\n"));
        var file = File("frontend/src/views/Progress.vue", padding + "+<button @click=\"submit\">Run</button>");
        Assert.Contains(GitProgressFeatureSynthesis.Extract([file]), f =>
            f.Area == "Frontend" && f.Citations.Any(c => c.Evidence.Contains("@click=\"submit\"")));
    }

    [Fact]
    public void Supports_inline_table_name_in_migration()
    {
        var file = File("backend/StudentProjects.Infrastructure/Persistence/Migrations/New.cs",
            "+migrationBuilder.CreateTable(name: \"GitProgressRuns\", columns: table => new { });");
        Assert.Contains(GitProgressFeatureSynthesis.Extract([file]), f =>
            f.Area == "CSDL" && f.Description.Contains("GitProgressRuns"));
    }

    private static ProgressFile File(string path, string patch) => new(path, "added", 1, 0, patch);
}
