using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using StudentProjects.Api.Services;
using StudentProjects.Domain.Entities;
using StudentProjects.Infrastructure.Persistence;

namespace StudentProjects.Api.Endpoints;

public static class CodeAnalysisV11Endpoints
{
    public static void MapCodeAnalysisV11Endpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1/v11").RequireAuthorization(p => p.RequireRole("Student", "Lecturer", "Admin"));
        api.MapPost("/repositories/{id:guid}/analyses", Start).RequireRateLimiting("analysis-start");
        api.MapGet("/repositories/{id:guid}/analyses", List);
        api.MapGet("/repositories/{id:guid}/analyses/compare", Compare);
        api.MapGet("/analyses/{id:guid}", Get);
        api.MapPost("/analyses/{id:guid}/reviews", Review);
    }

    public sealed record AnalyzeInput(string? Branch);
    public sealed record ReviewInput(string? Comment);
    private static Guid Actor(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
    private static bool CanRead(Project p, ClaimsPrincipal user) => user.IsInRole("Admin") ||
        (user.IsInRole("Student") && p.StudentUserId == Actor(user)) ||
        (user.IsInRole("Lecturer") && p.LecturerUserId == Actor(user));
    private static bool CanRequest(Project p, ClaimsPrincipal user) =>
        (user.IsInRole("Student") && p.StudentUserId == Actor(user)) ||
        (user.IsInRole("Lecturer") && p.LecturerUserId == Actor(user));

    private static async Task<(RepositoryLink? Link, Project? Project)> GetAllowedLink(
        Guid id, ClaimsPrincipal user, StudentProjectsDbContext db, CancellationToken ct)
    {
        var link = await db.Repositories.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        if (link is null) return (null, null);
        var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == link.ProjectId, ct);
        return (link, project);
    }

    private static async Task<IResult> Start(Guid id, AnalyzeInput? input, ClaimsPrincipal user,
        StudentProjectsDbContext db, GitHubSnapshotClient snapshots,
        AiCodeReviewClient ai, CancellationToken ct)
    {
        var (link, project) = await GetAllowedLink(id, user, db, ct);
        if (link is null || project is null) return Results.NotFound();
        if (!CanRequest(project, user)) return Results.Forbid();
        if (!GitHubUrl.TryParse(link.RepositoryUrl, out var owner, out var repo))
            return Results.Conflict(new { message = "Repository URL không hợp lệ." });
        var branch = (string.IsNullOrWhiteSpace(input?.Branch)
    ? link.DefaultBranch
    : input.Branch)?.Trim() ?? string.Empty;

        if (!GitHubSnapshotClient.ValidBranch(branch))
            return Results.BadRequest(new { message = "Tên branch không hợp lệ." });
        string sha;
        try
        {
            await snapshots.EnsurePublicAsync(owner, repo, ct);
            sha = await snapshots.ResolveCommitAsync(owner, repo, branch, ct);
        }
        catch (GitHubAccessException ex) { return Results.Json(new { code = ex.Code, message = ex.Message }, statusCode: ex.HttpStatus); }
        catch (AnalysisInputException ex) { return Results.UnprocessableEntity(new { message = ex.Message }); }

        var existing = await db.CodeAnalysisRuns.FirstOrDefaultAsync(r => r.RepositoryLinkId == id &&
            r.CommitSha == sha && r.Scope == ai.Scope && r.ModelName == ai.Model, ct);
        if (existing is not null)
        {
            if (existing.Status == "FAILED")
            {
                existing.Status = "PENDING";
                existing.ErrorMessage = null;
                existing.StartedAt = null;
                existing.CompletedAt = null;
                await db.SaveChangesAsync(ct);
            }
            return Results.Ok(new { existing.Id, existing.Status, existing.CommitSha, cached = existing.Status == "COMPLETED" });
        }
        var run = new CodeAnalysisRun
        {
            ProjectId = project.Id, RepositoryLinkId = id, RequestedByUserId = Actor(user),
            Branch = branch, CommitSha = sha, Scope = ai.Scope, ModelName = ai.Model
        };
        db.CodeAnalysisRuns.Add(run);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            // Concurrent requests for the same SHA are deduplicated by the unique DB index.
            var previouslyCreated = await db.CodeAnalysisRuns.AsNoTracking().FirstOrDefaultAsync(
                r => r.RepositoryLinkId == id && r.CommitSha == sha && r.Scope == ai.Scope && r.ModelName == ai.Model, ct);
            if (previouslyCreated is not null)
                return Results.Ok(new { previouslyCreated.Id, previouslyCreated.Status, previouslyCreated.CommitSha, cached = true });
            throw;
        }
        return Results.Accepted($"/api/v1/v11/analyses/{run.Id}", new { run.Id, run.Status, run.CommitSha, cached = false });
    }

    private static async Task<IResult> List(Guid id, ClaimsPrincipal user, StudentProjectsDbContext db, CancellationToken ct)
    {
        var (link, project) = await GetAllowedLink(id, user, db, ct);
        if (link is null || project is null) return Results.NotFound();
        if (!CanRead(project, user)) return Results.Forbid();
        var results = await db.CodeAnalysisRuns.AsNoTracking().Where(r => r.RepositoryLinkId == id)
            .OrderByDescending(r => r.CreatedAt).Take(40)
            .Select(r => new { r.Id, r.Branch, r.CommitSha, r.Scope, r.Status, r.ModelName, r.CreatedAt,
                r.CompletedAt, findingsCount = db.CodeAnalysisFindings.Count(f => f.RunId == r.Id) })
            .ToListAsync(ct);
        return Results.Ok(results);
    }

    private static async Task<IResult> Get(Guid id, ClaimsPrincipal user, StudentProjectsDbContext db, CancellationToken ct)
    {
        var run = await db.CodeAnalysisRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        if (run is null) return Results.NotFound();
        var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == run.ProjectId, ct);
        if (project is null) return Results.NotFound();
        if (!CanRead(project, user)) return Results.Forbid();
        // All AI-only findings are unverified suggestions, including old reports.
        // This derived field doesn't change the persisted schema.
        var findings = (await db.CodeAnalysisFindings.AsNoTracking().Where(f => f.RunId == id)
            .OrderBy(f => f.Severity).ThenBy(f => f.FilePath).ThenBy(f => f.LineNumber).ToListAsync(ct))
            .Select(f => new { f.Id, f.RunId, f.Category, f.Severity, f.FilePath, f.LineNumber,
                f.Evidence, f.Explanation, f.Recommendation, f.Fingerprint,
                verificationStatus = "NEEDS_REVIEW" }).ToArray();
        var reviews = await db.CodeAnalysisReviews.AsNoTracking().Where(r => r.RunId == id)
            .OrderByDescending(r => r.CreatedAt).ToListAsync(ct);
        var link = await db.Repositories.AsNoTracking().FirstOrDefaultAsync(l => l.Id == run.RepositoryLinkId, ct);
        return Results.Ok(new { run.Id, run.ProjectId, run.RepositoryLinkId, run.Branch, run.CommitSha, run.Scope,
            run.Status, run.ModelName, run.Summary, run.DevelopmentWork, run.AnalyzedPathsJson, run.SkippedFiles,
            run.ErrorMessage, run.CreatedAt, run.StartedAt, run.CompletedAt,
            RepositoryUrl = link?.RepositoryUrl, findings, reviews });
    }

    private static async Task<IResult> Compare(Guid id, Guid older, Guid newer,
        ClaimsPrincipal user, StudentProjectsDbContext db, CancellationToken ct)
    {
        var (link, project) = await GetAllowedLink(id, user, db, ct);
        if (link is null || project is null) return Results.NotFound();
        if (!CanRead(project, user)) return Results.Forbid();
        if (older == newer) return Results.BadRequest(new { message = "Hãy chọn hai lần phân tích khác nhau." });
        var runs = await db.CodeAnalysisRuns.AsNoTracking().Where(r =>
            (r.Id == older || r.Id == newer) && r.RepositoryLinkId == id && r.Status == "COMPLETED")
            .ToListAsync(ct);
        if (runs.Count != 2) return Results.BadRequest(new { message = "Hai báo cáo phải hoàn thành và thuộc cùng repository." });
        // Same SHA means different analysis settings, not actual code changes.
        var sameCommit = string.Equals(runs.Single(r => r.Id == older).CommitSha,
            runs.Single(r => r.Id == newer).CommitSha, StringComparison.OrdinalIgnoreCase);
        var a = await db.CodeAnalysisFindings.AsNoTracking().Where(f => f.RunId == older).ToListAsync(ct);
        var b = await db.CodeAnalysisFindings.AsNoTracking().Where(f => f.RunId == newer).ToListAsync(ct);
        var aKeys = a.Select(x => x.Fingerprint).ToHashSet();
        var bKeys = b.Select(x => x.Fingerprint).ToHashSet();
        return Results.Ok(new
        {
            older, newer, sameCommit,
            resolvedCandidates = a.Where(x => !bKeys.Contains(x.Fingerprint)).ToArray(),
            persistent = b.Where(x => aKeys.Contains(x.Fingerprint)).ToArray(),
            newFindings = b.Where(x => !aKeys.Contains(x.Fingerprint)).ToArray(),
            note = sameCommit
                ? "Hai báo cáo dùng cùng commit SHA. Chênh lệch phản ánh phạm vi/model/quy tắc phân tích, không chứng minh mã nguồn đã thay đổi."
                : "Đối chiếu theo bằng chứng và file; lỗi biến mất chưa chắc đã được sửa, cần giảng viên xác minh."
        });
    }

    private static async Task<IResult> Review(Guid id, ReviewInput? input, ClaimsPrincipal user,
        StudentProjectsDbContext db, CancellationToken ct)
    {
        if (!user.IsInRole("Lecturer")) return Results.Forbid();
        var run = await db.CodeAnalysisRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        if (run is null) return Results.NotFound();
        var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == run.ProjectId, ct);
        if (project is null) return Results.NotFound();
        if (project.LecturerUserId != Actor(user)) return Results.Forbid();
        if (run.Status != "COMPLETED") return Results.Conflict(new { message = "Chỉ phản hồi báo cáo đã hoàn thành." });
        var comment = input?.Comment?.Trim() ?? "";
        if (comment.Length < 5 || comment.Length > 2000)
            return Results.BadRequest(new { message = "Nhận xét phải dài từ 5 đến 2000 ký tự." });
        var review = new CodeAnalysisReview { RunId = id, LecturerUserId = Actor(user), Comment = comment };
        db.CodeAnalysisReviews.Add(review);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/v1/v11/analyses/{id}", review);
    }
}
