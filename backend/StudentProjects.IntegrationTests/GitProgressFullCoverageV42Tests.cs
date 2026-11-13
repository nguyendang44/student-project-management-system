using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using StudentProjects.Api.Services;
using Xunit;

namespace StudentProjects.IntegrationTests;

public sealed class GitProgressFullCoverageV42Tests
{
    private const string Line = "+        api.MapGet(\"/projects/{projectId:guid}/repositories\", List);";

    [Fact]
    public void Batcher_sends_every_safe_text_file_including_scripts_without_four_file_sampling()
    {
        var files = Enumerable.Range(0, 10)
            .Select(i => new ProgressFile($"backend/StudentProjects.Api/Endpoints/Test{i}.cs", "added", 1, 0,
                $"@@ -0,0 +1,1 @@\n{Line}"))
            .Concat([
                new("scripts/upgrade-v04.sql", "added", 1, 0, "+CREATE TABLE student_data (id int);"),
                new("scripts/install-v04.cjs", "added", 1, 0, "+console.log('install');"),
                new("scripts/v04.patch.json", "added", 1, 0, "+{\"version\":\"v0.4\"}")
            ]).ToArray();
        var plan = GitProgressDiffBatcher.Build(files);
        Assert.Equal(13, plan.EligibleFiles);
        Assert.Equal(13, plan.Batches.Select(b => b.File.Path).Distinct().Count());
        Assert.Equal(0, plan.MissingPatchFiles);
        Assert.Equal(0, plan.OmittedLines);
        Assert.Equal(0, plan.TruncatedBatches);
        Assert.Contains(plan.Batches, b => b.File.Path.EndsWith(".sql"));
        Assert.Contains(plan.Batches, b => b.File.Path.EndsWith(".cjs"));
        Assert.Contains(plan.Batches, b => b.File.Path.EndsWith(".json"));
    }

    [Fact]
    public void Long_patch_is_chunked_without_losing_final_changes()
    {
        var patch = "@@ -0,0 +1,180 @@\n" + string.Join('\n',
            Enumerable.Range(1, 180).Select(i => $"+        await service.SaveAsync(value{i});"));
        var file = new ProgressFile("backend/StudentProjects.Api/Services/S.cs", "added", 180, 0, patch);
        var plan = GitProgressDiffBatcher.Build([file]);
        Assert.True(plan.Batches.Count > 1);
        Assert.Equal(180, plan.Batches.SelectMany(b => b.Lines)
            .Count(l => l.Text.Contains("SaveAsync(")));
        Assert.Contains(plan.Batches[^1].Lines, l => l.Text.Contains("value180"));
        Assert.Equal(0, plan.TruncatedBatches);
    }

    [Fact]
    public void Unsafe_lines_and_absent_patch_are_reported_not_marked_complete()
    {
        var plan = GitProgressDiffBatcher.Build([
            new("backend/StudentProjects.Api/Program.cs", "modified", 1, 0,
                "+api_key = abcdefghijklmnop\n+        app.MapGet(\"/hello\", Handler);"),
            new("frontend/src/views/Test.vue", "modified", 2, 1, null)
        ]);
        Assert.Equal(2, plan.EligibleFiles);
        Assert.Equal(1, plan.MissingPatchFiles);
        Assert.Equal(1, plan.OmittedLines);
        Assert.DoesNotContain("abcdefghijklmnop", plan.Batches[0].PromptData);
    }

    [Fact]
    public async Task Ollama_empty_items_still_reports_full_file_coverage()
    {
        var files = Enumerable.Range(1, 13).Select(i =>
            new ProgressFile($"backend/StudentProjects.Api/Endpoints/T{i}.cs", "added", 1, 0,
                $"@@ -0,0 +1,1 @@\n{Line}")).ToArray();
        var client = new AiGitProgressClient(new HttpClient(new EmptyOllamaHandler())
        { BaseAddress = new Uri("http://127.0.0.1:11434/") },
            new ConfigurationBuilder().Build());
        var result = await client.AnalyzeAsync(new ProgressDiff(new('a', 40), new('b', 40), 1,
            files, false), CancellationToken.None);
        Assert.Equal(13, result.EligibleFiles);
        Assert.Equal(13, result.FullyAnalyzedFiles);
        Assert.Equal(13, result.SentFiles);
        Assert.Equal(result.PlannedBatches, result.CompletedBatches);
        Assert.Equal("complete", result.CoverageStatus);
        Assert.Equal(0, result.RawItemCount);
        Assert.Equal(0, result.CandidateCount);
    }

    private sealed class EmptyOllamaHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var json = JsonSerializer.Serialize(new { response = "{\"items\":[]}", done_reason = "stop" });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }
}
