using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using StudentProjects.Api.Services;
using Xunit;

namespace StudentProjects.IntegrationTests;

public sealed class GitProgressCodeContextTests
{
    private static readonly string Before = new('a', 40);
    private static readonly string After = new('b', 40);
    private static ProgressDiff Diff(string patch = "@@ -1,1 +1,1 @@\n+api.MapPost(\"/projects\", Create);") =>
        new(Before, After, 1, [new("backend/Test.Api/Endpoints/ProjectEndpoints.cs", "modified", 1, 1, patch)], false);

    [Fact]
    public void Prioritizes_changed_files_then_related_files_without_project_specific_features()
    {
        var paths = RepositoryContextBuilder.SelectPaths(
            ["src/AuthService.cs", "src/InvoiceService.cs", "src/InvoiceController.cs", "src/Other.cs"],
            ["src/InvoiceController.cs"]);
        Assert.Equal(new[] { "src/InvoiceController.cs", "src/InvoiceService.cs" }, paths);
    }

    [Fact]
    public void Pins_each_excerpt_to_its_actual_commit_and_preserves_line_ranges()
    {
        var result = RepositoryContextBuilder.Build(Diff(),
            new(Before, [new("backend/Test.Api/Endpoints/ProjectEndpoints.cs", "old\ncode")], 0),
            new(After, [new("backend/Test.Api/Endpoints/ProjectEndpoints.cs", "new\ncode\nhere")], 0));
        Assert.Equal("sampled", result.Status);
        Assert.Collection(result.Excerpts,
            x => { Assert.Equal(Before, x.CommitSha); Assert.Equal("before", x.Version); Assert.Equal(2, x.EndLine); },
            x => { Assert.Equal(After, x.CommitSha); Assert.Equal("after", x.Version); Assert.Equal(3, x.EndLine); });
        Assert.All(result.Excerpts, x => Assert.Equal(x.EndLine - x.StartLine + 1, x.Code.Split('\n').Length));
    }

    [Fact]
    public void Reads_near_changed_lines_in_large_files_instead_of_only_the_header()
    {
        var lines = Enumerable.Range(1, 300).Select(n => $"line {n}: source code").ToArray();
        var result = RepositoryContextBuilder.Build(Diff("@@ -200,1 +210,1 @@\n+new code"),
            new(Before, [new("backend/Test.Api/Endpoints/ProjectEndpoints.cs", string.Join('\n', lines))], 0),
            new(After, [new("backend/Test.Api/Endpoints/ProjectEndpoints.cs", string.Join('\n', lines))], 0));
        Assert.Contains("line 200:", result.Excerpts[0].Code);
        Assert.Contains("line 210:", result.Excerpts[1].Code);
        Assert.True(result.Excerpts.Sum(x => x.Code.Length) <= RepositoryContextBuilder.CharacterBudget);
    }

    [Fact]
    public void Missing_snapshot_is_explicit_and_never_treated_as_empty_old_code()
    {
        var result = RepositoryContextBuilder.Build(Diff(), null,
            new(After, [new("backend/Test.Api/Endpoints/ProjectEndpoints.cs", "new code")], 0));
        Assert.Equal("partial", result.Status);
        Assert.Equal("after", Assert.Single(result.Excerpts).Version);
        Assert.Equal("unavailable", RepositoryContextBuilder.Build(Diff(), null, null).Status);
    }

    [Fact]
    public async Task Sends_context_to_ollama_but_rejects_context_only_evidence_ids()
    {
        var handler = new CaptureHandler();
        var ai = new AiGitProgressClient(new HttpClient(handler) { BaseAddress = new("http://localhost/") },
            new ConfigurationBuilder().Build());
        var context = RepositoryContextBuilder.Build(Diff(),
            new(Before, [new("backend/Test.Api/Endpoints/ProjectEndpoints.cs", "existing code")], 0),
            new(After, [new("backend/Test.Api/Endpoints/ProjectEndpoints.cs", "updated code")], 0));
        var result = await ai.AnalyzeAsync(Diff(), CancellationToken.None, context);
        using var body = JsonDocument.Parse(handler.Body!);
        var prompt = body.RootElement.GetProperty("prompt").GetString()!;
        Assert.Contains(Before, prompt);
        Assert.Contains(After, prompt);
        Assert.Contains("existing code", prompt);
        Assert.Equal("qwen2.5-coder:7b", body.RootElement.GetProperty("model").GetString());
        Assert.Equal(8192, body.RootElement.GetProperty("options").GetProperty("num_ctx").GetInt32());
        Assert.Equal(1, result.Diagnostics!.UnknownId);
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public string? Body { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Body = await request.Content!.ReadAsStringAsync(ct);
            return new(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    response = "{\"items\":[{\"evidenceId\":\"CONTEXT1\",\"description\":\"Bổ sung chức năng tạo đề tài từ ngữ cảnh code tham khảo.\"}]}"
                }), Encoding.UTF8, "application/json")
            };
        }
    }
}
