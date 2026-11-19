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
        Assert.Equal((0m, (decimal?)40m, 0), progress);
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

    private sealed class ModelReply(string answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { response = answer }))
            });
    }
}
