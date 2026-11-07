using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using StudentProjects.Api.Services;
using Xunit;

namespace StudentProjects.IntegrationTests;

public sealed class GitProgressDiagnosticsV393Tests
{
    private static GitProgressMeaningPolicy.RejectionDiagnostics RejectedTestSetup() =>
        new(0, 0, 0, 0, 0, 0, 0, TestSetupOnly: 2);

    [Fact]
    public void Persisted_diagnostics_preserve_original_feature_evidence_but_do_not_leak_metadata_into_public_rows()
    {
        var evidence = new object[]
        {
            new { source = "feature-rule", file = "backend/StudentProjects.Api/Endpoints/GitHubV10Endpoints.cs",
                description = "Bổ sung tuyến API liên kết repository", evidence = "+api.MapPost(...)" }
        };
        var d = new ProgressReportDiagnostics(8, 2, 2, 0, 1, RejectedTestSetup(),
            3, 2, 2, 0, "filtered");
        var persisted = GitProgressDiagnostics.Write(evidence, d);
        using var stored = JsonDocument.Parse(persisted);
        Assert.Equal(2, stored.RootElement.GetArrayLength());
        Assert.Contains("__git-progress-diagnostics-v1", persisted);
        var visible = GitProgressDiagnostics.PublicEvidence(persisted);
        using var shown = JsonDocument.Parse(visible);
        var single = Assert.Single(shown.RootElement.EnumerateArray());
        Assert.Equal("feature-rule", single.GetProperty("source").GetString());
        Assert.DoesNotContain("__git-progress-diagnostics-v1", visible);
        var restored = Assert.IsType<ProgressReportDiagnostics>(GitProgressDiagnostics.Read(persisted));
        Assert.Equal(8, restored.SentFiles);
        Assert.Equal(2, restored.ModelItems);
        Assert.Equal(0, restored.Verified);
        Assert.Equal(2, restored.Rejections.TestSetupOnly);
        Assert.Equal(3, restored.FeatureGroups);
        Assert.Equal("filtered", restored.ExplanationStatus);
        Assert.Equal(0, restored.ExplanationAccepted);
    }

    [Fact]
    public void History_without_diagnostics_is_not_misrepresented_as_zero_candidates()
    {
        const string oldJson = "[{\"source\":\"feature-rule\",\"file\":\"backend/A.cs\",\"evidence\":\"+x\"}]";
        Assert.Null(GitProgressDiagnostics.Read(oldJson));
        Assert.Equal(oldJson, GitProgressDiagnostics.PublicEvidence(oldJson));
        Assert.Null(GitProgressDiagnostics.Read("[]"));
        Assert.Equal("[]", GitProgressDiagnostics.PublicEvidence("[]"));
        Assert.Null(GitProgressDiagnostics.Read("garbled"));
        Assert.Equal("garbled", GitProgressDiagnostics.PublicEvidence("garbled"));
    }

    [Fact]
    public async Task Empty_ollama_items_are_counted_without_inventing_verified_findings()
    {
        using var http = new HttpClient(new FakeOllama("{\"items\":[]}"))
        { BaseAddress = new Uri("http://127.0.0.1:11434/") };
        var model = new AiGitProgressClient(http, new ConfigurationBuilder().Build());
        var diff = new ProgressDiff(new string('a', 40), new string('b', 40), 1,
            [new ProgressFile("backend/StudentProjects.Api/Endpoints/Test.cs", "added", 1, 0,
                "+app.MapGet(\"/health\", Health);")], false);
        var result = await model.AnalyzeAsync(diff, CancellationToken.None);
        Assert.Equal(1, result.SentFiles);
        Assert.Equal(0, result.RawItemCount);
        Assert.Equal(0, result.CandidateCount);
        Assert.NotNull(result.Diagnostics);
        Assert.Equal(0, result.Diagnostics!.Eligible);
        Assert.Equal(0, result.RuleBasedFallback ? 0 : result.Items.Count);
    }

    [Fact]
    public async Task Invalid_evidence_id_counts_as_rejection_without_loosening_filter()
    {
        using var http = new HttpClient(new FakeOllama(
            "{\"items\":[{\"evidenceId\":\"L999\",\"description\":\"Bổ sung tuyến API mới cho phép frontend truy xuất thông tin repository.\"}]}"))
        { BaseAddress = new Uri("http://127.0.0.1:11434/") };
        var model = new AiGitProgressClient(http, new ConfigurationBuilder().Build());
        var diff = new ProgressDiff(new string('a', 40), new string('b', 40), 1,
            [new ProgressFile("backend/StudentProjects.Api/Endpoints/Test.cs", "added", 1, 0,
                "+app.MapGet(\"/health\", Health);")], false);
        var result = await model.AnalyzeAsync(diff, CancellationToken.None);
        Assert.Equal(1, result.RawItemCount);
        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.Diagnostics?.UnknownId);
        Assert.True(result.RuleBasedFallback); // fallback remains separate from verified Ollama items
    }

    [Fact]
    public async Task Feature_explanation_exposes_raw_and_parsed_counts_without_replacing_feature_fact()
    {
        using var http = new HttpClient(new FakeOllama(
            "{\"items\":[{\"factId\":\"F404\",\"explanation\":\"Giúp frontend gửi yêu cầu phân tích và tra cứu lịch sử để theo dõi dự án.\"}]}"))
        { BaseAddress = new Uri("http://127.0.0.1:11434/") };
        var model = new AiGitProgressClient(http, new ConfigurationBuilder().Build());
        var fact = new ProgressFeatureFact("API", "Thay đổi tuyến API",
            "Bổ sung tuyến POST /repositories/{id:guid}/progress để tiếp nhận yêu cầu.",
            [new ProgressFeatureCitation("backend/StudentProjects.Api/Endpoints/Test.cs",
                "+api.MapPost(\"/repositories/{id:guid}/progress\", Start);")]);
        var result = await model.ExplainFeaturesWithDiagnosticsAsync([fact], CancellationToken.None);
        Assert.Equal(1, result.RawItems);
        Assert.Equal(1, result.ParsedItems);
        Assert.Empty(result.Items);
        Assert.Single(GitProgressFeatureReport.Build([fact], result.Items));
    }

    private sealed class FakeOllama(string generated) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var json = JsonSerializer.Serialize(new { response = generated, done = true });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }
}
