using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using StudentProjects.Api.Services;
using StudentProjects.Domain.Entities;
using StudentProjects.Domain.Enums;
using StudentProjects.Infrastructure.Persistence;

namespace StudentProjects.Api.Endpoints;

public static partial class V08Endpoints
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
        api.MapGet("/projects/{projectId:guid}/guidance-context", GuidanceContext);
        api.MapPut("/guidance/{id:guid}", UpdateGuidance);
        api.MapPost("/guidance/{id:guid}/review", ReviewGuidance);
        api.MapPost("/guidance/{id:guid}/task", CreateGuidanceTask);
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
        var activeProjects = projects.Where(p => p.Status != ProjectStatus.COMPLETED && p.Status != ProjectStatus.CANCELLED).ToArray();
        var milestoneIds = milestones.Select(m => m.Id).ToArray();
        var criteria = await db.MilestoneCriteria.AsNoTracking().Where(c => !c.IsDeleted && milestoneIds.Contains(c.MilestoneId)).ToListAsync(ct);
        var criterionIds = criteria.Select(c => c.Id).ToArray();
        var assessments = await db.MilestoneCriterionAssessments.AsNoTracking().Where(a => criterionIds.Contains(a.CriterionId) &&
            (a.Source != "AI" || a.Note == MilestoneAutoAssessment.EvidenceVersion)).ToListAsync(ct);
        var topicIds = projects.Select(p => p.TopicId).Distinct().ToArray();
        var titles = await db.Topics.AsNoTracking().Where(t => topicIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Title, ct);
        var today = now.ToOffset(TimeSpan.FromHours(7)).Date;
        var schedules = activeProjects.Select(p =>
        {
            var percent = ProjectEvidenceProgress.Calculate(milestones.Where(m => m.ProjectId == p.Id), criteria, assessments);
            return new { p.Id, ProjectId = p.Id, Title = titles.GetValueOrDefault(p.TopicId, "Đồ án"),
                p.StartAt, p.DeadlineAt, Percent = percent,
                Warning = ProgressDeadlinePolicy.Evaluate(p.StartAt, p.DeadlineAt, percent, now) };
        }).OrderBy(p => p.DeadlineAt ?? DateTimeOffset.MaxValue).ToArray();
        var overdue = schedules.Count(p => p.DeadlineAt.HasValue &&
            p.DeadlineAt.Value.ToOffset(TimeSpan.FromHours(7)).Date < today && p.Percent != 100m);
        var upcoming = schedules.Where(p => p.DeadlineAt.HasValue &&
            p.DeadlineAt.Value.ToOffset(TimeSpan.FromHours(7)).Date >= today &&
            p.DeadlineAt.Value.ToOffset(TimeSpan.FromHours(7)).Date <= today.AddDays(7)).ToArray();
        var pendingGuidance = await db.GuidanceSessions.AsNoTracking().CountAsync(g => ids.Contains(g.ProjectId) && g.LecturerConfirmedAt == null && g.ReviewStatus != "CHANGES_REQUESTED", ct);
        return Results.Ok(new {
            ProjectCount = projects.Count, PendingPlans = pendingPlans, PendingReviews = pendingReviews,
            Revisions = revisions, Overdue = overdue, UpcomingCount = upcoming.Length, Upcoming = upcoming, Schedules = schedules,
            AtRisk = schedules.Count(p => p.Warning is not null && (p.Warning.Level is "warning" or "danger") && p.Warning.Code != "OVERDUE"),
            PendingGuidance = pendingGuidance
        });
    }
}
