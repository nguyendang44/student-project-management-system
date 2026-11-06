using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using StudentProjects.Api.Services;
using Xunit;

namespace StudentProjects.IntegrationTests;

public sealed class GitProgressGroundedFallbackV34Tests
{
    [Fact]
    public void Extracts_facts_from_multiple_areas_with_real_sent_evidence()
    {
        var examples = new[]
        {
            File("backend/StudentProjects.Api/Endpoints/P.cs", "+group.MapPost(\"/progress\", Start);"),
            File("backend/StudentProjects.Api/Services/S.cs", "+builder.Services.AddHostedService<GitProgressWorker>();"),
            File("frontend/src/views/P.vue", "+<button @click=\"start\">Run</button>"),
            File("backend/StudentProjects.IntegrationTests/PTests.cs", "+Assert.Equal(2, result.Count);"),
            File("backend/StudentProjects.Infrastructure/Persistence/Migrations/New.cs", "+migrationBuilder.CreateTable(name: \"GitProgressRuns\");")
        };
        var references = examples.Select((file, n) => new ProgressEvidenceReference("L" + (n + 1), file.Path, file.Patch!)).ToArray();
        var actual = GitProgressGroundedFallback.Extract(references, examples);
        Assert.Equal(5, actual.Count);
        Assert.Contains(actual, e => e.Description.Contains("POST /progress"));
        Assert.Contains(actual, e => e.Description.Contains("GitProgressWorker"));
        Assert.Contains(actual, e => e.Description.Contains("start"));
        Assert.Contains(actual, e => e.Description.Contains("Assert.Equal"));
        Assert.Contains(actual, e => e.Description.Contains("GitProgressRuns"));
        Assert.All(actual, e => Assert.True(GitProgressMeaningPolicy.UsefulDescription(e.Description)));
    }

    [Fact]
    public void Ignores_unsent_comment_declaration_and_removed_lines()
    {
        var file = File("backend/StudentProjects.Api/Endpoints/P.cs", "+group.MapPost(\"/progress\", Start);");
        var forged = new[]
        {
            new ProgressEvidenceReference("L1", file.Path, "+group.MapPost(\"/fake\", Start);"),
            new ProgressEvidenceReference("L2", "backend/StudentProjects.Api/Endpoints/Unknown.cs", file.Patch!),
            new ProgressEvidenceReference("L3", file.Path, "+public class P { }"),
            new ProgressEvidenceReference("L4", file.Path, "-group.MapPost(\"/progress\", Start);")
        };
        Assert.Empty(GitProgressGroundedFallback.Extract(forged, [file]));
        var comment = File("backend/StudentProjects.Api/Endpoints/P.cs", "+/// group.MapPost(\"/progress\", Start);");
        Assert.Empty(GitProgressGroundedFallback.Extract(
            [new ProgressEvidenceReference("L1", comment.Path, comment.Patch!)], [comment]));
    }

    [Fact]
    public async Task Uses_rule_based_fallback_only_when_ai_has_no_approved_observations()
    {
        var handler = new StubOllama("The class Analyzer is defined and includes a new endpoint mapping.");
        var client = Client(handler);
        var line = "+api.MapPost(\"/progress\", Start);";
        var result = await client.AnalyzeAsync(Diff(line), CancellationToken.None);
        Assert.Equal(1, result.CandidateCount);
        Assert.True(result.RuleBasedFallback);
        Assert.Single(result.Items);
        Assert.Contains("POST /progress", result.Items[0].Description);
        Assert.Equal(1, result.Diagnostics!.Boilerplate);
        Assert.Equal("qwen2.5-coder:7b|gp-purpose-v3.7", client.AnalysisKey);
    }

    [Fact]
    public async Task Retains_genuine_ai_observations_without_fallback()
    {
        var handler = new StubOllama("Bổ sung route POST /progress để gửi yêu cầu đến handler Start.");
        var result = await Client(handler).AnalyzeAsync(Diff("+api.MapPost(\"/progress\", Start);"), CancellationToken.None);
        Assert.False(result.RuleBasedFallback);
        Assert.Single(result.Items);
        Assert.Equal("Bổ sung route POST /progress để gửi yêu cầu đến handler Start.", result.Items[0].Description);
    }

    [Fact]
    public async Task Does_not_invent_progress_from_boilerplate_without_behavior_lines()
    {
        var handler = new StubOllama("The class Analyzer is defined and includes a new endpoint mapping.");
        var result = await Client(handler).AnalyzeAsync(Diff("+public class Analyzer { }"), CancellationToken.None);
        Assert.Empty(result.Items);
        Assert.False(result.RuleBasedFallback);
    }

    private static ProgressFile File(string path, string patch) => new(path, "added", 1, 0, patch);
    private static ProgressDiff Diff(string patch) => new(new string('a', 40), new string('b', 40), 1,
        [File("backend/StudentProjects.Api/Endpoints/P.cs", patch)], false);
    private static AiGitProgressClient Client(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:11434/") },
            new ConfigurationBuilder().Build());

    private sealed class StubOllama(string description) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var json = JsonSerializer.Serialize(new { response = JsonSerializer.Serialize(new
            {
                items = new[] { new { evidenceId = "L1", description } }
            }), done = true });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }
}
