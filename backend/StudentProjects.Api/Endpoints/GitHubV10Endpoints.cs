using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using StudentProjects.Api.Services;
using StudentProjects.Domain.Entities;
using StudentProjects.Domain.Enums;
using StudentProjects.Infrastructure.Persistence;

namespace StudentProjects.Api.Endpoints;

/// <summary>v0.10 GitHub public repository integration, scoped to existing Projects.</summary>
public static class GitHubV10Endpoints
{
    public static void MapGitHubV10Endpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1/v10").RequireAuthorization(p => p.RequireRole("Student", "Lecturer", "Admin"));
        api.MapGet("/projects/{projectId:guid}/repositories", List);
        api.MapPost("/projects/{projectId:guid}/repositories", Link);
        api.MapGet("/repositories/{id:guid}/commits", Commits);
        api.MapGet("/repositories/{id:guid}/branches", Branches);
        api.MapDelete("/repositories/{id:guid}", Unlink);
    }

    private static Guid Actor(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    private static bool CanRead(Project p, ClaimsPrincipal user) => user.IsInRole("Admin") ||
        (user.IsInRole("Student") && p.StudentUserId == Actor(user)) ||
        (user.IsInRole("Lecturer") && p.LecturerUserId == Actor(user));

    private static bool CanManage(Project p, ClaimsPrincipal user) =>
        user.IsInRole("Student") && p.StudentUserId == Actor(user);

    private static IResult GitHubError(GitHubAccessException e) =>
        Results.Json(new { code = e.Code, message = e.Message }, statusCode: e.HttpStatus);

    public sealed record LinkRepositoryInput(string? RepositoryUrl);

    private static async Task<IResult> List(Guid projectId, ClaimsPrincipal user, StudentProjectsDbContext db,
        CancellationToken ct)
    {
        var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null) return Results.NotFound();
        if (!CanRead(project, user)) return Results.Forbid();
        var rows = await db.Repositories.AsNoTracking().Where(r => r.ProjectId == projectId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new { r.Id, r.ProjectId, r.RepositoryUrl, r.DefaultBranch, r.LastCommitAt, r.CreatedAt })
            .ToListAsync(ct);
        return Results.Ok(rows);
    }

    private static async Task<IResult> Link(Guid projectId, LinkRepositoryInput? input, ClaimsPrincipal user,
        StudentProjectsDbContext db, IGitHubPublicClient github, CancellationToken ct)
    {
        var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null) return Results.NotFound();
        if (!CanManage(project, user)) return Results.Forbid();
        if (project.Status == ProjectStatus.CANCELLED)
            return Results.Conflict(new { code = "PROJECT_CANCELLED", message = "Không thể liên kết repository với Project đã hủy." });
        if (!GitHubUrl.TryParse(input?.RepositoryUrl, out var owner, out var repo))
            return Results.BadRequest(new { code = "INVALID_GITHUB_URL", message = "Nhập URL HTTPS của repository GitHub, dạng https://github.com/owner/repository." });
        GitHubRepositoryInfo metadata;
        try { metadata = await github.GetRepositoryAsync(owner, repo, ct); }
        catch (GitHubAccessException e) { return GitHubError(e); }
        // Verify public status via trusted GitHub metadata before recording a link.
        var already = await db.Repositories.AsNoTracking().AnyAsync(r => r.ProjectId == projectId &&
            r.RepositoryUrl.ToLower() == metadata.RepositoryUrl.ToLower(), ct);
        if (already) return Results.Conflict(new { code = "REPOSITORY_ALREADY_LINKED", message = "Repository này đã liên kết với Project." });
        var link = new RepositoryLink
        {
            ProjectId = projectId, RepositoryUrl = metadata.RepositoryUrl,
            DefaultBranch = metadata.DefaultBranch, LastCommitAt = metadata.PushedAt
        };
        db.Repositories.Add(link);
        db.AuditEntries.Add(new AuditEntry
        {
            ActorUserId = Actor(user), Action = "GITHUB_REPOSITORY_LINKED",
            EntityName = "RepositoryLink", EntityId = link.Id
        });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            return Results.Conflict(new { code = "REPOSITORY_ALREADY_LINKED", message = "Repository đã được liên kết hoặc Database chưa được nâng cấp." });
        }
        return Results.Created($"/api/v1/v10/projects/{projectId}/repositories", new
        {
            link.Id, link.ProjectId, link.RepositoryUrl, link.DefaultBranch, link.LastCommitAt, link.CreatedAt
        });
    }

    private static async Task<IResult> Commits(Guid id, ClaimsPrincipal user,
        StudentProjectsDbContext db, IGitHubPublicClient github, CancellationToken ct)
    {
        var link = await db.Repositories.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        if (link is null) return Results.NotFound();
        var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == link.ProjectId, ct);
        if (project is null) return Results.NotFound();
        if (!CanRead(project, user)) return Results.Forbid();
        if (!GitHubUrl.TryParse(link.RepositoryUrl, out var owner, out var repo))
            return Results.Conflict(new { code = "INVALID_SAVED_URL", message = "URL repository lưu trong hệ thống không hợp lệ." });
        try
        {
            var commits = await github.GetCommitsAsync(owner, repo, ct);
            return Results.Ok(new { link.Id, link.ProjectId, link.RepositoryUrl, Items = commits });
        }
        catch (GitHubAccessException e) { return GitHubError(e); }
    }

    private static async Task<IResult> Branches(Guid id, ClaimsPrincipal user,
        StudentProjectsDbContext db, IGitHubPublicClient github, CancellationToken ct)
    {
        var link = await db.Repositories.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        if (link is null) return Results.NotFound();
        var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == link.ProjectId, ct);
        if (project is null) return Results.NotFound();
        if (!CanRead(project, user)) return Results.Forbid();
        if (!GitHubUrl.TryParse(link.RepositoryUrl, out var owner, out var repo))
            return Results.Conflict(new { code = "INVALID_SAVED_URL", message = "URL repository đã lưu không hợp lệ." });
        try { return Results.Ok(new { link.Id, Items = await github.GetBranchesAsync(owner, repo, ct) }); }
        catch (GitHubAccessException e) { return GitHubError(e); }
    }

    private static async Task<IResult> Unlink(Guid id, ClaimsPrincipal user,
        StudentProjectsDbContext db, CancellationToken ct)
    {
        var link = await db.Repositories.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (link is null) return Results.NotFound();
        var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == link.ProjectId, ct);
        if (project is null) return Results.NotFound();
        if (!CanManage(project, user)) return Results.Forbid();
        db.Repositories.Remove(link);
        db.AuditEntries.Add(new AuditEntry
        {
            ActorUserId = Actor(user), Action = "GITHUB_REPOSITORY_UNLINKED",
            EntityName = "RepositoryLink", EntityId = link.Id
        });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            return Results.Conflict(new { code = "REPOSITORY_IN_USE", message = "Repository đang có dữ liệu phụ thuộc, không thể gỡ liên kết." });
        }
        return Results.NoContent();
    }
}
