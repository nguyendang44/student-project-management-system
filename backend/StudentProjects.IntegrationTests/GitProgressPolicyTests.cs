using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using StudentProjects.Api.Services;
using Xunit;

namespace StudentProjects.IntegrationTests;

public sealed class GitProgressPolicyTests
{
    private const string Old = "1111111111111111111111111111111111111111";
    private const string New = "2222222222222222222222222222222222222222";

    [Fact]
    public void Only_full_40_hex_commit_shas_are_accepted()
    {
        Assert.True(GitProgressEvidencePolicy.IsSha(Old));
        Assert.False(GitProgressEvidencePolicy.IsSha("main"));
        Assert.False(GitProgressEvidencePolicy.IsSha("abc1234"));
        Assert.False(GitProgressEvidencePolicy.IsSha(new string('z', 40)));
    }

    [Fact]
    public void Screens_out_ai_claims_without_exact_changed_line()
    {
        var files = new[] { new ProgressFile("src/Login.cs", "modified", 1, 0, "@@ -1 +1 @@\n+if (isValid) return Token();") };
        var ok = new ProgressEvidence("src/Login.cs", "+if (isValid) return Token();", "Thêm nhánh kiểm tra.");
        var invented = new ProgressEvidence("src/Login.cs", "+AllowsAnonymousAccess()", "Đã sửa bảo mật.");
        var other = new ProgressEvidence("elsewhere.cs", "+if (isValid) return Token();", "Không có file.");
        var kept = GitProgressEvidencePolicy.Screen([ok, invented, other], files);
        Assert.Single(kept);
        Assert.Equal(ok, kept[0]);
    }

    [Fact]
    public void Skips_secret_bearing_lines_and_sensitive_files()
    {
        var file = new ProgressFile("src/config.cs", "modified", 2, 0,
            "+password = \"hunter2-test-value\"\n+var n = 42;\n+const string api_key = \"my-real-secret-value\";");
        var lines = GitProgressEvidencePolicy.EvidenceLines(file);
        Assert.Single(lines);
        Assert.Equal("+var n = 42;", lines[0]);
        Assert.False(GitProgressEvidencePolicy.SafePath(".env"));
        Assert.False(GitProgressEvidencePolicy.SafePath("secrets/password.pem"));
    }

    [Fact]
    public void Summary_never_claims_work_is_complete()
    {
        var diff = new ProgressDiff(Old, New, 2, [new ProgressFile("src/a.cs", "modified", 7, 3, "+x = 5;")], false);
        var summary = GitProgressEvidencePolicy.ComposeSummary(diff, 1, 0);
        Assert.Contains("2 commit", summary);
        Assert.Contains("+7/-3", summary);
        Assert.Contains("không chứng minh", summary);
    }

    [Theory]
    [InlineData("diverged", 1)]
    [InlineData("behind", 0)]
    [InlineData("identical", 0)]
    public async Task Compare_rejects_non_descendant_commit_pairs(string status, int ahead)
    {
        var content = $$"""
            {"status":"{{status}}","ahead_by":{{ahead}},"files":[]}
            """;
        var client = new GitHubProgressClient(new HttpClient(new StubHandler(content))
            { BaseAddress = new Uri("https://api.github.com/") }, new ConfigurationBuilder().Build());
        await Assert.ThrowsAsync<AnalysisInputException>(() =>
            client.CompareAsync("someone", "repo", Old, New, CancellationToken.None));
    }

    [Fact]
    public async Task Compare_uses_github_file_changes_not_commit_messages()
    {
        const string body = """
            {"status":"ahead","ahead_by":2,"files":[
              {"filename":"src/a.cs","status":"modified","additions":4,"deletions":2,"patch":"@@ -1 +1 @@\n+var value = 42;"}
            ]}
            """;
        var client = new GitHubProgressClient(new HttpClient(new StubHandler(body))
            { BaseAddress = new Uri("https://api.github.com/") }, new ConfigurationBuilder().Build());
        var result = await client.CompareAsync("someone", "repo", Old, New, CancellationToken.None);
        Assert.Equal(2, result.AheadBy);
        Assert.Single(result.Files);
        Assert.Equal("src/a.cs", result.Files[0].Path);
        Assert.Equal(4, result.Files[0].Additions);
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
