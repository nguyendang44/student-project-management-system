using Microsoft.EntityFrameworkCore;
using StudentProjects.Domain.Entities;
using StudentProjects.Domain.Enums;
using StudentProjects.Infrastructure.Persistence;

namespace StudentProjects.Api.Services;

/// <summary>Server-side notification derivation from real v0.7 entities.
/// No client-issued notification creation. Unique SQL index prevents duplicate dispatches.
/// Single-flight within an API host; events are kept idempotent across host restarts.
/// </summary>
public sealed class V08NotificationEngine(IServiceScopeFactory scopeFactory, ILogger<V08NotificationEngine> logger)
{
    private readonly SemaphoreSlim _singleFlight = new(1, 1);
    private static readonly TimeZoneInfo Vietnam = ResolveVietnamTimeZone();

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        foreach (var name in new[] { "Asia/Bangkok", "SE Asia Standard Time" })
            try { return TimeZoneInfo.FindSystemTimeZoneById(name); } catch (TimeZoneNotFoundException) { }
        return TimeZoneInfo.CreateCustomTimeZone("UTC+07", TimeSpan.FromHours(7), "UTC+07", "UTC+07");
    }

    public async Task RefreshAsync(CancellationToken ct)
    {
        await _singleFlight.WaitAsync(ct);
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<StudentProjectsDbContext>();
            var now = DateTimeOffset.UtcNow;
            var today = TimeZoneInfo.ConvertTime(now, Vietnam).Date;
            var cutoff = now.AddDays(-14);
            var projects = await db.Projects.AsNoTracking()
                .Where(p => p.Status != ProjectStatus.CANCELLED).ToListAsync(ct);
            var byProject = projects.ToDictionary(p => p.Id);
            var ids = byProject.Keys.ToArray();
            if (ids.Length == 0) return;
            var milestones = await db.Milestones.AsNoTracking().Where(m => ids.Contains(m.ProjectId)).ToListAsync(ct);
            var byMilestone = milestones.ToDictionary(m => m.Id);
            var mid = byMilestone.Keys.ToArray();
            var submissions = await db.MilestoneSubmissions.AsNoTracking()
                .Where(s => mid.Contains(s.MilestoneId) && s.SubmittedAt >= cutoff).ToListAsync(ct);
            var reviews = await db.ProjectEvaluations.AsNoTracking()
                .Where(r => ids.Contains(r.ProjectId) && r.CreatedAt >= cutoff).ToListAsync(ct);
            var audits = await db.AuditEntries.AsNoTracking()
                .Where(a => a.CreatedAt >= cutoff && a.EntityId != null &&
                    (a.EntityName == "Milestone" || a.EntityName == "GuidanceSession"))
                .ToListAsync(ct);
            var journalIds = audits.Where(a => a.EntityName == "GuidanceSession" && a.EntityId.HasValue)
                .Select(a => a.EntityId!.Value).Distinct().ToArray();
            var journalMap = await db.GuidanceSessions.AsNoTracking().Where(j => journalIds.Contains(j.Id))
                .ToDictionaryAsync(j => j.Id, ct);
            var recipients = projects.SelectMany(p => new[] { p.StudentUserId, p.LecturerUserId }).Distinct().ToArray();
            var existing = await db.Notifications.AsNoTracking()
                .Where(n => recipients.Contains(n.RecipientUserId) && n.Type.StartsWith("V08:"))
                .Select(n => new { n.RecipientUserId, n.Type }).ToListAsync(ct);
            var keys = new HashSet<string>(existing.Select(n => $"{n.RecipientUserId:N}:{n.Type}"));
            void Queue(Guid userId, string type, string message)
            {
                if (userId == Guid.Empty || message.Length > 4000) return;
                if (!keys.Add($"{userId:N}:{type}")) return;
                db.Notifications.Add(new Notification { RecipientUserId = userId, Type = type, Message = message });
            }
            foreach (var m in milestones)
            {
                var p = byProject[m.ProjectId];
                if (m.ProposalStatus != "CONFIRMED" || m.Status == MilestoneStatus.APPROVED || p.Status == ProjectStatus.COMPLETED)
                    continue;
                var deadlineDate = TimeZoneInfo.ConvertTime(m.DeadlineAt, Vietnam).Date;
                var days = (deadlineDate - today).Days;
                if (days is 7 or 3 or 1)
                    Queue(p.StudentUserId, $"V08:DUE:{m.Id:N}:{deadlineDate:yyyyMMdd}:{days}",
                        $"Milestone '{m.Title}' còn {days} ngày đến hạn ({deadlineDate:dd/MM/yyyy}).");
                if (m.DeadlineAt < now)
                {
                    Queue(p.StudentUserId, $"V08:LATE:{m.Id:N}:{deadlineDate:yyyyMMdd}",
                        $"Milestone '{m.Title}' đã quá hạn ({deadlineDate:dd/MM/yyyy}).");
                    Queue(p.LecturerUserId, $"V08:LATE:{m.Id:N}:{deadlineDate:yyyyMMdd}",
                        $"Milestone '{m.Title}' của sinh viên đang quá hạn ({deadlineDate:dd/MM/yyyy}).");
                }
            }
            foreach (var s in submissions)
                if (byMilestone.TryGetValue(s.MilestoneId, out var m))
                    Queue(byProject[m.ProjectId].LecturerUserId, $"V08:SUB:{s.Id:N}", $"Sinh viên đã nộp kết quả Milestone: {m.Title}.");
            foreach (var r in reviews)
                if (byProject.TryGetValue(r.ProjectId, out var p))
                {
                    var name = r.MilestoneId is Guid midId && byMilestone.TryGetValue(midId, out var rm) ? rm.Title : "Project";
                    Queue(p.StudentUserId, $"V08:REVIEW:{r.Id:N}",
                        $"Giảng viên đã {(r.IsApproved ? "phê duyệt" : "nhận xét/yêu cầu chỉnh sửa")} kết quả: {name}.");
                }
            // Audit entry IDs are stable event identifiers. UpdatedAt on a Milestone also
            // changes for progress/reviews, so it MUST NOT identify a plan approval event.
            foreach (var a in audits)
            {
                if (a.EntityId is not Guid entityId) continue;
                if (a.EntityName == "Milestone" && byMilestone.TryGetValue(entityId, out var m)
                    && byProject.TryGetValue(m.ProjectId, out var p))
                {
                    var msg = a.Action switch {
                        "MILESTONE_PROPOSED" or "MILESTONE_PROPOSED_FROM_TEMPLATE" or "MILESTONE_PROPOSAL_RESUBMITTED"
                            => $"Sinh viên gửi đề xuất kế hoạch: {m.Title}.",
                        "MILESTONE_PROPOSAL_CONFIRMED" => $"Giảng viên đã xác nhận kế hoạch: {m.Title}.",
                        "MILESTONE_PROPOSAL_CHANGES_REQUESTED" => $"Giảng viên yêu cầu sửa kế hoạch: {m.Title}.",
                        _ => null
                    };
                    if (msg is not null)
                    {
                        var to = a.Action is "MILESTONE_PROPOSAL_CONFIRMED" or "MILESTONE_PROPOSAL_CHANGES_REQUESTED"
                            ? p.StudentUserId : p.LecturerUserId;
                        Queue(to, $"V08:PLAN:{a.Id:N}", msg);
                    }
                }
                else if (a.EntityName == "GuidanceSession" && journalMap.TryGetValue(entityId, out var j)
                    && byProject.TryGetValue(j.ProjectId, out var journalProject))
                {
                    if (a.Action == "GUIDANCE_LOG_CREATED")
                        Queue(j.CreatedByUserId == journalProject.StudentUserId ? journalProject.LecturerUserId : journalProject.StudentUserId,
                            $"V08:MEET:{a.Id:N}", $"Có nhật ký buổi hướng dẫn mới ngày {TimeZoneInfo.ConvertTime(j.MeetingAt, Vietnam):dd/MM/yyyy}.");
                    else if (a.Action == "GUIDANCE_LOG_CONFIRMED")
                        Queue(journalProject.StudentUserId, $"V08:MEETOK:{a.Id:N}", "Giảng viên đã xác nhận một nhật ký hướng dẫn.");
                }
            }
            if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is DbUpdateException)
        {
            // Another instance may have inserted an identical event; unique SQL constraint keeps exactly one.
            logger.LogWarning(ex, "v0.8 notification deduplication/conflict; will retry next run.");
        }
        finally { _singleFlight.Release(); }
    }
}

public sealed class V08NotifierWorker(IServiceScopeFactory scopeFactory, ILogger<V08NotifierWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<V08NotificationEngine>().RefreshAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning(ex, "v0.8 notification cycle failed; check DB upgrade."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
