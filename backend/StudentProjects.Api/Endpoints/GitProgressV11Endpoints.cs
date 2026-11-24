using System.Security.Claims;
using System.Text.Json;
using System.Xml;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using StudentProjects.Domain.Entities;
using StudentProjects.Infrastructure.Persistence;
using StudentProjects.Api.Services;

namespace StudentProjects.Api.Endpoints;

public static class GitProgressV11Endpoints
{
    public sealed record StartInput(string? BaseSha, string? HeadSha);

    public static void MapGitProgressV11Endpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1/v11").RequireAuthorization(p => p.RequireRole("Student", "Lecturer", "Admin"));
        api.MapPost("/repositories/{id:guid}/progress", Start).RequireRateLimiting("analysis-start");
        api.MapGet("/repositories/{id:guid}/progress", List);
        api.MapGet("/progress/{id:guid}", Get);
        api.MapPost("/progress/{id:guid}/word-comparison", CompareWord)
            .RequireAuthorization(p => p.RequireRole("Lecturer")).RequireRateLimiting("analysis-start");
    }

    private static async Task<IResult> CompareWord(Guid id, HttpRequest request, ClaimsPrincipal user,
        StudentProjectsDbContext db, AiGitProgressClient ai, CancellationToken ct)
    {
        var run = await db.GitProgressRuns.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (run is null) return Results.NotFound();
        var assigned = await db.Projects.AnyAsync(x => x.Id == run.ProjectId && x.LecturerUserId == Actor(user), ct);
        if (!assigned) return Results.Forbid();
        if (run.Status != "COMPLETED") return Results.Conflict(new { message = "Chờ báo cáo Git hoàn tất rồi đối chiếu." });

        const int maxRequest = GitProgressWordComparison.MaxFileBytes + 16 * 1024;
        if (request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
            limit.MaxRequestBodySize = maxRequest;
        if (request.ContentLength > maxRequest) return Results.BadRequest(new { message = "File Word tối đa 2 MB." });
        if (!request.HasFormContentType) return Results.BadRequest(new { message = "Hãy tải một file Word .docx." });

        IReadOnlyList<WordReportLine> claims;
        try
        {
            var form = await request.ReadFormAsync(ct);
            var file = form.Files.GetFile("file");
            if (form.Files.Count != 1 || file is null || file.Length <= 0 ||
                file.Length > GitProgressWordComparison.MaxFileBytes ||
                !Path.GetExtension(file.FileName).Equals(".docx", StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest(new { message = "Chọn một file Word .docx tối đa 2 MB." });
            await using var stream = file.OpenReadStream();
            claims = GitProgressWordComparison.ReadClaims(stream);
        }
        catch (InvalidDataException)
        {
            return Results.BadRequest(new { message = "Không đọc được nội dung Word. Dùng file .docx có văn bản, tối đa 20 công việc và 500 ký tự mỗi mục." });
        }
        catch (XmlException)
        {
            return Results.BadRequest(new { message = "File Word có nội dung XML không hợp lệ." });
        }

        IReadOnlyList<WordEvidence> evidence;
        try { evidence = GitProgressWordComparison.ReadEvidence(run.EvidenceJson); }
        catch (InvalidDataException ex) { return Results.Conflict(new { message = ex.Message }); }
        if (evidence.Count == 0) return Results.Conflict(new { message = "Báo cáo Git chưa có mục công việc kèm dòng bằng chứng." });
        try { return Results.Ok(await ai.CompareWordAsync(claims, evidence, ct)); }
        catch (InvalidDataException ex) { return Results.Json(new { message = ex.Message }, statusCode: 502); }
        catch (Exception ex) when (!ct.IsCancellationRequested &&
            ex is HttpRequestException or JsonException or OperationCanceledException)
        {
            return Results.Json(new { message = "Ollama chưa trả được kết quả. Kiểm tra Ollama rồi thử lại." }, statusCode: 502);
        }
    }

    private static Guid Actor(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
    private static bool CanRead(Project project, ClaimsPrincipal user) => user.IsInRole("Admin") ||
        (user.IsInRole("Student") && project.StudentUserId == Actor(user)) ||
        (user.IsInRole("Lecturer") && project.LecturerUserId == Actor(user));
    private static bool CanStart(Project project, ClaimsPrincipal user) =>
        (user.IsInRole("Student") && project.StudentUserId == Actor(user)) ||
        (user.IsInRole("Lecturer") && project.LecturerUserId == Actor(user));
    private static async Task<(RepositoryLink? Link, Project? Project)> FindLinkAsync(
        Guid id, StudentProjectsDbContext db, CancellationToken ct)
    {
        var link = await db.Repositories.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        var project = link is null ? null : await db.Projects.AsNoTracking().FirstOrDefaultAsync(x => x.Id == link.ProjectId, ct);
        return (link, project);
    }

    private static async Task<IResult> Start(Guid id, StartInput? input, ClaimsPrincipal user,
        StudentProjectsDbContext db, AiGitProgressClient ai, CancellationToken ct)
    {
        var (link, project) = await FindLinkAsync(id, db, ct);
        if (link is null || project is null) return Results.NotFound();
        if (!CanStart(project, user)) return Results.Forbid();
        if (!GitHubUrl.TryParse(link.RepositoryUrl, out _, out _))
            return Results.Conflict(new { message = "URL repository đã lưu không hợp lệ." });
        if (!GitProgressEvidencePolicy.IsSha(input?.BaseSha) ||
            !GitProgressEvidencePolicy.IsSha(input?.HeadSha))
            return Results.BadRequest(new { message = "Chọn đủ hai SHA commit 40 ký tự từ danh sách GitHub." });
        var before = input!.BaseSha!.ToLowerInvariant();
        var after = input.HeadSha!.ToLowerInvariant();
        if (before == after) return Results.BadRequest(new { message = "Hai commit phải khác nhau." });
        var existing = await db.GitProgressRuns.FirstOrDefaultAsync(x => x.RepositoryLinkId == id &&
            x.BaseSha == before && x.HeadSha == after && x.ModelName == ai.AnalysisKey, ct);
        if (existing is not null)
        {
            // Retry incomplete AI while leaving persisted Git evidence intact until the worker saves.
            var diagnostics = GitProgressDiagnostics.Read(existing.EvidenceJson);
            if (existing.Status == "FAILED" || (existing.Status == "COMPLETED" &&
                (diagnostics?.AnalysisStatus is "timeout" or "connection-error" or "invalid-response" ||
                 diagnostics?.FeatureReportStatus == "error")))
            {
                existing.Status = "PENDING";
                existing.StartedAt = null;
                existing.CompletedAt = null;
                existing.ErrorMessage = null;
                await db.SaveChangesAsync(ct);
            }
            db.AuditEntries.Add(MilestoneAssessmentRequests.CreateComparison(existing.Id, Actor(user)));
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { existing.Id, existing.Status, cached = existing.Status == "COMPLETED" });
        }
        var run = new GitProgressRun
        {
            ProjectId = project.Id, RepositoryLinkId = id, RequestedByUserId = Actor(user),
            BaseSha = before, HeadSha = after, ModelName = ai.AnalysisKey
        };
        db.GitProgressRuns.Add(run);
        var assessmentRequest = MilestoneAssessmentRequests.CreateComparison(run.Id, Actor(user));
        db.AuditEntries.Add(assessmentRequest);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            // Concurrent requests are deduplicated by the unique index.
            var duplicate = await db.GitProgressRuns.AsNoTracking().FirstOrDefaultAsync(x =>
                x.RepositoryLinkId == id && x.BaseSha == before && x.HeadSha == after && x.ModelName == ai.AnalysisKey, ct);
            if (duplicate is not null)
            {
                db.Entry(run).State = EntityState.Detached;
                db.Entry(assessmentRequest).State = EntityState.Detached;
                db.AuditEntries.Add(MilestoneAssessmentRequests.CreateComparison(duplicate.Id, Actor(user)));
                await db.SaveChangesAsync(ct);
                return Results.Ok(new { duplicate.Id, duplicate.Status, cached = duplicate.Status == "COMPLETED" });
            }
            throw;
        }
        return Results.Accepted($"/api/v1/v11/progress/{run.Id}", new { run.Id, run.Status, cached = false });
    }

    private static async Task<IResult> List(Guid id, ClaimsPrincipal user, StudentProjectsDbContext db, CancellationToken ct)
    {
        var (link, project) = await FindLinkAsync(id, db, ct);
        if (link is null || project is null) return Results.NotFound();
        if (!CanRead(project, user)) return Results.Forbid();
        var runs = await db.GitProgressRuns.AsNoTracking().Where(x => x.RepositoryLinkId == id)
            .OrderByDescending(x => x.CreatedAt).Take(40)
            .Select(x => new { x.Id, x.BaseSha, x.HeadSha, x.ModelName, x.Status,
                x.TotalCommits, x.TotalFiles, x.CreatedAt, x.CompletedAt })
            .ToListAsync(ct);
        return Results.Ok(runs);
    }

    private static async Task<IResult> Get(Guid id, ClaimsPrincipal user, StudentProjectsDbContext db, CancellationToken ct)
    {
        var run = await db.GitProgressRuns.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (run is null) return Results.NotFound();
        var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(x => x.Id == run.ProjectId, ct);
        if (project is null) return Results.NotFound();
        if (!CanRead(project, user)) return Results.Forbid();
        var url = await db.Repositories.AsNoTracking().Where(x => x.Id == run.RepositoryLinkId)
            .Select(x => x.RepositoryUrl).FirstOrDefaultAsync(ct);
        return Results.Ok(new { run.Id, run.ProjectId, run.RepositoryLinkId, run.BaseSha, run.HeadSha,
            run.ModelName, run.Status, run.TotalCommits, run.TotalFiles, run.Additions, run.Deletions,
            run.SentFiles, run.PartialCoverage, run.Summary, run.FilesJson,
            evidenceJson = GitProgressDiagnostics.PublicEvidence(run.EvidenceJson),
            aiDiagnostics = GitProgressDiagnostics.Read(run.EvidenceJson),
            run.ErrorMessage, run.CreatedAt, run.StartedAt, run.CompletedAt, repositoryUrl = url });
    }
}
