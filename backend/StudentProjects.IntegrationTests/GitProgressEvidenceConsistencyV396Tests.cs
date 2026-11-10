using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using StudentProjects.Api.Services;
using Xunit;

namespace StudentProjects.IntegrationTests;

public sealed class GitProgressEvidenceConsistencyV396Tests
{
    private const string UiFile = "frontend/src/views/GitHubV10View.vue";
    private const string ApiFile = "frontend/src/features/github/github.api.ts";
    private const string TestFile = "backend/StudentProjects.IntegrationTests/GitHubV10IntegrationTests.cs";
    private const string FrontendLine = "+      <select v-model=\"projectId\" :disabled=\"busy || loading\">";
    private const string FrontendApiLine = "+  list: (projectId: string) => request<GithubRepositoryLink[]>(`/v10/projects/${projectId}/repositories`),";
    private const string TestMethod = "+    public async Task Student_can_link_and_view_but_other_student_cannot_access()";
    private const string TestPost = "+        var response = await student.PostAsJsonAsync($\"/api/v1/v10/projects/{ProjectA}/repositories\",";
    private const string TestCreated = "+        Assert.Equal(HttpStatusCode.Created, response.StatusCode);";

    private static readonly ProgressFeatureFact FrontendFact = new("Frontend", "Tích hợp giao diện người dùng",
        "Bổ sung giao diện có sự kiện hoặc liên kết dữ liệu, lời gọi API phía frontend.",
        [new ProgressFeatureCitation(UiFile, FrontendLine),
         new ProgressFeatureCitation(ApiFile, FrontendApiLine)]);

    private static readonly ProgressFeatureFact TestFact = new("Kiểm thử", "Bổ sung kiểm thử tự động",
        "Bổ sung mã kiểm thử hành vi liên kết repository và quyền truy cập.",
        [new ProgressFeatureCitation(TestFile, TestMethod),
         new ProgressFeatureCitation(TestFile, TestPost),
         new ProgressFeatureCitation(TestFile, TestCreated)]);

    [Fact]
    public void Frontend_explanation_about_registration_date_is_rejected_for_GitHub_only_evidence()
    {
        const string misleading = "Trường ngày giúp người dùng chọn thời điểm bắt đầu đợt đăng ký trên giao diện.";
        Assert.Empty(GitProgressAiExplanationPolicy.Screen(
            [new ProgressFeatureExplanation("F1", misleading)], [FrontendFact]));
        const string grounded = "Giúp người dùng chọn project trước khi truy vấn danh sách repository của project trên giao diện.";
        var result = GitProgressAiExplanationPolicy.Screen(
            [new ProgressFeatureExplanation("F1", grounded)], [FrontendFact]);
        Assert.Single(result);
    }

    [Fact]
    public void Test_explanation_about_Unauthorized_is_rejected_when_only_Created_is_cited()
    {
        const string misleading = "Bài test giúp đối chiếu phản hồi Unauthorized trong tình huống không xác thực.";
        Assert.Empty(GitProgressAiExplanationPolicy.Screen(
            [new ProgressFeatureExplanation("F1", misleading)], [TestFact]));
        const string grounded = "Bài kiểm thử giúp đối chiếu phản hồi Created khi sinh viên gửi yêu cầu liên kết repository.";
        Assert.Single(GitProgressAiExplanationPolicy.Screen(
            [new ProgressFeatureExplanation("F1", grounded)], [TestFact]));
    }

    [Fact]
    public void Status_is_accepted_only_if_the_same_feature_cites_it()
    {
        var authFact = new ProgressFeatureFact("Kiểm thử", "Bổ sung kiểm thử tự động",
            "Bổ sung bài test kiểm tra truy cập trái phép.",
            [new ProgressFeatureCitation(TestFile, "+    public async Task AnonymousRequests_AreRejected()"),
             new ProgressFeatureCitation(TestFile,
                "+        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);")]);
        Assert.Single(GitProgressAiExplanationPolicy.Screen(
            [new ProgressFeatureExplanation("F1", "Bài test giúp đối chiếu Unauthorized khi truy cập không xác thực vào project.")],
            [authFact]));
        Assert.Empty(GitProgressAiExplanationPolicy.Screen(
            [new ProgressFeatureExplanation("F1", "Bài test giúp đối chiếu Created khi truy cập không xác thực vào project.")],
            [authFact]));
    }

    [Fact]
    public void Date_explanation_can_be_used_when_the_same_feature_has_a_real_date_field()
    {
        var dates = new ProgressFeatureFact("Frontend", "Tích hợp giao diện người dùng",
            "Bổ sung trường ngày bắt đầu đợt đăng ký.",
            [new ProgressFeatureCitation("frontend/src/views/TopicsManagementView.vue",
                "+<label>Ngày bắt đầu<input v-model=\"periodForm.startsDate\" type=\"date\" /></label>")]);
        Assert.Single(GitProgressAiExplanationPolicy.Screen(
            [new ProgressFeatureExplanation("F1", "Trường ngày giúp người dùng chọn thời điểm bắt đầu đợt đăng ký trên giao diện.")],
            [dates]));
    }

    [Fact]
    public void Date_specificity_shortcut_requires_an_actual_added_date_control()
    {
        var plain = new ProgressFeatureFact("Frontend", "Tích hợp giao diện người dùng",
            "Bổ sung một trường nhập văn bản thông thường.",
            [new ProgressFeatureCitation(UiFile,
                "+<input v-model=\"periodForm.name\" type=\"text\" />")]);
        const string unsupported = "Trường ngày giúp người dùng chọn thời điểm bắt đầu đợt đăng ký trên giao diện.";
        Assert.Empty(GitProgressAiExplanationPolicy.Screen(
            [new ProgressFeatureExplanation("F1", unsupported)], [plain]));
    }

