using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using StudentProjects.Api.Services;
using Xunit;

namespace StudentProjects.IntegrationTests;

public sealed class GitProgressDescriptionQualityV394Tests
{
    private const string ApiLine = "+api.MapPost(\"/projects/{projectId:guid}/repositories\", Link);";
    private const string ApiFile = "backend/StudentProjects.Api/Endpoints/GitHubV10Endpoints.cs";
    private const string SetupLine = "+await db.Database.SaveChangesAsync();";
    private const string TestFile = "backend/StudentProjects.IntegrationTests/GitHubV10IntegrationTests.cs";

    [Theory]
    [InlineData("Bổ sung route POST /progress được khai báo để tiếp nhận yêu cầu phân tích tiến độ.")]
    [InlineData("Cập nhật route GET /repositories được khai báo để giao diện truy xuất danh sách repository.")]
    public void Actionable_descriptions_with_declaration_words_are_no_longer_false_boilerplate(string description)
    {
        Assert.Equal(GitProgressMeaningPolicy.DescriptionIssue.None,
            GitProgressMeaningPolicy.CheckDescription(description));
    }

    [Theory]
    [InlineData("The class Analyzer is defined and nothing more.")]
    [InlineData("The code snippet is a summary of the class and its methods.")]
    [InlineData("Được khai báo là một lớp không có thao tác cụ thể nào.")]
    [InlineData("Đã hoàn thành toàn bộ hệ thống và đảm bảo an toàn bảo mật.")]
    public void Pure_declarations_and_overclaims_remain_rejected(string description)
    {
        Assert.False(GitProgressMeaningPolicy.UsefulDescription(description));
    }

    [Fact]
    public void Evidence_id_and_exact_line_remain_mandatory_for_useful_descriptions()
    {
        var file = new ProgressFile(ApiFile, "added", 1, 0, ApiLine);
        var refs = new[] { new ProgressEvidenceReference("L1", ApiFile, ApiLine) };
        const string desc = "Bổ sung route POST /projects/{projectId:guid}/repositories được khai báo để gọi Link.";
        var good = GitProgressMeaningPolicy.ScreenMeaningfulById(
            [new ProgressEvidenceSelection("L1", desc)], refs, [file]);
        Assert.Single(good);
        Assert.Equal(ApiLine, good[0].Evidence);
        var fake = GitProgressMeaningPolicy.ScreenMeaningfulById(
            [new ProgressEvidenceSelection("L404", desc)], refs, [file]);
        Assert.Empty(fake);
        var missing = GitProgressMeaningPolicy.ExplainRejectionsById(
            [new ProgressEvidenceSelection("L404", desc)], refs, [file]);
        Assert.Equal(1, missing.UnknownId);
    }

    [Fact]
    public void Fake_named_route_is_blocked_even_when_evidence_id_is_real()
    {
        var file = new ProgressFile(ApiFile, "added", 1, 0, ApiLine);
        var refs = new[] { new ProgressEvidenceReference("L1", ApiFile, ApiLine) };
        const string hallucination = "Bổ sung route POST /admin/delete để xóa dữ liệu mà dự án đang quản lý.";
        Assert.True(GitProgressMeaningPolicy.HasUncitedRoute(hallucination, ApiLine));
        Assert.Empty(GitProgressMeaningPolicy.ScreenMeaningfulById(
            [new ProgressEvidenceSelection("L1", hallucination)], refs, [file]));
        var diagnostics = GitProgressMeaningPolicy.ExplainRejectionsById(
            [new ProgressEvidenceSelection("L1", hallucination)], refs, [file]);
        Assert.Equal(1, diagnostics.UnsupportedClaim);
        Assert.Equal(0, diagnostics.Eligible);
    }

    [Fact]
    public void Test_database_setup_remains_filtered_without_filtering_production_save()
    {
        Assert.True(GitProgressMeaningPolicy.IsLowValueTestSetupLine(TestFile, SetupLine));
        Assert.Empty(GitProgressMeaningPolicy.MeaningfulLines(
            new ProgressFile(TestFile, "modified", 1, 0, SetupLine)));
        const string production = "backend/StudentProjects.Api/Services/RepositoryLinkService.cs";
        Assert.False(GitProgressMeaningPolicy.IsLowValueTestSetupLine(production, SetupLine));
    }

