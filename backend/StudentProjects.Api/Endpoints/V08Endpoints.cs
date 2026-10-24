using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using StudentProjects.Api.Services;
using StudentProjects.Domain.Entities;
using StudentProjects.Domain.Enums;
using StudentProjects.Infrastructure.Persistence;

namespace StudentProjects.Api.Endpoints;

public static class V08Endpoints
{
    public static void MapV08Endpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1/v08").RequireAuthorization(p => p.RequireRole("Student", "Lecturer", "Admin"));
        api.MapGet("/notifications", Notifications);
        api.MapPost("/notifications/{id:guid}/read", ReadNotification);
        api.MapPost("/notifications/read-all", ReadAll);
        api.MapGet("/dashboard", Dashboard);
        api.MapGet("/projects/{projectId:guid}/guidance", Guidance);
        api.MapPost("/projects/{projectId:guid}/guidance", CreateGuidance);
        api.MapPost("/guidance/{id:guid}/confirm", ConfirmGuidance);
    }
    private static Guid Actor(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
    private static bool CanRead(Project p, ClaimsPrincipal user) => user.IsInRole("Admin") ||
        (user.IsInRole("Student") && p.StudentUserId == Actor(user)) ||
        (user.IsInRole("Lecturer") && p.LecturerUserId == Actor(user));
    private static bool CanWrite(Project p, ClaimsPrincipal user) =>
        (user.IsInRole("Student") && p.StudentUserId == Actor(user)) ||
        (user.IsInRole("Lecturer") && p.LecturerUserId == Actor(user));
    private static IResult Bad(string message) => Results.BadRequest(new { code = "INVALID_DATA", message });

    private static async Task<IResult> Notifications(ClaimsPrincipal user, StudentProjectsDbContext db,
        V08NotificationEngine engine, CancellationToken ct)
    {
        var actor = Actor(user);
        if (actor == Guid.Empty) return Results.Unauthorized();
        await engine.RefreshAsync(ct);
        var rows = await db.Notifications.AsNoTracking().Where(n => n.RecipientUserId == actor && n.Type.StartsWith("V08:"))
            .OrderByDescending(n => n.CreatedAt).Take(100)
            .Select(n => new { n.Id, n.Type, n.Message, n.IsRead, n.CreatedAt }).ToListAsync(ct);
        var unread = await db.Notifications.AsNoTracking().CountAsync(n => n.RecipientUserId == actor && !n.IsRead && n.Type.StartsWith("V08:"), ct);
        // V0804_NOTIFICATION_LINKS: resolve historic notifications to their original objects, with permission checks.
        var targets = await V08NotificationLinks.ResolveAsync(db, user, actor, rows.Select(n => n.Type), ct);
        var linked = rows.Select(n => new { n.Id, n.Type, n.Message, n.IsRead, n.CreatedAt,
            TargetUrl = targets.TryGetValue(n.Type, out var destination) ? destination : (string?)null });
        return Results.Ok(new { Items = linked, UnreadCount = unread });
    }
    private static async Task<IResult> ReadNotification(Guid id, ClaimsPrincipal user, StudentProjectsDbContext db, CancellationToken ct)
    {
        var n = await db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.RecipientUserId == Actor(user) && n.Type.StartsWith("V08:"), ct);
        if (n == null) return Results.NotFound();
        n.IsRead = true;
        n.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
    private static async Task<IResult> ReadAll(ClaimsPrincipal user, StudentProjectsDbContext db, CancellationToken ct)
    {
        await db.Notifications.Where(n => n.RecipientUserId == Actor(user) && !n.IsRead && n.Type.StartsWith("V08:"))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsRead, true).SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow), ct);
        return Results.NoContent();
    }
    private static async Task<IResult> Dashboard(ClaimsPrincipal user, StudentProjectsDbContext db, CancellationToken ct)
    {
        var actor = Actor(user);
        var query = db.Projects.AsNoTracking();
        if (user.IsInRole("Student")) query = query.Where(p => p.StudentUserId == actor);
        else if (user.IsInRole("Lecturer")) query = query.Where(p => p.LecturerUserId == actor);
        else if (!user.IsInRole("Admin")) return Results.Forbid();
        var projects = await query.ToListAsync(ct);
        var ids = projects.Select(p => p.Id).ToArray();
        var milestones = await db.Milestones.AsNoTracking().Where(m => ids.Contains(m.ProjectId)).ToListAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var pendingPlans = milestones.Count(m => m.ProposalStatus == "PENDING_REVIEW");
        var pendingReviews = milestones.Count(m => m.ProposalStatus == "CONFIRMED" && m.Status == MilestoneStatus.SUBMITTED);
        var revisions = milestones.Count(m => m.ProposalStatus == "CHANGES_REQUESTED" || m.Status == MilestoneStatus.REVISION_REQUIRED);
        var overdue = milestones.Count(m => m.ProposalStatus == "CONFIRMED" && m.Status != MilestoneStatus.APPROVED && m.DeadlineAt < now);
        var upcoming = milestones.Where(m => m.ProposalStatus == "CONFIRMED" && m.Status != MilestoneStatus.APPROVED &&
            m.DeadlineAt >= now && m.DeadlineAt <= now.AddDays(7))
            .OrderBy(m => m.DeadlineAt).Take(10)
            .Select(m => new { m.Id, m.ProjectId, m.Title, m.DeadlineAt }).ToList();
        var pendingGuidance = await db.GuidanceSessions.AsNoTracking().CountAsync(g => ids.Contains(g.ProjectId) && g.LecturerConfirmedAt == null, ct);
        return Results.Ok(new {
            ProjectCount = projects.Count, PendingPlans = pendingPlans, PendingReviews = pendingReviews,
            Revisions = revisions, Overdue = overdue, UpcomingCount = upcoming.Count, Upcoming = upcoming,
            PendingGuidance = pendingGuidance
        });
    }
    private static async Task<IResult> Guidance(Guid projectId, ClaimsPrincipal user, StudentProjectsDbContext db, CancellationToken ct)
    {
        var p = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (p == null) return Results.NotFound();
        if (!CanRead(p, user)) return Results.Forbid();
        var rows = await db.GuidanceSessions.AsNoTracking().Where(g => g.ProjectId == projectId)
            .OrderByDescending(g => g.MeetingAt).Take(200)
            .Select(g => new { g.Id, g.ProjectId, g.CreatedByUserId, g.MeetingAt, g.Summary,
                g.Feedback, g.NextActions, g.LecturerConfirmedAt, g.CreatedAt }).ToListAsync(ct);
        return Results.Ok(rows);
    }
    public sealed record GuidanceInput(DateTimeOffset MeetingAt, string? Summary, string? Feedback, string? NextActions);
    private static async Task<IResult> CreateGuidance(Guid projectId, GuidanceInput? input, ClaimsPrincipal user,
        StudentProjectsDbContext db, CancellationToken ct)
    {
        var p = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (p == null) return Results.NotFound();
        if (!CanWrite(p, user)) return Results.Forbid();
        if (p.Status == ProjectStatus.CANCELLED) return Results.Conflict(new { code="INVALID_STATE", message="Project đã hủy." });
        if (input == null || input.MeetingAt == default || input.MeetingAt > DateTimeOffset.UtcNow.AddDays(1) ||
            string.IsNullOrWhiteSpace(input.Summary) || input.Summary.Trim().Length > 4000 ||
            (input.Feedback?.Length ?? 0) > 4000 || (input.NextActions?.Length ?? 0) > 4000)
            return Bad("Ngày gặp hoặc nội dung nhật ký không hợp lệ (tối đa 4000 ký tự/trường).");
        var entry = new GuidanceSession {
            ProjectId = projectId, CreatedByUserId = Actor(user), MeetingAt = input.MeetingAt,
            Summary = input.Summary.Trim(), Feedback = (input.Feedback ?? "").Trim(),
            NextActions = (input.NextActions ?? "").Trim()
        };
        db.GuidanceSessions.Add(entry);
        db.AuditEntries.Add(new AuditEntry { ActorUserId = Actor(user), Action="GUIDANCE_LOG_CREATED", EntityName="GuidanceSession", EntityId=entry.Id });
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/v1/v08/projects/{projectId}/guidance", new { entry.Id });
    }
    private static async Task<IResult> ConfirmGuidance(Guid id, ClaimsPrincipal user, StudentProjectsDbContext db, CancellationToken ct)
    {
        var entry = await db.GuidanceSessions.FirstOrDefaultAsync(g => g.Id == id, ct);
        if (entry == null) return Results.NotFound();
        var p = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == entry.ProjectId, ct);
        if (p == null) return Results.NotFound();
        if (!user.IsInRole("Lecturer") || p.LecturerUserId != Actor(user)) return Results.Forbid();
        if (entry.LecturerConfirmedAt != null) return Results.Conflict(new { code="ALREADY_CONFIRMED", message="Nhật ký đã được xác nhận." });
        entry.LecturerConfirmedAt = DateTimeOffset.UtcNow;
        entry.UpdatedAt = DateTimeOffset.UtcNow;
        db.AuditEntries.Add(new AuditEntry { ActorUserId = Actor(user), Action="GUIDANCE_LOG_CONFIRMED", EntityName="GuidanceSession", EntityId=entry.Id });
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { entry.Id, entry.LecturerConfirmedAt });
    }
}
