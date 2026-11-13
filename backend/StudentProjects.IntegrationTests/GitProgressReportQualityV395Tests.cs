using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using StudentProjects.Api.Services;
using Xunit;

namespace StudentProjects.IntegrationTests;

public sealed class GitProgressReportQualityV395Tests
{
    private const string ApiPath = "backend/StudentProjects.Api/Endpoints/GitHubV10Endpoints.cs";
    private const string TestPath = "backend/StudentProjects.IntegrationTests/GitHubV10IntegrationTests.cs";
    private const string UiPath = "frontend/src/views/GitHubV10View.vue";
    private const string NewRoute = "+api.MapPost(\"/projects/{projectId:guid}/repositories\", Link);";
    private const string OldList = "-  list: (token?: string) => request<unknown>('/repositories', { method: 'GET' }, token),";
    private const string RemoveLink = "+              <button v-if=\"isStudent\" type=\"button\" :disabled=\"busy\" @click=\"removeLink(r)\">Gỡ liên kết</button>";

    [Fact]
    public void Deleted_lines_remain_in_git_stats_but_never_reach_model_as_new_work()
    {
        var file = new ProgressFile("frontend/src/features/github/github.api.ts", "modified", 1, 1,
            OldList + "\n+  list: (projectId: string) => request<unknown>(`/v10/projects/${projectId}/repositories`),");
        var lines = GitProgressMeaningPolicy.MeaningfulLines(file);
        Assert.DoesNotContain(lines, x => x.StartsWith('-'));
        Assert.Single(lines);
        var removedRef = new ProgressEvidenceReference("L1", file.Path, OldList);
        var fakeClaim = new ProgressEvidenceSelection("L1",
            "Bổ sung hàm list nhận token để gọi GET /repositories và trả về kết quả.");
        Assert.Empty(GitProgressMeaningPolicy.ScreenMeaningfulById([fakeClaim], [removedRef], [file]));
        var diagnostics = GitProgressMeaningPolicy.ExplainRejectionsById([fakeClaim], [removedRef], [file]);
        Assert.Equal(1, diagnostics.NonFunctionalLine);
        Assert.Equal(1, file.Deletions);
        Assert.Equal(1, file.Additions);
    }

    [Fact]
    public void Test_login_setup_only_is_dropped_without_hiding_security_or_created_checks()
    {
        const string login = "+        Assert.Equal(HttpStatusCode.OK, login.StatusCode);";
        const string created = "+        Assert.Equal(HttpStatusCode.Created, response.StatusCode);";
        const string unauthorized = "+        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);";
        Assert.True(GitProgressMeaningPolicy.IsLowValueTestSetupLine(TestPath, login));
        Assert.False(GitProgressMeaningPolicy.IsLowValueTestSetupLine(TestPath, created));
        Assert.False(GitProgressMeaningPolicy.IsLowValueTestSetupLine(TestPath, unauthorized));
        Assert.False(GitProgressMeaningPolicy.IsLowValueTestSetupLine(ApiPath, login));
        var file = new ProgressFile(TestPath, "added", 3, 0,
            string.Join("\n", new[] { login, created, unauthorized }));
        Assert.DoesNotContain(login, GitProgressMeaningPolicy.MeaningfulLines(file));
        Assert.Contains(created, GitProgressMeaningPolicy.MeaningfulLines(file));
        Assert.Contains(unauthorized, GitProgressMeaningPolicy.MeaningfulLines(file));
        var ids = new[]
        {
            new ProgressEvidenceReference("L1", TestPath, login),
            new ProgressEvidenceReference("L2", TestPath, created),
            new ProgressEvidenceReference("L3", TestPath, unauthorized)
        };
        var candidates = new[]
        {
            new ProgressEvidenceSelection("L1", "Bổ sung assertion so sánh login.StatusCode với HttpStatusCode.OK."),
            new ProgressEvidenceSelection("L2", "Bổ sung assertion kiểm tra phản hồi Created sau yêu cầu liên kết repository."),
            new ProgressEvidenceSelection("L3", "Bổ sung assertion kiểm tra phản hồi Unauthorized khi yêu cầu chưa đăng nhập.")
        };
        var accepted = GitProgressMeaningPolicy.ScreenMeaningfulById(candidates, ids, [file]);
        Assert.Equal(2, accepted.Count);
        Assert.DoesNotContain(accepted, e => e.Evidence == login);
        var diagnostics = GitProgressMeaningPolicy.ExplainRejectionsById(candidates, ids, [file]);
        Assert.Equal(1, diagnostics.TestSetupOnly);
        Assert.Equal(2, diagnostics.Eligible);
    }

    [Fact]
    public void Existing_english_ui_comment_is_normalized_into_grounded_vietnamese()
    {
        var file = new ProgressFile(UiPath, "modified", 1, 0, RemoveLink);
        var reference = new ProgressEvidenceReference("L1", UiPath, RemoveLink);
        const string english = "The code snippet shows a button that calls removeLink when a student clicks it while the operation is not busy.";
        var accepted = Assert.Single(GitProgressMeaningPolicy.ScreenMeaningfulById(
            [new ProgressEvidenceSelection("L1", english)], [reference], [file]));
        Assert.StartsWith("Bổ sung", accepted.Description);
        Assert.Contains("removeLink", accepted.Description);
        Assert.DoesNotContain("The code snippet", accepted.Description);
        Assert.Equal(RemoveLink, accepted.Evidence);
    }

