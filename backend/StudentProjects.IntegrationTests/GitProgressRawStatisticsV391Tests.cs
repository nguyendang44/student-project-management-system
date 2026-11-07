using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using StudentProjects.Api.Services;
using Xunit;

namespace StudentProjects.IntegrationTests;

public sealed class GitProgressRawStatisticsV391Tests
{
    private const string BaseSha = "c04dfe28788d2270d344cb454883844ba738a1e3";
    private const string HeadSha = "dadc6d42b74d16a532def7e2d8deda8db5f206fa";

    // Exact paths and line totals from the user's v0.9 -> v0.10 git --numstat output.
    // HTTP response is simulated; this test does not call GitHub or Ollama.
    private static readonly (string Path, int Added, int Deleted)[] ChangedFiles =
    [
        ("backend/StudentProjects.Api/Endpoints/GitHubV10Endpoints.cs", 143, 0),
        ("backend/StudentProjects.Api/Endpoints/SkeletonEndpoints.cs", 1, 3),
        ("backend/StudentProjects.Api/Program.cs", 8, 0),
        ("backend/StudentProjects.Api/Services/GitHubPublicClient.cs", 162, 0),
        ("backend/StudentProjects.IntegrationTests/GitHubV10IntegrationTests.cs", 182, 0),
        ("frontend/src/app/modules.ts", 5, 2),
        ("frontend/src/app/router.ts", 2, 1),
        ("frontend/src/features/github/github.api.ts", 13, 2),
        ("frontend/src/features/github/github.types.ts", 22, 2),
        ("frontend/src/views/GitHubV10View.vue", 220, 0),
        ("scripts/github-v010.patch.json", 8, 0),
        ("scripts/install-github-v010.cjs", 102, 0),
        ("scripts/upgrade-github-v010.sql", 82, 0),
        ("scripts/v010-backups/2026-10-30T12-00-21-939Z/backend/StudentProjects.Api/Endpoints/SkeletonEndpoints.cs", 50, 0),
        ("scripts/v010-backups/2026-10-30T12-00-21-939Z/backend/StudentProjects.Api/Program.cs", 104, 0),
        ("scripts/v010-backups/2026-10-30T12-00-21-939Z/frontend/src/app/modules.ts", 657, 0),
        ("scripts/v010-backups/2026-10-30T12-00-21-939Z/frontend/src/app/router.ts", 41, 0),
        ("scripts/v010-backups/2026-10-30T12-00-21-939Z/frontend/src/features/github/github.api.ts", 5, 0),
        ("scripts/v010-backups/2026-10-30T12-00-21-939Z/frontend/src/features/github/github.types.ts", 2, 0),
    ];

    [Fact]
    public async Task Raw_totals_are_preserved_and_backup_files_are_excluded_from_analysis()
    {
        var body = JsonSerializer.Serialize(new
        {
            status = "ahead", ahead_by = 1,
            files = ChangedFiles.Select(f => new
            {
                filename = f.Path, status = "modified", additions = f.Added, deletions = f.Deleted,
                patch = "+        api.MapGet(\"/repositories/{id:guid}/commits\", Commits);"
            })
        });
        using var http = new HttpClient(new StubHandler(body))
        { BaseAddress = new Uri("https://api.github.com/") };
        var client = new GitHubProgressClient(http, new ConfigurationBuilder().Build());

        var diff = await client.CompareAsync("someone", "repo", BaseSha, HeadSha, CancellationToken.None);
        Assert.Equal(19, diff.RawFileCount);
        Assert.Equal(1809, diff.RawAdditions);
        Assert.Equal(10, diff.RawDeletions);
        Assert.Equal(13, diff.Files.Count);
        Assert.Equal(950, diff.Files.Sum(f => f.Additions));
        Assert.Equal(6, diff.RawFileCount - diff.Files.Count);
        Assert.Equal(10, diff.Files.Count(f => GitProgressEvidencePolicy.IsAnalysisSourcePath(f.Path)));
        Assert.False(diff.PartialCoverage); // filtering backups is not GitHub truncation
        Assert.All(diff.Files, f => Assert.True(GitProgressEvidencePolicy.SafePath(f.Path)));
        Assert.DoesNotContain(diff.Files, f => f.Path.Contains("v010-backups", StringComparison.OrdinalIgnoreCase));

        var summary = GitProgressEvidencePolicy.ComposeSummary(diff, 8, 3);
        Assert.Contains("19 file thay đổi", summary);
        Assert.Contains("+1809/-10", summary);
        Assert.Contains("13 file", summary);
        Assert.Contains("loại 6 file", summary);
        Assert.Contains("mã nguồn ứng dụng/kiểm thử: 10 file", summary);
        Assert.Contains("file khác: 3", summary);
        Assert.Contains("8 file cho AI", summary);
        Assert.DoesNotContain("có thể chưa đầy đủ", summary);
    }

    [Fact]
    public void Legacy_diff_callers_without_raw_metadata_still_work()
    {
        var diff = new ProgressDiff(BaseSha, HeadSha, 1,
            [new ProgressFile("backend/StudentProjects.Api/Program.cs", "modified", 7, 2, "+app.MapGet(\"/test\", Test);")],
            false);
        Assert.Equal(1, diff.RawFileCount);
        Assert.Equal(7, diff.RawAdditions);
        Assert.Equal(2, diff.RawDeletions);
        Assert.Contains("+7/-2", GitProgressEvidencePolicy.ComposeSummary(diff, 1, 0));
    }

    private sealed class StubHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.StartsWith("/repos/someone/repo/compare/", request.RequestUri?.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
