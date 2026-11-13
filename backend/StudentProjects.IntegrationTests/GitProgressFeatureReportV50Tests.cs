using StudentProjects.Api.Services;
using Xunit;

namespace StudentProjects.IntegrationTests;

public sealed class GitProgressFeatureReportV50Tests
{
    private const string Backend = "backend/StudentProjects.Api/Endpoints/GitHubV10Endpoints.cs";
    private const string Api = "frontend/src/features/github/github.api.ts";
    private const string View = "frontend/src/views/GitHubV10View.vue";

    [Fact]
    public void Feature_report_keeps_each_handlers_business_evidence_in_its_own_operation()
    {
        var files = new[]
        {
            new ProgressFile(Backend, "added", 16, 0,
                "+api.MapGet(\"/projects/{projectId:guid}/repositories\", List);\n" +
                "+api.MapPost(\"/projects/{projectId:guid}/repositories\", Link);\n" +
                "+api.MapGet(\"/repositories/{id:guid}/commits\", Commits);\n" +
                "+api.MapDelete(\"/repositories/{id:guid}\", Unlink);\n" +
                "+private static async Task<IResult> List(Guid projectId)\n" +
                "+if (!CanRead(project, user)) return Results.Forbid();\n" +
                "+var rows = await db.Repositories.AsNoTracking().Where(r => r.ProjectId == projectId)\n" +
                "+private static async Task<IResult> Link(Guid projectId)\n" +
                "+if (!CanManage(project, user)) return Results.Forbid();\n" +
                "+if (!GitHubUrl.TryParse(input?.RepositoryUrl, out var owner, out var repo))\n" +
                "+db.Repositories.Add(link);\n" +
                "+try { await db.SaveChangesAsync(ct); }\n" +
                "+private static async Task<IResult> Commits(Guid id)\n" +
                "+var commits = await github.GetCommitsAsync(owner, repo, ct);\n" +
                "+private static async Task<IResult> Unlink(Guid id)\n" +
                "+db.Repositories.Remove(link);\n" +
                "+try { await db.SaveChangesAsync(ct); }"),
            new ProgressFile(Api, "added", 3, 0,
                "+list: (projectId: string) => request(`/v10/projects/${projectId}/repositories`),\n" +
                "+link: (projectId: string, repositoryUrl: string) =>\n" +
                "+unlink: (repositoryId: string) =>"),
            new ProgressFile(View, "added", 3, 0,
                "+await githubApi.link(projectId.value, repositoryUrl.value.trim())\n" +
                "+<form class=\"gh-add\" @submit.prevent=\"link\">\n" +
                "+await githubApi.unlink(link.id)")
        };
        var features = GitProgressFeatureReport.BuildGitHubRouteReport(files);
        Assert.Equal(4, features.Count);
        var list = Assert.Single(features.Where(x => x.Name == "Khai báo API xem danh sách repository"));
        var link = Assert.Single(features.Where(x => x.Name == "Khai báo API liên kết repository"));
        var commits = Assert.Single(features.Where(x => x.Name == "Khai báo API xem commit"));
        var unlink = Assert.Single(features.Where(x => x.Name == "Khai báo API gỡ liên kết repository"));

        Assert.Contains(list.Citations, c => c.Evidence.Contains("db.Repositories.AsNoTracking().Where", StringComparison.Ordinal));
        Assert.DoesNotContain(list.Citations, c => c.Evidence.Contains("SaveChangesAsync", StringComparison.Ordinal));
        Assert.Contains(link.Citations, c => c.Evidence.Contains("db.Repositories.Add(link)", StringComparison.Ordinal));
        Assert.Contains(link.Citations, c => c.Evidence.Contains("SaveChangesAsync", StringComparison.Ordinal));
        Assert.Contains(link.Citations, c => c.Evidence.Contains("await githubApi.link", StringComparison.Ordinal));
        Assert.Contains(link.Citations, c => c.Evidence.Contains("@submit.prevent", StringComparison.Ordinal));
        Assert.DoesNotContain(link.Citations, c => c.Evidence.Contains("db.Repositories.Remove(link)", StringComparison.Ordinal));
        Assert.Contains(commits.Citations, c => c.Evidence.Contains("github.GetCommitsAsync", StringComparison.Ordinal));
        Assert.DoesNotContain(commits.Citations, c => c.Evidence.Contains("SaveChangesAsync", StringComparison.Ordinal));
        Assert.Contains(unlink.Citations, c => c.Evidence.Contains("db.Repositories.Remove(link)", StringComparison.Ordinal));
        Assert.Contains(unlink.Citations, c => c.Evidence.Contains("SaveChangesAsync", StringComparison.Ordinal));
        Assert.Contains(unlink.Citations, c => c.Evidence.Contains("await githubApi.unlink", StringComparison.Ordinal));
        Assert.DoesNotContain(unlink.Citations, c => c.Evidence.Contains("db.Repositories.Add(link)", StringComparison.Ordinal));
    }