    [Fact]
    public void Existing_english_api_comment_is_normalized_without_inventing_a_route()
    {
        var file = new ProgressFile(ApiPath, "added", 1, 0, NewRoute);
        var reference = new ProgressEvidenceReference("L1", ApiPath, NewRoute);
        const string english = "The API exposes a POST /projects/{projectId:guid}/repositories route that invokes Link for requests.";
        var accepted = Assert.Single(GitProgressMeaningPolicy.ScreenMeaningfulById(
            [new ProgressEvidenceSelection("L1", english)], [reference], [file]));
        Assert.StartsWith("Bổ sung", accepted.Description);
        Assert.Contains("POST /projects/{projectId:guid}/repositories", accepted.Description);
        const string hallucination = "The API exposes POST /admin/delete route to remove project records.";
        Assert.Empty(GitProgressMeaningPolicy.ScreenMeaningfulById(
            [new ProgressEvidenceSelection("L1", hallucination)], [reference], [file]));
    }

    [Fact]
    public void Generic_purpose_explanations_are_rejected_and_concrete_ones_are_retained()
    {
        var fact = new ProgressFeatureFact("Frontend", "Tích hợp giao diện người dùng",
            "Bổ sung giao diện có sự kiện hoặc liên kết dữ liệu, lời gọi API phía frontend.",
            [new ProgressFeatureCitation(UiPath, "+      <select v-model=\"projectId\" :disabled=\"busy || loading\">"),
             new ProgressFeatureCitation("frontend/src/features/github/github.api.ts",
                "+  list: (projectId: string) => request<unknown>(`/v10/projects/${projectId}/repositories`),")]);
        const string generic = "Để thực hiện giao diện cung cấp kết nối với API backend và thực hiện các thao tác trên giao diện.";
        const string concrete = "Giúp người dùng chọn project trước khi truy vấn danh sách repository của project trên giao diện.";
        Assert.Empty(GitProgressAiExplanationPolicy.Screen(
            [new ProgressFeatureExplanation("F1", generic)], [fact]));
        Assert.Single(GitProgressAiExplanationPolicy.Screen(
            [new ProgressFeatureExplanation("F1", concrete)], [fact]));
    }

    [Fact]
    public void Test_explanation_without_concrete_access_case_is_rejected()
    {
        var fact = new ProgressFeatureFact("Kiểm thử", "Bổ sung kiểm thử tự động",
            "Bổ sung kiểm thử hành vi liên kết và xem repository theo quyền.",
            [new ProgressFeatureCitation(TestPath,
                "+public async Task Student_can_link_and_view_but_other_student_cannot_access()")]);
        const string generic = "Để kiểm tra hành vi liên quan đến việc liên kết và xem repository, nhưng không liên quan đến API production.";
        Assert.Empty(GitProgressAiExplanationPolicy.Screen(
            [new ProgressFeatureExplanation("F1", generic)], [fact]));
    }

    [Fact]
    public async Task Model_receives_only_added_lines_and_explanation_receives_verified_source_lines()
    {
        using var handler = new FakeOllama("""
            {"items":[{"evidenceId":"L1","description":"Bổ sung route POST /projects/{projectId:guid}/repositories gọi Link để nhận yêu cầu liên kết repository."}]}
            """);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434/") };
        var ai = new AiGitProgressClient(http, new ConfigurationBuilder().Build());
        var diff = new ProgressDiff(new string('a', 40), new string('b', 40), 1,
            [new ProgressFile(ApiPath, "modified", 1, 0, NewRoute),
             new ProgressFile("frontend/src/features/github/github.api.ts", "modified", 1, 1,
                OldList + "\n+  list: (projectId: string) => request<unknown>(`/v10/projects/${projectId}/repositories`),")], false,
            GitFileCount: 19, GitAdditions: 1809, GitDeletions: 10);
        var analyzed = await ai.AnalyzeAsync(diff, CancellationToken.None);
        Assert.Equal(19, diff.RawFileCount);
        Assert.Equal(1809, diff.RawAdditions);
        Assert.Equal(10, diff.RawDeletions);
        Assert.Equal("qwen2.5-coder:7b|gp-context-v4.0", ai.AnalysisKey);
        Assert.DoesNotContain("list: (token?: string)", handler.LastPrompt);
        Assert.Contains("projects/{projectId:guid}/repositories", handler.LastPrompt);
        // Already cited by the main API feature; don't show it twice.
        Assert.Empty(analyzed.Items);
        Assert.Equal(1, analyzed.Diagnostics?.GroupedFeatureDetail);

        var fact = new ProgressFeatureFact("API", "Thay đổi tuyến API",
            "Bổ sung khai báo POST /projects/{projectId:guid}/repositories.",
            [new ProgressFeatureCitation(ApiPath, NewRoute)]);
        handler.Generated = """{"items":[]}""";
        await ai.ExplainFeaturesWithDiagnosticsAsync([fact], CancellationToken.None);
        Assert.Contains("verifiedAddedLines", handler.LastPrompt);
        Assert.Contains("projects/{projectId:guid}/repositories", handler.LastPrompt);
        Assert.Contains("các thao tác trên giao diện", handler.LastPrompt);
    }

    private sealed class FakeOllama(string generated) : HttpMessageHandler
    {
        public string Generated { get; set; } = generated;
        public string LastPrompt { get; private set; } = "";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            LastPrompt = body.RootElement.GetProperty("prompt").GetString() ?? "";
            return new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(JsonSerializer.Serialize(new { response = Generated, done = true }), Encoding.UTF8, "application/json") };
        }
    }
}
