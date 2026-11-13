using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using StudentProjects.Api.Services;
using Xunit;

namespace StudentProjects.IntegrationTests;

public sealed class GitProgressSemanticV43Tests
{
    private const string ApiPath = "backend/StudentProjects.Api/Endpoints/GitHubV10Endpoints.cs";

    private static (IReadOnlyList<ProgressEvidence> Results,
        GitProgressMeaningPolicy.RejectionDiagnostics Diagnostics) Screen(
        string path, string line, string description)
    {
        var reference = new ProgressEvidenceReference("L1", path, line);
        var file = new ProgressFile(path, "added", 1, 0, line);
        var selection = new ProgressEvidenceSelection("L1", description);
        return (GitProgressMeaningPolicy.ScreenMeaningfulById([selection], [reference], [file]),
            GitProgressMeaningPolicy.ExplainRejectionsById([selection], [reference], [file]));
    }

    [Theory]
    [InlineData("+        var api = app.MapGroup(\"/api/v1/v10\").RequireAuthorization();",
        "Bổ sung phương thức Actor để lấy ID người dùng từ ClaimsPrincipal.")]
    [InlineData("+builder.Services.AddMemoryCache();",
        "Bổ sung dịch vụ cache và client GitHub trong cấu hình dịch vụ.")]
    [InlineData("+    string RepositoryUrl, string FullName, string DefaultBranch,",
        "Bổ sung cấu trúc dữ liệu GitHubRepositoryInfo để lưu repository.")]
    public void Claim_about_missing_named_implementation_is_rejected(string line, string description)
    {
        var path = line.Contains("RepositoryUrl", StringComparison.Ordinal) ?
            "backend/StudentProjects.Api/Services/GitHubPublicClient.cs" :
            line.Contains("AddMemoryCache", StringComparison.Ordinal) ?
            "backend/StudentProjects.Api/Program.cs" : ApiPath;
        var (kept, diag) = Screen(path, line, description);
        Assert.Empty(kept);
        Assert.Equal(1, diag.SemanticMismatch);
        Assert.Equal(1, diag.WeakDescription);
        Assert.Equal(0, diag.Eligible);
    }

    [Fact]
    public void Module_endpoint_label_is_metadata_not_a_new_backend_api()
    {
        const string path = "frontend/src/app/modules.ts";
        const string line = "+      \"GET /v10/projects/{projectId}/repositories\",";
        var (kept, diagnostics) = Screen(path, line,
            "Bổ sung API GET /v10/projects/{projectId}/repositories vào backend.");
        Assert.Empty(kept);
        Assert.Equal(1, diagnostics.SupportingDetail);
        Assert.Equal(0, diagnostics.Eligible);
        var plan = GitProgressDiffBatcher.Build([new ProgressFile(path, "modified", 1, 0, line)]);
        Assert.Single(plan.Batches);
        Assert.Empty(plan.References);
        Assert.Contains("GET /v10/projects/{projectId}/repositories", plan.Batches[0].PromptData);
    }

    [Theory]
    [InlineData("backend/StudentProjects.IntegrationTests/GitHubV10IntegrationTests.cs",
        "+    private const string Key = \"v010-TEST-ONLY\";")]
    [InlineData("backend/StudentProjects.IntegrationTests/GitHubV10IntegrationTests.cs",
        "+    private static readonly Guid StudentA = Guid.Parse(\"01000000-0000-4000-8000-000000000001\");")]
    [InlineData("frontend/src/views/GitHubV10View.vue", "+<script setup lang=\"ts\">")]
    [InlineData("frontend/src/views/GitHubV10View.vue", "+const auth = useAuthStore()")]
    [InlineData("scripts/install-github-v010.cjs", "+const fs = require('node:fs');")]
    public void Supporting_scaffolding_is_context_but_not_independent_work(string path, string line)
    {
        Assert.True(GitProgressSemanticEvidencePolicy.IsSupportingOnlyLine(path, line));
        var plan = GitProgressDiffBatcher.Build([new ProgressFile(path, "added", 1, 0, line)]);
        Assert.Single(plan.Batches);
        Assert.Empty(plan.References);
    }

    [Fact]
    public void Sql_comment_is_not_a_database_schema_change()
    {
        const string line = "+-- StudentProjects v0.10 - SQL Server schema repair (safe, idempotent).";
        Assert.False(GitProgressMeaningPolicy.IsFunctionalLine(line));
        var plan = GitProgressDiffBatcher.Build([
            new ProgressFile("scripts/upgrade-github-v010.sql", "added", 1, 0, line)]);
        Assert.Single(plan.Batches);
        Assert.Empty(plan.References);
    }

