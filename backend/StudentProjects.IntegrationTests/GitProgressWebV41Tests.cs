using System.Text.Json;
using StudentProjects.Api.Services;
using Xunit;

namespace StudentProjects.IntegrationTests;

public sealed class GitProgressWebV41Tests
{
    [Fact]
    public async Task Transport_failure_is_not_reported_as_successful_empty_AI()
    {
        var attempt = await GitProgressAiAvailability.RunAsync(
            _ => Task.FromException<ProgressAiResult>(new HttpRequestException()), CancellationToken.None);
        Assert.Equal("connection-error", attempt.Status);
        Assert.Empty(attempt.Result.Items);
    }

    [Fact]
    public async Task Malformed_JSON_keeps_Git_facts_available_without_raw_response()
    {
        var attempt = await GitProgressAiAvailability.RunAsync(
            _ => Task.FromException<ProgressAiResult>(new JsonException("private response")), CancellationToken.None);
        Assert.Equal("invalid-response", attempt.Status);
        Assert.DoesNotContain("private response", JsonSerializer.Serialize(attempt));
    }

    [Fact]
    public async Task Timeout_is_recoverable_but_shutdown_cancellation_propagates()
    {
        var attempt = await GitProgressAiAvailability.RunAsync(
            _ => Task.FromException<ProgressAiResult>(new TaskCanceledException()), CancellationToken.None);
        Assert.Equal("timeout", attempt.Status);
        using var shutdown = new CancellationTokenSource();
        shutdown.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => GitProgressAiAvailability.RunAsync(
            token => Task.FromCanceled<ProgressAiResult>(token), shutdown.Token));
    }

    [Fact]
    public void Context_includes_later_changed_region_and_both_commits()
    {
        var before = new string('a', 40);
        var after = new string('b', 40);
        const string path = "src/Service.cs";
        var code = string.Join('\n', Enumerable.Range(1, 300).Select(i => $"line {i}: source code"));
        var diff = new ProgressDiff(before, after, 1,
            [new(path, "modified", 2, 2, "@@ -10,1 +10,1 @@\n-old\n+new\n@@ -210,1 +210,1 @@\n-old\n+new")], false);
        var context = RepositoryContextBuilder.Build(diff,
            new(before, [new(path, code)], 0), new(after, [new(path, code)], 0));
        Assert.Contains(context.Excerpts, x => x.Version == "before" && x.Code.Contains("line 210:"));
        Assert.Contains(context.Excerpts, x => x.Version == "after" && x.Code.Contains("line 210:"));
        Assert.True(context.Excerpts.Sum(x => x.Code.Length + 1) <= RepositoryContextBuilder.CharacterBudget);
        Assert.All(context.Excerpts, x => Assert.Equal(x.EndLine - x.StartLine + 1, x.Code.Split('\n').Length));
    }

    [Fact]
    public void Unavailable_AI_summary_never_claims_a_successfully_sent_sample()
    {
        var diff = new ProgressDiff(new('a', 40), new('b', 40), 1, [], false);
        var summary = GitProgressEvidencePolicy.ComposeSummary(diff, 0, 0, false);
        Assert.DoesNotContain("Đã gửi", summary);
        Assert.Contains("chưa hoàn tất", summary);
    }

    [Fact]
    public void Diagnostics_round_trip_keeps_failure_distinct_from_empty_model_output()
    {
        var diagnostics = new ProgressReportDiagnostics(0, 0, 0, 0, 0,
            new(0, 0, 0, 0, 0, 0, 0), 1, 0, 0, 0, "not-requested", AnalysisStatus: "timeout");
        var persisted = GitProgressDiagnostics.Write([], diagnostics);
        Assert.Equal("timeout", GitProgressDiagnostics.Read(persisted)!.AnalysisStatus);
        Assert.Equal("[]", GitProgressDiagnostics.PublicEvidence(persisted));
    }
}