    [Fact]
    public void Feature_synthesis_joins_a_behavior_named_test_with_request_and_Created_assertion()
    {
        var file = new ProgressFile(TestFile, "added", 3, 0,
            string.Join("\n", new[] { TestMethod, TestPost, TestCreated }));
        var fact = Assert.Single(GitProgressFeatureSynthesis.Extract([file]));
        Assert.Equal("Kiểm thử", fact.Area);
        Assert.Contains("Student_can_link_and_view", fact.Description);
        Assert.Contains("POST", fact.Description);
        Assert.Contains("Created", fact.Description);
        Assert.Equal(3, fact.Citations.Count);
        Assert.Contains("không phải bằng chứng API production mới", fact.Description);
        Assert.Contains("không phải bằng chứng", fact.Description);
        Assert.Contains(fact.Citations, c => c.Evidence == TestPost);
        Assert.Contains(fact.Citations, c => c.Evidence == TestCreated);
        Assert.False(fact.Description.Contains("đã pass", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Supplemental_test_lines_already_covered_by_group_are_not_repeated_as_work_items()
    {
        var fact = Assert.Single(GitProgressFeatureSynthesis.Extract(
            [new ProgressFile(TestFile, "added", 3, 0, string.Join("\n", new[] { TestMethod, TestPost, TestCreated }))]));
        var comments = new[]
        {
            new ProgressEvidence(TestFile, TestPost, "Bổ sung lời gọi bất đồng bộ PostAsJsonAsync trong luồng xử lý."),
            new ProgressEvidence(TestFile, TestCreated, "Bổ sung phép kiểm tra Assert.Equal trong mã kiểm thử."),
            new ProgressEvidence(UiFile, "+<button @click=\"removeLink(r)\">Gỡ liên kết</button>",
                "Bổ sung nút Gỡ liên kết gọi removeLink(r) khi nhấn.")
        };
        var result = GitProgressSupplementalPolicy.CollapseTestDetails(comments, [fact]);
        Assert.Equal(2, result.GroupedCount);
        Assert.Single(result.Kept);
        Assert.Equal(UiFile, result.Kept[0].File);
        // Without any grouped test, evidence must NOT be silently removed.
        Assert.Equal(3, GitProgressSupplementalPolicy.CollapseTestDetails(comments, []).Kept.Count);
    }

    [Fact]
    public async Task Ollama_reply_test_details_are_collapsed_with_consistent_diagnostics()
    {
        // The model cites L2/L3 of the same named test. v3.9.5 rendered them as
        // separate technical work; v3.9.6 keeps the group rather than duplicating.
        using var handler = new FakeOllama("""
            {"items":[
              {"evidenceId":"L2","description":"Bổ sung lời gọi PostAsJsonAsync để gửi yêu cầu liên kết repository."},
              {"evidenceId":"L3","description":"Bổ sung phép kiểm tra Assert.Equal cho phản hồi Created trong mã kiểm thử."}
            ]}
            """);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434/") };
        var ai = new AiGitProgressClient(http, new ConfigurationBuilder().Build());
        var file = new ProgressFile(TestFile, "added", 3, 0, string.Join("\n", new[] { TestMethod, TestPost, TestCreated }));
        var diff = new ProgressDiff(new string('a', 40), new string('b', 40), 1, [file], false,
            GitFileCount: 19, GitAdditions: 1809, GitDeletions: 10);
        var result = await ai.AnalyzeAsync(diff, CancellationToken.None);
        Assert.Equal("qwen2.5-coder:7b|gp-actions-v3.9.7", ai.AnalysisKey);
        Assert.Equal(19, diff.RawFileCount);
        Assert.Equal(1809, diff.RawAdditions);
        Assert.Equal(10, diff.RawDeletions);
        Assert.Equal(2, result.CandidateCount);
        Assert.Empty(result.Items);
        Assert.True(result.Diagnostics?.GroupedTestDetail > 0);
        Assert.Equal(0, result.Diagnostics?.Eligible);
        Assert.Contains("Evidence JSON:", handler.LastPrompt);
    }

    [Fact]
    public async Task Explain_prompt_contains_no_unrelated_date_or_unauthorized_example()
    {
        using var handler = new FakeOllama("""{"items":[]}""");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434/") };
        var ai = new AiGitProgressClient(http, new ConfigurationBuilder().Build());
        await ai.ExplainFeaturesWithDiagnosticsAsync([FrontendFact], CancellationToken.None);
        Assert.Contains("verifiedAddedLines", handler.LastPrompt);
        Assert.DoesNotContain("Trường ngày giúp người dùng chọn thời điểm bắt đầu", handler.LastPrompt);
        Assert.DoesNotContain("Bài test giúp đối chiếu phản hồi Unauthorized", handler.LastPrompt);
    }

    private sealed class FakeOllama(string generated) : HttpMessageHandler
    {
        public string LastPrompt { get; private set; } = "";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            using var document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            LastPrompt = document.RootElement.GetProperty("prompt").GetString() ?? "";
            return new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(JsonSerializer.Serialize(new { response = generated, done = true }), Encoding.UTF8, "application/json") };
        }
    }
}
