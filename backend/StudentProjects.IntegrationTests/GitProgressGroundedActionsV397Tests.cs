using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using StudentProjects.Api.Services;
using Xunit;

namespace StudentProjects.IntegrationTests;

public sealed class GitProgressGroundedActionsV397Tests
{
    private const string ApiPath = "backend/StudentProjects.Api/Endpoints/GitHubV10Endpoints.cs";
    private const string UiPath = "frontend/src/views/GitHubV10View.vue";
    private const string TestPath = "backend/StudentProjects.IntegrationTests/GitHubV10IntegrationTests.cs";
    private const string ApiLine = "+api.MapPost(\"/projects/{projectId:guid}/repositories\", Link);";
    private const string ButtonLine = "+<button v-if=\"isStudent\" type=\"button\" :disabled=\"busy\" @click=\"removeLink(r)\">Gỡ liên kết</button>";
    private const string ModelLine = "+<select v-model=\"projectId\" :disabled=\"busy || loading\">";
    private const string CreatedLine = "+Assert.Equal(HttpStatusCode.Created, response.StatusCode);";

    private static IReadOnlyList<ProgressEvidence> Select(string path, string line, string description)
    {
        var input = new ProgressFile(path, "modified", 1, 0, line);
        return GitProgressMeaningPolicy.ScreenMeaningfulById(
            [new ProgressEvidenceSelection("L1", description)],
            [new ProgressEvidenceReference("L1", path, line)], [input]);
    }

    [Fact]
    public void Exact_named_POST_route_supplies_action_without_accepting_generic_AI_language()
    {
        const string model = "The endpoint for POST /projects/{projectId:guid}/repositories has handler Link.";
        Assert.Equal(GitProgressMeaningPolicy.DescriptionIssue.MissingAction,
            GitProgressMeaningPolicy.CheckDescription(model));
        var kept = Assert.Single(Select(ApiPath, ApiLine, model));
        Assert.Contains("POST /projects/{projectId:guid}/repositories", kept.Description);
        Assert.StartsWith("Bổ sung", kept.Description);
        Assert.Equal(ApiLine, kept.Evidence);
        var diag = GitProgressMeaningPolicy.ExplainRejectionsById(
            [new ProgressEvidenceSelection("L1", model)],
            [new ProgressEvidenceReference("L1", ApiPath, ApiLine)],
            [new ProgressFile(ApiPath, "modified", 1, 0, ApiLine)]);
        Assert.Equal(0, diag.MissingAction);
        Assert.Equal(1, diag.Eligible);
    }

    [Fact]
    public void Specific_UI_handler_is_normalized_to_a_real_added_click_action()
    {
        const string model = "The user interface button has an event handler removeLink(r) here.";
        var kept = Assert.Single(Select(UiPath, ButtonLine, model));
        Assert.Contains("Gỡ liên kết", kept.Description);
        Assert.Contains("removeLink(r)", kept.Description);
    }

    [Fact]
    public void Exact_v_model_binding_can_supply_a_form_action()
    {
        const string model = "The projectId value is the form selection field shown here.";
        var kept = Assert.Single(Select(UiPath, ModelLine, model));
        Assert.Contains("v-model", kept.Description);
        Assert.Contains("projectId", kept.Description);
    }

    [Theory]
    [InlineData("The repository code now has convenient profile features and tools.")]
    [InlineData("The route POST /admin/delete has handler Link in this file.")]
    [InlineData("The route DELETE /projects/{projectId:guid}/repositories has handler Link.")]
    public void Generic_invented_or_wrong_route_claims_are_not_recovered(string description)
    {
        Assert.Empty(Select(ApiPath, ApiLine, description));
    }

    [Fact]
    public void Rewriting_does_not_infer_test_success_or_a_new_permission()
    {
        const string testModel = "The assertion has status Created and response data in this method.";
        Assert.Empty(Select(TestPath, CreatedLine, testModel));
        const string wrongRole = "The lecturer is the actor for the removeLink(r) button in this view.";
        Assert.Empty(Select(UiPath, ButtonLine, wrongRole));
    }

    [Fact]
    public void Screening_keeps_server_evidence_id_and_added_line_guards()
    {
        const string desc = "The endpoint for POST /projects/{projectId:guid}/repositories has handler Link.";
        var file = new ProgressFile(ApiPath, "modified", 1, 0, ApiLine);
        Assert.Empty(GitProgressMeaningPolicy.ScreenMeaningfulById(
            [new ProgressEvidenceSelection("L999", desc)],
            [new ProgressEvidenceReference("L1", ApiPath, ApiLine)], [file]));
        Assert.Empty(Select(ApiPath, "-api.MapPost(\"/projects/{projectId:guid}/repositories\", Link);", desc));
    }

    [Fact]
    public void Exact_group_citation_collapses_but_distinct_UI_button_remains()
    {
        var facts = new[]
        {
            new ProgressFeatureFact("API", "Thay đổi tuyến API", "Bổ sung tuyến POST cho project.",
                [new ProgressFeatureCitation(ApiPath, ApiLine)]),
            new ProgressFeatureFact("Frontend", "Tích hợp giao diện", "Bổ sung chọn project.",
                [new ProgressFeatureCitation(UiPath, ModelLine)])
        };
        var extras = new[]
        {
            new ProgressEvidence(ApiPath, ApiLine, "Bổ sung khai báo POST liên kết repository."),
            new ProgressEvidence(UiPath, ModelLine, "Bổ sung chọn project trong biểu mẫu."),
            new ProgressEvidence(UiPath, ButtonLine, "Bổ sung nút Gỡ liên kết gọi removeLink(r) khi nhấn.")
        };
        var result = GitProgressSupplementalPolicy.CollapseFeatureOverlap(extras, facts);
        Assert.Equal(2, result.GroupedCount);
        var kept = Assert.Single(result.Kept);
        Assert.Equal(ButtonLine, kept.Evidence);
        Assert.Equal(3, GitProgressSupplementalPolicy.CollapseFeatureOverlap(extras, []).Kept.Count);
    }

    [Fact]
    public async Task Ollama_route_selection_already_in_main_group_is_not_an_extra_work_item()
    {
        using var handler = new FakeOllama("""
            {"items":[{"evidenceId":"L1","description":"The endpoint for POST /projects/{projectId:guid}/repositories has handler Link."}]}
            """);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434/") };
        var ai = new AiGitProgressClient(http, new ConfigurationBuilder().Build());
        var diff = new ProgressDiff(new string('a', 40), new string('b', 40), 1,
            [new ProgressFile(ApiPath, "added", 1, 0, ApiLine)], false,
            GitFileCount: 19, GitAdditions: 1809, GitDeletions: 10);
        var result = await ai.AnalyzeAsync(diff, CancellationToken.None);
        Assert.Equal("qwen2.5-coder:7b|gp-actions-v3.9.7", ai.AnalysisKey);
        Assert.Equal(19, diff.RawFileCount);
        Assert.Equal(1809, diff.RawAdditions);
        Assert.Equal(10, diff.RawDeletions);
        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.Diagnostics?.GroupedFeatureDetail);
        Assert.Equal(0, result.Diagnostics?.Eligible);
        Assert.Empty(result.Items);
        Assert.Contains("Evidence JSON:", handler.LastPrompt);
    }

    private sealed class FakeOllama(string generated) : HttpMessageHandler
    {
        public string LastPrompt { get; private set; } = "";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            using var document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            LastPrompt = document.RootElement.GetProperty("prompt").GetString() ?? "";
            var body = JsonSerializer.Serialize(new { response = generated, done = true });
            return new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
