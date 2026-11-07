using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using StudentProjects.Api.Services;
using Xunit;

namespace StudentProjects.IntegrationTests;

public sealed class GitProgressQualityV392Tests
{
    private const string TestPath = "backend/StudentProjects.IntegrationTests/GitHubV10IntegrationTests.cs";
    private const string ProductionPath = "backend/StudentProjects.Api/Services/GitHubPublicClient.cs";
    private static ProgressFile File(string path, string patch) => new(path, "modified", 2, 0, patch);

    [Theory]
    [InlineData(TestPath, "+            await db.Database.EnsureCreatedAsync();", true)]
    [InlineData(TestPath, "+            await db.SaveChangesAsync();", true)]
    [InlineData(TestPath, "+            await db.Database.MigrateAsync(ct);", true)]
    [InlineData(TestPath, "+            Assert.Equal(HttpStatusCode.Created, response.StatusCode);", false)]
    [InlineData(TestPath, "+    public async Task Student_can_link_and_view_but_other_student_cannot_access()", false)]
    [InlineData(ProductionPath, "+            await db.SaveChangesAsync();", false)]
    [InlineData("frontend/src/views/GitHubV10View.vue", "+      <button @click=\"removeLink(r)\">Gỡ liên kết</button>", false)]
    public void Low_value_db_setup_is_test_only(string path, string line, bool excluded)
    {
        Assert.Equal(excluded, GitProgressMeaningPolicy.IsLowValueTestSetupLine(path, line));
    }

    [Fact]
    public void Test_setup_is_not_sent_to_Ollama_but_assertions_remain()
    {
        var file = File(TestPath, string.Join('\n',
            "+        await db.Database.EnsureCreatedAsync();",
            "+        await db.SaveChangesAsync();",
            "+    public async Task Student_can_link_and_view_but_other_student_cannot_access()",
            "+        Assert.Equal(HttpStatusCode.Created, response.StatusCode);"));
        var lines = GitProgressMeaningPolicy.MeaningfulLines(file);
        Assert.DoesNotContain(lines, x => x.Contains("EnsureCreatedAsync", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, x => x.Contains("SaveChangesAsync", StringComparison.Ordinal));
        Assert.Contains(lines, x => x.Contains("Student_can_link_and_view", StringComparison.Ordinal));
        Assert.Contains(lines, x => x.Contains("Assert.Equal", StringComparison.Ordinal));
    }

    [Fact]
    public void Old_style_ollama_candidates_are_screened_but_legitimate_user_actions_remain()
    {
        const string created = "+        await db.Database.EnsureCreatedAsync();";
        const string saved = "+        await db.SaveChangesAsync();";
        const string assertion = "+        Assert.Equal(HttpStatusCode.Created, response.StatusCode);";
        const string production = "+        await db.SaveChangesAsync();";
        const string ui = "+              <button v-if=\"isStudent\" type=\"button\" :disabled=\"busy\" @click=\"removeLink(r)\">Gỡ liên kết</button>";
        var testFile = File(TestPath, string.Join('\n', created, saved, assertion));
        var productionFile = File(ProductionPath, production);
        var uiFile = File("frontend/src/views/GitHubV10View.vue", ui);
        var references = new[] {
            new ProgressEvidenceReference("L1", testFile.Path, created),
            new ProgressEvidenceReference("L2", testFile.Path, saved),
            new ProgressEvidenceReference("L3", testFile.Path, assertion),
            new ProgressEvidenceReference("L4", productionFile.Path, production),
            new ProgressEvidenceReference("L5", uiFile.Path, ui)
        };
        var selections = new[] {
            new ProgressEvidenceSelection("L1", "The commit ensures the database is created before running tests."),
            new ProgressEvidenceSelection("L2", "The commit saves changes to the database after running tests."),
            new ProgressEvidenceSelection("L3", "Bổ sung Assert.Equal để kiểm tra phản hồi Created khi sinh viên liên kết repository."),
            new ProgressEvidenceSelection("L4", "Bổ sung SaveChangesAsync để lưu dữ liệu của handler production vào database."),
            new ProgressEvidenceSelection("L5", "Bổ sung nút Gỡ liên kết gọi removeLink(r) cho sinh viên trên giao diện.")
        };
        var files = new[] { testFile, productionFile, uiFile };
        var diagnostics = GitProgressMeaningPolicy.ExplainRejectionsById(selections, references, files);
        Assert.Equal(2, diagnostics.TestSetupOnly);
        Assert.Equal(3, diagnostics.Eligible);
        var screened = GitProgressMeaningPolicy.ScreenMeaningfulById(selections, references, files);
        Assert.Equal(3, screened.Count);
        Assert.Contains(screened, x => x.File == testFile.Path && x.Evidence == assertion);
        Assert.Contains(screened, x => x.File == productionFile.Path && x.Evidence == production);
        Assert.Contains(screened, x => x.File == uiFile.Path && x.Evidence == ui);
        Assert.DoesNotContain(screened, x => x.Evidence.Contains("EnsureCreatedAsync", StringComparison.Ordinal));
    }

    [Fact]
    public void Rule_fallback_does_not_resurrect_test_db_setup()
    {
        const string setup = "+        await db.SaveChangesAsync();";
        const string assertion = "+        Assert.Equal(HttpStatusCode.Created, response.StatusCode);";
        var f = File(TestPath, setup + "\n" + assertion);
        var refs = new[] {
            new ProgressEvidenceReference("L1", f.Path, setup),
            new ProgressEvidenceReference("L2", f.Path, assertion)
        };
        var fallback = GitProgressGroundedFallback.Extract(refs, [f]);
        Assert.Single(fallback);
        Assert.Equal(assertion, fallback[0].Evidence);
    }

    [Fact]
    public async Task GitHub_client_returns_at_most_20_commit_choices_with_full_SHA()
    {
        var commits = Enumerable.Range(0, 25).Select(i => new
        {
            sha = i.ToString("x40"),
            commit = new { message = $"Commit number {i}", author = new { name = "Student", date = "2026-11-07T07:00:00Z" } },
            html_url = $"https://github.com/example/demo/commit/{i:x40}"
        });
        var json = JsonSerializer.Serialize(commits);
        var handler = new CommitHandler(json);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var client = new GitHubPublicClient(http, cache, new ConfigurationBuilder().Build());
        var result = await client.GetCommitsAsync("example", "demo", CancellationToken.None);
        Assert.Equal(20, result.Count);
        Assert.All(result, x => Assert.Equal(40, x.Sha.Length));
        Assert.Equal("Commit number 0", result[0].Message);
        Assert.Equal("Commit number 19", result[19].Message);
        Assert.Equal("Student", result[0].Author);
        Assert.Contains("per_page=20", handler.RequestedPath);
    }

    private sealed class CommitHandler(string json) : HttpMessageHandler
    {
        public string RequestedPath { get; private set; } = "";
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestedPath = request.RequestUri?.PathAndQuery ?? "";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }
}
