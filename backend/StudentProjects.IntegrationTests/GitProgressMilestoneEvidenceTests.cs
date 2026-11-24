using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using StudentProjects.Api.Services;
using StudentProjects.Domain.Entities;
using Xunit;

namespace StudentProjects.IntegrationTests;

public sealed class GitProgressMilestoneEvidenceTests
{
    private const string Backend = "backend/StudentProjects.Api/Endpoints/GitHubV10Endpoints.cs";
    private const string Frontend = "frontend/src/views/GitHubV10View.vue";
    private static readonly WordReportLine[] Criteria =
    [
        new(1, "Đăng nhập và phân quyền. Người dùng đăng nhập được. Giao diện và API kiểm tra quyền của sinh viên, giảng viên và quản trị viên."),
        new(2, "Xem commit và nhánh GitHub. Giao diện hiển thị danh sách commit và nhánh do backend trả về.")
    ];
    private static readonly WordEvidence[] Report =
    [
        new(1, "Gắn repo", Backend, "+        api.MapPost(\"/projects/{projectId:guid}/repositories\", Link);"),
        new(2, "Gắn repo", Backend, "+        api.MapGet(\"/projects/{projectId:guid}/repositories\", List);"),
        new(3, "Gắn repo", "frontend/src/features/github/github.api.ts", "+  link: (projectId: string, repositoryUrl: string) =>"),
        new(4, "Gắn repo", Frontend, "+    await githubApi.link(projectId.value, repositoryUrl.value.trim())"),
        new(5, "Xem commit", Backend, "+        api.MapGet(\"/repositories/{id:guid}/commits\", Commits);"),
        new(6, "Xem nhánh", Backend, "+        api.MapGet(\"/repositories/{id:guid}/branches\", Branches);"),
        new(7, "Xem lịch sử", Frontend, "+    const [recent, foundBranches] = await Promise.all([githubApi.commits(link.id), githubApi.branches(link.id)])")
    ];

    [Fact]
    public async Task Milestone_rejects_repository_citations_for_login_and_only_counts_supported_weight()
    {
        using var http = new HttpClient(new ModelReply("""
            {"items":[{"id":1,"status":"full","evidenceIds":[1,2,3,4]},
                      {"id":2,"status":"full","evidenceIds":[5,6,7]}]}
            """)) { BaseAddress = new Uri("http://localhost:11434/") };
        var comparison = await new AiGitProgressClient(http, new ConfigurationBuilder().Build())
            .CompareMilestoneAsync(Criteria, Report, CancellationToken.None);
        Assert.Equal("missing", comparison.Items[0].Status);
        Assert.Equal(0, comparison.Items[0].Evidence.Count);
        Assert.Equal("full", comparison.Items[1].Status);

        var criteria = new[] { new MilestoneCriterion { Points = 15 }, new MilestoneCriterion { Points = 10 } };
        var proposals = comparison.Items.Select((item, i) => new MilestoneCriterionAssessment
        {
            CriterionId = criteria[i].Id, Source = "AI", Outcome = item.Status
        });
        var progress = MilestoneCriterionProgress.Calculate(criteria, proposals);
        Assert.Equal((40m, (decimal?)40m, 0), progress);
    }

    [Fact]
    public async Task Milestone_requires_evidence_for_both_commits_and_branches()
    {
        using var http = new HttpClient(new ModelReply("""
            {"items":[{"id":2,"status":"full","evidenceIds":[5,7]}]}
            """)) { BaseAddress = new Uri("http://localhost:11434/") };
        var comparison = await new AiGitProgressClient(http, new ConfigurationBuilder().Build())
            .CompareMilestoneAsync([Criteria[1]], Report, CancellationToken.None);
        Assert.Equal("partial", comparison.Items[0].Status);
    }

    [Theory]
    [InlineData("Xây dựng cấu trúc dự án Backend. Cấu trúc dự án được tạo theo chuẩn ASP.NET Core Minimal API và Entity Framework Core")]
    [InlineData("Xây dựng cấu trúc dự án Frontend. Cấu trúc dự án được tạo theo chuẩn Vue 3, TypeScript và Vite")]
    [InlineData("Xây dựng chức năng quản lý đề tài. Quản lý đề tài, đợt đăng ký, đề xuất của sinh viên và yêu cầu hướng dẫn")]
    public async Task Repository_operations_do_not_establish_unrelated_work(string claim)
    {
        using var http = new HttpClient(new ModelReply("""{"items":[{"id":1,"status":"full","evidenceIds":[1,2,3,4]}]}"""))
            { BaseAddress = new Uri("http://localhost:11434/") };
        var result = await new AiGitProgressClient(http, new ConfigurationBuilder().Build())
            .CompareMilestoneAsync([new(1, claim)], Report, CancellationToken.None);
        Assert.Equal("missing", result.Items[0].Status);
    }