    [Fact]
    public async Task Model_is_prompted_for_vietnamese_specific_edits_and_verified_citations()
    {
        using var handler = new FakeOllama("""
            {"items":[{"evidenceId":"L1","description":"Bổ sung route POST /projects/{projectId:guid}/repositories gọi Link để tiếp nhận liên kết repository."}]}
            """);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434/") };
        var ai = new AiGitProgressClient(http, new ConfigurationBuilder().Build());
        var diff = new ProgressDiff(new string('a', 40), new string('b', 40), 1,
            [new ProgressFile(ApiFile, "added", 1, 0, ApiLine)], false);

        var result = await ai.AnalyzeAsync(diff, CancellationToken.None);
        Assert.Contains("gp-web-v5.0", ai.AnalysisKey);
        Assert.Equal(1, result.CandidateCount);
        // Valid AI evidence that duplicates the main API fact is grouped,
        // not shown as a second independent change.
        Assert.Equal(0, result.Diagnostics?.Eligible);
        Assert.Equal(1, result.Diagnostics?.GroupedFeatureDetail);
        Assert.False(result.RuleBasedFallback);
        Assert.Empty(result.Items);
        Assert.Contains("TIẾNG VIỆT", handler.LastPrompt);
        Assert.DoesNotContain("EnsureCreatedAsync", handler.LastPrompt);
        Assert.Contains("CHỈ trích ID", handler.LastPrompt);
        Assert.Contains("Diff JSON", handler.LastPrompt);
    }

    [Fact]
    public async Task Model_claiming_runtime_success_still_fails_even_with_real_evidence_id()
    {
        using var handler = new FakeOllama("""
            {"items":[{"evidenceId":"L1","description":"Bổ sung route POST /projects/{projectId:guid}/repositories đã chạy thành công và đảm bảo bảo mật tuyệt đối."}]}
            """);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434/") };
        var ai = new AiGitProgressClient(http, new ConfigurationBuilder().Build());
        var diff = new ProgressDiff(new string('a', 40), new string('b', 40), 1,
            [new ProgressFile(ApiFile, "added", 1, 0, ApiLine)], false);
        var result = await ai.AnalyzeAsync(diff, CancellationToken.None);
        Assert.Equal(0, result.Diagnostics?.Eligible);
        Assert.Equal(1, result.Diagnostics?.UnsupportedClaim);
        // Rule fallback is also an exact citation of the main API fact.
        Assert.False(result.RuleBasedFallback);
        Assert.Empty(result.Items);
    }

    [Fact]
    public void A_grounded_repository_use_case_can_be_explained_without_inventing_routes()
    {
        var fact = new ProgressFeatureFact("API", "Thay đổi tuyến API",
            "Bổ sung khai báo POST /projects/{projectId:guid}/repositories, GET /repositories/{id:guid}/commits.",
            [new ProgressFeatureCitation(ApiFile, ApiLine),
             new ProgressFeatureCitation(ApiFile,
                "+api.MapGet(\"/repositories/{id:guid}/commits\", Commits);")]);
        const string explanation = "Các route giúp frontend liên kết repository với project và tra cứu commit để theo dõi mã nguồn.";
        Assert.Single(GitProgressAiExplanationPolicy.Screen(
            [new ProgressFeatureExplanation("F1", explanation)], [fact]));
        Assert.Empty(GitProgressAiExplanationPolicy.Screen(
            [new ProgressFeatureExplanation("F1", "Các route giúp frontend gửi yêu cầu đến /admin/delete để xóa dữ liệu project.")],
            [fact]));
    }

    [Fact]
    public async Task Second_prompt_only_explains_existing_facts_and_does_not_replace_verified_groups()
    {
        using var handler = new FakeOllama("""
            {"items":[]}
            """);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434/") };
        var ai = new AiGitProgressClient(http, new ConfigurationBuilder().Build());
        var fact = new ProgressFeatureFact("API", "Thay đổi tuyến API",
            "Bổ sung khai báo POST /projects/{projectId:guid}/repositories.",
            [new ProgressFeatureCitation(ApiFile, ApiLine)]);
        var result = await ai.ExplainFeaturesWithDiagnosticsAsync([fact], CancellationToken.None);
        Assert.Empty(result.Items);
        Assert.Contains("verifiedEdit", handler.LastPrompt);
        Assert.Contains("factId", handler.LastPrompt);
        // v3.9.6 deliberately removed hard-coded feature examples from the
        // prompt. Assert on the real, per-feature evidence instead.
        Assert.Contains("verifiedAddedLines", handler.LastPrompt);
        Assert.Contains("/projects/{projectId:guid}/repositories", handler.LastPrompt);
        Assert.Contains("api.MapPost", handler.LastPrompt);
        Assert.Single(GitProgressFeatureReport.Build([fact], result.Items));
    }

    private sealed class FakeOllama(string generated) : HttpMessageHandler
    {
        public string LastPrompt { get; private set; } = "";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            LastPrompt = payload.RootElement.GetProperty("prompt").GetString() ?? "";
            var response = JsonSerializer.Serialize(new { response = generated, done = true });
            return new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent(response, Encoding.UTF8, "application/json") };
        }
    }
}