    [Fact]
    public void Milestone_deadline_report_links_correct_business_steps_and_frontend_without_borrowing_neighbor()
    {
        const string endpoint = "backend/StudentProjects.Api/Endpoints/ProjectProgressV07Endpoints.cs";
        var files = new[]
        {
            new ProgressFile(endpoint, "modified", 16, 0,
                "+api.MapPut(\"/milestones/{id:guid}/deadline\", UpdateStudentDeadline);\n" +
                "+private static async Task<IResult> UpdateStudentDeadline(Guid id, StudentDeadlineInput? req,\n" +
                "+    ClaimsPrincipal principal, StudentProjectsDbContext db, CancellationToken ct)\n" +
                "+    {\n" +
                "+if (!CanWrite(project, principal, \"Student\")) return Results.Forbid();\n" +
                "+if (deadline < item.StartAt) return Invalid(\"Ngày nộp không hợp lệ\");\n" +
                "+item.DeadlineAt = deadline;\n" +
                "+item.ProposalStatus = \"PENDING_REVIEW\";\n" +
                "+await db.SaveChangesAsync(ct);\n" +
                "+    }\n" +
                "+private static async Task<IResult> AnotherMethod(Guid id)\n" +
                "+    {\n" +
                "+db.Repositories.Remove(link);\n" +
                "+    }"),
            new ProgressFile("frontend/src/features/progress/projectProgress.api.ts", "modified", 1, 0,
                "+updateDeadline: (id: string, deadlineAt: string) =>"),
            new ProgressFile("frontend/src/views/ProjectProgressV07View.vue", "modified", 2, 0,
                "+const result = await projectProgressApi.updateDeadline(m.id, deadline.toISOString())\n" +
                "+<button @click=\"saveDeadline(selected)\">Lưu ngày</button>")
        };
        var milestone = Assert.Single(GitProgressFeatureReport.BuildRouteEvidenceReport(files));
        Assert.Contains(milestone.Citations, c => c.Evidence.Contains("item.DeadlineAt = deadline", StringComparison.Ordinal));
        Assert.Contains(milestone.Citations, c => c.Evidence.Contains("SaveChangesAsync", StringComparison.Ordinal));
        Assert.Contains(milestone.Citations, c => c.Evidence.Contains("await projectProgressApi.updateDeadline", StringComparison.Ordinal));
        Assert.Contains(milestone.Citations, c => c.Evidence.Contains("!CanWrite", StringComparison.Ordinal));
        Assert.DoesNotContain(milestone.Citations, c => c.Evidence.Contains("db.Repositories.Remove", StringComparison.Ordinal));
    }

    [Fact]
    public void Partial_handler_patch_cannot_borrow_unrelated_save_from_next_method()
    {
        const string endpoint = "backend/StudentProjects.Api/Endpoints/ProjectProgressV07Endpoints.cs";
        var files = new[]
        {
            new ProgressFile(endpoint, "modified", 4, 0,
                "+api.MapPut(\"/milestones/{id:guid}/deadline\", UpdateStudentDeadline);\n" +
                "+private static async Task<IResult> UpdateStudentDeadline(Guid id)\n" +
                "+    {\n" +
                "+item.DeadlineAt = deadline;\n" +
                "+    }\n" +
                "+private static async Task<IResult> ReviewProposal(Guid id)\n" +
                "+    {\n" +
                "+await db.SaveChangesAsync(ct);\n" +
                "+    }")
        };
        var milestone = Assert.Single(GitProgressFeatureReport.BuildRouteEvidenceReport(files));
        Assert.Contains(milestone.Citations, c => c.Evidence.Contains("item.DeadlineAt = deadline", StringComparison.Ordinal));
        Assert.DoesNotContain(milestone.Citations, c => c.Evidence.Contains("SaveChangesAsync", StringComparison.Ordinal));
    }

    [Fact]
    public void No_handler_body_means_no_claims_about_database_persistence()
    {
        var files = new[]
        {
            new ProgressFile(Backend, "added", 3, 0,
                "+api.MapPost(\"/projects/{projectId:guid}/repositories\", Link);\n" +
                "+private static async Task<IResult> Other(Guid id)\n" +
                "+await db.SaveChangesAsync(ct);")
        };
        var link = Assert.Single(GitProgressFeatureReport.BuildGitHubRouteReport(files));
        Assert.DoesNotContain(link.Steps, s => s.Detail.Contains("cơ sở dữ liệu", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(link.Citations, c => c.Evidence.Contains("SaveChangesAsync", StringComparison.Ordinal));
    }
}