    [Theory]
    [InlineData(ApiPath, "+    public static void MapGitHubV10Endpoints(this IEndpointRouteBuilder app)",
        "Bổ sung phương thức MapGitHubV10Endpoints để định nghĩa endpoint GitHub v10.")]
    [InlineData(ApiPath, "+api.MapGet(\"/repositories/{id:guid}/commits\", Commits);",
        "Bổ sung route GET /repositories/{id:guid}/commits liên kết handler Commits.")]
    [InlineData("frontend/src/features/github/github.api.ts",
        "+  list: (projectId: string) => request<GithubRepositoryLink[]>(`/v10/projects/${projectId}/repositories`),",
        "Bổ sung lời gọi API phía frontend để liệt kê repository theo project.")]
    public void Correct_exact_line_descriptions_remain_eligible(string path, string line, string description)
    {
        var (kept, diag) = Screen(path, line, description);
        Assert.Single(kept);
        Assert.Equal(0, diag.SemanticMismatch);
        Assert.Equal(1, diag.Eligible);
    }

    [Fact]
    public void Frontend_api_definition_claim_is_not_accepted_as_a_backend_endpoint()
    {
        const string path = "frontend/src/features/github/github.api.ts";
        const string line = "+  link: (projectId: string, repositoryUrl: string) => request(`/v10/projects/${projectId}/repositories`),";
        var (kept, diag) = Screen(path, line, "Bổ sung endpoint link repository to project trong backend.");
        Assert.Empty(kept);
        Assert.Equal(1, diag.SemanticMismatch);
    }

    [Fact]
    public void Related_wiring_is_grouped_but_independent_button_is_kept()
    {
        var facts = new[]
        {
            new ProgressFeatureFact("Frontend", "Tích hợp giao diện", "Bổ sung giao diện chọn project.",
                [new ProgressFeatureCitation("frontend/src/views/GitHubV10View.vue",
                    "+<select v-model=\"projectId\">")]),
            new ProgressFeatureFact("API", "Thay đổi tuyến API", "Bổ sung khai báo route.",
                [new ProgressFeatureCitation(ApiPath, "+api.MapPost(\"/repos\", Link);")])
        };
        var extras = new[]
        {
            new ProgressEvidence("frontend/src/app/router.ts", "+  component: () => import('../views/GitHubV10View.vue')",
                "Bổ sung điều hướng giao diện đến GitHubV10View."),
            new ProgressEvidence("frontend/src/features/github/github.types.ts", "+export interface GithubCommitResult {",
                "Bổ sung kiểu dữ liệu GithubCommitResult cho frontend."),
            new ProgressEvidence("backend/StudentProjects.Api/Program.cs", "+builder.Services.AddMemoryCache();",
                "Bổ sung cache vào cấu hình API."),
            new ProgressEvidence("frontend/src/views/GitHubV10View.vue", "+<button @click=\"removeLink\">Gỡ liên kết</button>",
                "Bổ sung nút gỡ liên kết repository trên giao diện.")
        };
        var (kept, collapsed) = GitProgressSupplementalPolicy.CollapseFeatureOverlap(extras, facts);
        Assert.Equal(3, collapsed);
        Assert.Single(kept);
        Assert.Contains("@click", kept[0].Evidence);
    }

    [Fact]
    public async Task Analyze_counts_semantic_mismatch_without_promoting_it_to_evidence()
    {
        const string source = "+builder.Services.AddMemoryCache();";
        using var http = new HttpClient(new FakeOllama(
            """
            {"items":[{"evidenceId":"L1","description":"Bổ sung dịch vụ cache và client GitHub trong cấu hình dịch vụ."}]}
            """)) { BaseAddress = new Uri("http://127.0.0.1:11434/") };
        var ai = new AiGitProgressClient(http, new ConfigurationBuilder().Build());
        Assert.EndsWith("gp-web-v5.0", ai.AnalysisKey);
        var diff = new ProgressDiff(new string('a', 40), new string('b', 40), 1,
            [new ProgressFile("backend/StudentProjects.Api/Program.cs", "modified", 1, 0, source)], false);
        var result = await ai.AnalyzeAsync(diff, CancellationToken.None);
        Assert.Equal(1, result.RawItemCount);
        Assert.Equal(1, result.Diagnostics!.SemanticMismatch);
        Assert.Equal(0, result.Diagnostics.Eligible);
        Assert.Empty(result.Items);
    }

    [Fact]
    public void No_diff_does_not_create_any_fake_feature_or_ai_coverage()
    {
        var diff = new ProgressDiff(new string('a', 40), new string('b', 40), 1, [], false);
        Assert.Empty(GitProgressFeatureSynthesis.Extract(diff.Files));
        var plan = GitProgressDiffBatcher.Build(diff.Files);
        Assert.Empty(plan.Batches);
        Assert.Equal(0, plan.EligibleFiles);
    }

    private sealed class FakeOllama(string generated) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { response = generated, done = true }),
                    Encoding.UTF8, "application/json")
            });
    }
}
