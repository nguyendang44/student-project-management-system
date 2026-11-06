using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using StudentProjects.Api.Services;
using Xunit;

namespace StudentProjects.IntegrationTests;

public sealed class GitProgressEvidenceIdTests
{
    [Fact]
    public void Valid_id_resolves_to_exact_server_evidence()
    {
        var file = new ProgressFile("backend/Services/Review.cs", "added", 1, 0,
            "+public Task AnalyzeAsync() => RunAsync();");
        var references = new[] { new ProgressEvidenceReference("L1", file.Path,
            "+public Task AnalyzeAsync() => RunAsync();") };
        var choices = new[] { new ProgressEvidenceSelection("L1", "Bổ sung phương thức AnalyzeAsync.") };
        var results = GitProgressEvidencePolicy.ScreenById(choices, references, [file]);
        var item = Assert.Single(results);
        Assert.Equal(file.Path, item.File);
        Assert.Equal("+public Task AnalyzeAsync() => RunAsync();", item.Evidence);
    }

    [Fact]
    public void Invented_or_unsent_evidence_ids_are_rejected()
    {
        var file = new ProgressFile("src/a.cs", "modified", 1, 0, "+public void Execute() {};");
        var references = new[]
        {
            new ProgressEvidenceReference("L1", file.Path, "+public void Execute() {};")
        };
        var results = GitProgressEvidencePolicy.ScreenById(
            [new("L999", "Không tồn tại."), new("L2", "Không tồn tại.")], references, [file]);
        Assert.Empty(results);
    }

    [Fact]
    public void Forged_id_to_non_sent_line_still_fails_exact_line_guard()
    {
        var file = new ProgressFile("src/a.cs", "modified", 1, 0, "+public void Execute() {};");
        var references = new[] { new ProgressEvidenceReference("L1", file.Path, "+invented()") };
        Assert.Empty(GitProgressEvidencePolicy.ScreenById(
            [new("L1", "Không có bằng chứng.")], references, [file]));
    }

    [Fact]
    public void Duplicate_ids_are_rejected_even_when_lines_match()
    {
        var file = new ProgressFile("src/a.cs", "modified", 1, 0, "+public void Execute() {};");
        var references = new[]
        {
            new ProgressEvidenceReference("L1", file.Path, "+public void Execute() {};"),
            new ProgressEvidenceReference("L1", file.Path, "+public void Execute() {};")
        };
        Assert.Empty(GitProgressEvidencePolicy.ScreenById(
            [new("L1", "Mơ hồ.")], references, [file]));
    }

    [Fact]
    public async Task Ollama_can_cite_a_real_line_by_id_without_copying_it()
    {
        var responder = new OllamaHandler("L1");
        var client = new AiGitProgressClient(new HttpClient(responder)
        { BaseAddress = new Uri("http://127.0.0.1:11434/") }, new ConfigurationBuilder().Build());
        var file = new ProgressFile("backend/Endpoints/Health.cs", "added", 1, 0,
            "+app.MapGet(\"/health\", () => \"ok\");");
        var diff = new ProgressDiff(new string('a', 40), new string('b', 40), 1, [file], false);
        var result = await client.AnalyzeAsync(diff, CancellationToken.None);
        Assert.Equal(1, result.SentFiles);
        Assert.Equal(1, result.CandidateCount);
        var evidence = Assert.Single(result.Items);
        Assert.Equal(file.Path, evidence.File);
        Assert.Equal("+app.MapGet(\"/health\", () => \"ok\");", evidence.Evidence);
        Assert.Contains("L1", responder.LastPrompt);
        Assert.Contains("|gp-purpose-v3.7", client.AnalysisKey);
        Assert.Equal("qwen2.5-coder:7b", client.Model);
    }

    [Fact]
    public async Task Ollama_with_invalid_id_cannot_produce_a_fake_finding()
    {
        var client = new AiGitProgressClient(new HttpClient(new OllamaHandler("L404"))
        { BaseAddress = new Uri("http://127.0.0.1:11434/") }, new ConfigurationBuilder().Build());
        var file = new ProgressFile("src/x.cs", "added", 1, 0, "+public void Run() {};");
        var result = await client.AnalyzeAsync(
            new ProgressDiff(new string('a', 40), new string('b', 40), 1, [file], false),
            CancellationToken.None);
        Assert.Equal(1, result.CandidateCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public void Summary_reports_file_areas_without_claiming_feature_completion()
    {
        var diff = new ProgressDiff(new string('a', 40), new string('b', 40), 1,
        [
            new ProgressFile("backend/StudentProjects.Api/Program.cs", "modified", 5, 2, "+x = 1;"),
            new ProgressFile("backend/StudentProjects.IntegrationTests/ReviewTests.cs", "added", 10, 0, "+x = 1;"),
            new ProgressFile("frontend/src/views/A.vue", "added", 4, 0, "+hello world")
        ], false);
        var summary = GitProgressEvidencePolicy.ComposeSummary(diff, 2, 0);
        Assert.Contains("backend: 1 file", summary);
        Assert.Contains("kiểm thử: 1 file", summary);
        Assert.Contains("frontend: 1 file", summary);
        Assert.Contains("không chứng minh", summary);
    }

    private sealed class OllamaHandler(string evidenceId) : HttpMessageHandler
    {
        public string LastPrompt { get; private set; } = "";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal("/api/generate", request.RequestUri?.AbsolutePath);
            var payload = await request.Content!.ReadAsStringAsync(ct);
            using var json = JsonDocument.Parse(payload);
            LastPrompt = json.RootElement.GetProperty("prompt").GetString() ?? "";
            var inner = JsonSerializer.Serialize(new
            {
                items = new[] { new { evidenceId, description = "Thêm endpoint kiểm tra trạng thái." } }
            });
            var result = JsonSerializer.Serialize(new { response = inner, done = true });
            return new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(result, Encoding.UTF8, "application/json") };
        }
    }
}