    [Fact]
    public async Task Commit_and_list_operations_do_not_prove_repository_linking()
    {
        using var http = new HttpClient(new ModelReply("""{"items":[{"id":1,"status":"full","evidenceIds":[2,5,7]}]}"""))
            { BaseAddress = new Uri("http://localhost:11434/") };
        var result = await new AiGitProgressClient(http, new ConfigurationBuilder().Build())
            .CompareMilestoneAsync([new(1, "Xây dựng chức năng liên kết repository GitHub")], Report, CancellationToken.None);
        Assert.Equal("missing", result.Items[0].Status);
    }

    [Fact]
    public async Task Linking_needs_mutation_persistence_and_the_frontend_call()
    {
        WordEvidence[] evidence = [Report[0], Report[3], new(8, "Gắn repo", Backend, "+ db.Repositories.Add(link);")];
        using var http = new HttpClient(new ModelReply("""{"items":[{"id":1,"status":"full","evidenceIds":[1,4,8]}]}"""))
            { BaseAddress = new Uri("http://localhost:11434/") };
        var result = await new AiGitProgressClient(http, new ConfigurationBuilder().Build())
            .CompareMilestoneAsync([new(1, "Xây dựng chức năng liên kết repository GitHub")], evidence, CancellationToken.None);
        Assert.Equal("full", result.Items[0].Status);
    }

    [Fact]
    public void Complete_milestone_reader_keeps_citations_beyond_the_word_sample_limit()
    {
        var citations = Enumerable.Range(1, 18).Select(i => new { file = Backend,
            evidence = $"+ api.MapGet(\"/repositories/{i}/commits\", Commits);" }).ToArray();
        var json = JsonSerializer.Serialize(new[] { new { title = "Xem commit", description = "Lấy commit từ GitHub",
            steps = new[] { new { detail = "Đọc commit" } }, citations } });
        var report = new GitProgressRun { EvidenceJson = json, BaseSha = "base", HeadSha = "head" };
        var rows = GitProgressWordComparison.ReadEvidence(json, complete: true, source: report);
        Assert.Equal(18, rows.Count);
        Assert.Equal(report.Id, rows[17].GitProgressRunId);
        Assert.Contains("Đọc commit", rows[0].ImplementationSteps!);
        Assert.Equal(10, GitProgressWordComparison.ReadEvidence(json).Count);
    }

    [Fact]
    public async Task Evidence_from_different_reports_can_jointly_satisfy_a_criterion()
    {
        var handler = new CorpusReply();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434/") };
        WordEvidence[] evidence =
        [
            Report[4] with { GitProgressRunId = Guid.NewGuid(), FeatureDescription = new string('a', 12000) },
            Report[5] with { GitProgressRunId = Guid.NewGuid(), FeatureDescription = new string('b', 12000) }
        ];
        var result = await new AiGitProgressClient(http, new ConfigurationBuilder().Build())
            .CompareMilestoneAsync([new(1, "Xem commit và nhánh GitHub")], evidence, CancellationToken.None);
        Assert.Equal("full", result.Items[0].Status);
        Assert.Equal(2, result.Items[0].Evidence.Count);
        Assert.Equal(3, handler.Calls); // Two full-reading batches, then the joint acceptance check.
    }

    private sealed class CorpusReply : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var prompt = payload.RootElement.GetProperty("prompt").GetString()!;
            var start = prompt.LastIndexOf("\n{\"claims\":", StringComparison.Ordinal);
            using var data = JsonDocument.Parse(prompt[(start + 1)..]);
            var ids = data.RootElement.GetProperty("evidence").EnumerateArray().Select(e => e.GetProperty("Id").GetInt32()).ToArray();
            var items = data.RootElement.GetProperty("claims").EnumerateArray().Select(c => new
            { id = c.GetProperty("Id").GetInt32(), status = "full", evidenceIds = ids });
            return new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(JsonSerializer.Serialize(new { response = JsonSerializer.Serialize(new { items }) })) };
        }
    }

    private sealed class ModelReply(string answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { response = answer }))
            });
    }
}
