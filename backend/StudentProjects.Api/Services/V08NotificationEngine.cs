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

    // V094_REQUEST_NOTIFICATIONS
    // On-demand when Notifications is opened and also by the background worker.
    // Pending requests from older releases are backfilled once. Subsequent decisions
    // use immutable audit IDs, so resubmissions/revisions are separate events.
    private static async Task RefreshRequestNotificationsAsync(StudentProjectsDbContext db,
        DateTimeOffset now, CancellationToken ct)
    {
        var recent = now.AddDays(-30);
        var events = await db.AuditEntries.AsNoTracking()
            .Where(a => a.EntityName == "LecturerRequest" && a.EntityId != null && a.CreatedAt >= recent &&
                (a.Action == "JOINT_APPLICATION_RESUBMITTED" ||
                 a.Action == "JOINT_APPLICATION_REVISION_REQUIRED" ||
                 a.Action == "LECTURER_OFFERED_SUPERVISION" ||
                 a.Action == "LECTURER_REQUEST_REJECTED" ||
                 a.Action == "LECTURER_REQUEST_CANCELLED"))
            .ToListAsync(ct);
        var eventRequestIds = events.Select(a => a.EntityId!.Value).Distinct().ToArray();
        var requests = await db.LecturerRequests.AsNoTracking()
            .Where(r => r.Status == RequestStatus.PENDING || eventRequestIds.Contains(r.Id))
            .ToListAsync(ct);
        if (requests.Count == 0) return;

        var byRequest = requests.ToDictionary(r => r.Id);
        var peopleIds = requests.SelectMany(r => new[] { r.StudentUserId, r.LecturerUserId }).Distinct().ToArray();
        var names = await db.Users.AsNoTracking().Where(u => peopleIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        var topicIds = requests.Select(r => r.TopicId).Distinct().ToArray();
        var titles = await db.Topics.AsNoTracking().Where(t => topicIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Title, ct);
        var receivers = peopleIds;
        var seenRows = await db.Notifications.AsNoTracking()
            .Where(n => receivers.Contains(n.RecipientUserId) && n.Type.StartsWith("V08:REQ"))
            .Select(n => new { n.RecipientUserId, n.Type }).ToListAsync(ct);
        var seen = new HashSet<string>(seenRows.Select(n => $"{n.RecipientUserId:N}:{n.Type}"));

        void Queue(Guid recipient, string type, string message)
        {
            if (recipient == Guid.Empty || message.Length > 4000 ||
                !seen.Add($"{recipient:N}:{type}")) return;
            db.Notifications.Add(new Notification { RecipientUserId = recipient, Type = type,
                Message = message });
        }
        string TopicName(LecturerRequest r) =>
            !string.IsNullOrWhiteSpace(r.DraftTitle) ? r.DraftTitle! :
            titles.TryGetValue(r.TopicId, out var name) ? name : "đề tài";
        string StudentName(LecturerRequest r) =>
            names.TryGetValue(r.StudentUserId, out var name) ? name : "Sinh viên";
        string LecturerName(LecturerRequest r) =>
            names.TryGetValue(r.LecturerUserId, out var name) ? name : "Giảng viên";

        foreach (var r in requests.Where(r => r.Status == RequestStatus.PENDING))
            Queue(r.LecturerUserId, $"V08:REQ:{r.Id:N}",
                $"Sinh viên {StudentName(r)} gửi yêu cầu hướng dẫn đề tài: {TopicName(r)}.");

        foreach (var e in events)
        {
            if (e.EntityId is not Guid requestId || !byRequest.TryGetValue(requestId, out var r)) continue;
            var message = e.Action switch
            {
                "JOINT_APPLICATION_RESUBMITTED" => $"Sinh viên {StudentName(r)} đã gửi lại đề xuất: {TopicName(r)}.",
                "JOINT_APPLICATION_REVISION_REQUIRED" => $"Giảng viên {LecturerName(r)} yêu cầu chỉnh sửa đề xuất: {TopicName(r)}.",
                "LECTURER_OFFERED_SUPERVISION" => $"Giảng viên {LecturerName(r)} đã đồng ý hướng dẫn đề tài: {TopicName(r)}. Hãy xác nhận giảng viên.",
                "LECTURER_REQUEST_REJECTED" => $"Giảng viên {LecturerName(r)} đã từ chối yêu cầu hướng dẫn đề tài: {TopicName(r)}.",
                "LECTURER_REQUEST_CANCELLED" => $"Sinh viên {StudentName(r)} đã hủy yêu cầu hướng dẫn đề tài: {TopicName(r)}.",
                _ => null
            };
            if (message is null) continue;
            var recipient = e.Action is "JOINT_APPLICATION_RESUBMITTED" or "LECTURER_REQUEST_CANCELLED"
                ? r.LecturerUserId : r.StudentUserId;
            Queue(recipient, $"V08:REQEV:{e.Id:N}", message);
        }
        if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync(ct);
    }

    public async Task RefreshAsync(CancellationToken ct)
    {
        await _singleFlight.WaitAsync(ct);
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<StudentProjectsDbContext>();
            var now = DateTimeOffset.UtcNow;
            // V094_REQUEST_NOTIFICATIONS: requests exist BEFORE Projects, so this must run
            // before the early return for accounts without a Project.
            await RefreshRequestNotificationsAsync(db, now, ct);
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
            var criteria = await db.MilestoneCriteria.AsNoTracking().Where(c => mid.Contains(c.MilestoneId) && !c.IsDeleted).ToListAsync(ct);
            var byCriterion = criteria.ToDictionary(c => c.Id);
            var criterionIds = byCriterion.Keys.ToArray();
            var confirmed = await db.MilestoneCriterionAssessments.AsNoTracking()
                .Where(a => criterionIds.Contains(a.CriterionId) && a.Source == "REVIEW")
                .Select(a => new { a.Id, a.CriterionId, a.Outcome, a.CreatedAt }).ToListAsync(ct);
            var achieved = confirmed.Where(a => byCriterion.TryGetValue(a.CriterionId, out var c) &&
                (!c.DefinitionChangedAt.HasValue || a.CreatedAt >= c.DefinitionChangedAt.Value)).GroupBy(a => a.CriterionId).Where(g =>
                g.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id).First().Outcome == "full")
                .Select(g => g.Key).ToHashSet();
            var submissions = await db.MilestoneSubmissions.AsNoTracking()
                .Where(s => mid.Contains(s.MilestoneId) && s.SubmittedAt >= cutoff).ToListAsync(ct);
            var reviews = await db.ProjectEvaluations.AsNoTracking()
                .Where(r => ids.Contains(r.ProjectId) && r.CreatedAt >= cutoff).ToListAsync(ct);
            var audits = await db.AuditEntries.AsNoTracking()
                .Where(a => a.CreatedAt >= cutoff && a.EntityId != null &&
                    (a.EntityName == "Milestone" || a.EntityName == "GuidanceSession" ||
                     (a.EntityName == "Project" && a.Action == "PROJECT_DEADLINE_UPDATED") ||
                     (a.EntityName == "MilestoneCriterion" && a.Action == "CRITERION_DEADLINE_UPDATED")))
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
            foreach (var p in projects)
            {
                if (p.DeadlineAt is not DateTimeOffset deadline || p.Status == ProjectStatus.COMPLETED) continue;
                var deadlineDate = TimeZoneInfo.ConvertTime(deadline, Vietnam).Date;
                var days = (deadlineDate - today).Days;
                if (days is 7 or 3 or 1)
                    Queue(p.StudentUserId, $"V08:PDUE:{p.Id:N}:{deadlineDate:yyyyMMdd}:{days}",
                        $"Đồ án còn {days} ngày đến hạn nộp ({deadlineDate:dd/MM/yyyy}).");
                if (deadline < now)
                {
                    Queue(p.StudentUserId, $"V08:PLATE:{p.Id:N}:{deadlineDate:yyyyMMdd}",
                        $"Đồ án đã quá hạn nộp ({deadlineDate:dd/MM/yyyy}).");
                    Queue(p.LecturerUserId, $"V08:PLATE:{p.Id:N}:{deadlineDate:yyyyMMdd}",
                        $"Đồ án của sinh viên đã quá hạn nộp ({deadlineDate:dd/MM/yyyy}).");
                }
            }
            foreach (var criterion in criteria)
            {
                if (criterion.DeadlineAt is not DateTimeOffset deadline || achieved.Contains(criterion.Id) ||
                    !byMilestone.TryGetValue(criterion.MilestoneId, out var milestone) ||
                    milestone.ProposalStatus != "CONFIRMED" || milestone.Status == MilestoneStatus.APPROVED) continue;
                var project = byProject[milestone.ProjectId];
                if (project.Status == ProjectStatus.COMPLETED) continue;
                var date = TimeZoneInfo.ConvertTime(deadline, Vietnam).Date;
                var days = (date - today).Days;
                if (days is 7 or 3 or 1)
                    Queue(project.StudentUserId, $"V08:CDUE:{criterion.Id:N}:{date:yyyyMMdd}:{days}",
                        $"Đầu việc '{criterion.Title}' thuộc Milestone '{milestone.Title}' còn {days} ngày đến hạn ({date:dd/MM/yyyy}).");
                if (deadline < now)
                {
                    Queue(project.StudentUserId, $"V08:CLATE:{criterion.Id:N}:{date:yyyyMMdd}",
                        $"Đầu việc '{criterion.Title}' thuộc Milestone '{milestone.Title}' đã quá hạn ({date:dd/MM/yyyy}).");
                    Queue(project.LecturerUserId, $"V08:CLATE:{criterion.Id:N}:{date:yyyyMMdd}",
                        $"Đầu việc '{criterion.Title}' của sinh viên đã quá hạn ({date:dd/MM/yyyy}).");
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
                if (a.EntityName == "Project" && a.Action == "PROJECT_DEADLINE_UPDATED" &&
                    byProject.TryGetValue(entityId, out var deadlineProject) &&
                    deadlineProject.DeadlineAt is DateTimeOffset projectDeadline)
                {
                    Queue(deadlineProject.StudentUserId, $"V08:PDEAD:{a.Id:N}",
                        $"Giảng viên đã cập nhật deadline toàn dự án. Ngày hiện tại là {TimeZoneInfo.ConvertTime(projectDeadline, Vietnam):dd/MM/yyyy}.");
                }
                else if (a.EntityName == "MilestoneCriterion" && a.Action == "CRITERION_DEADLINE_UPDATED" &&
                    byCriterion.TryGetValue(entityId, out var criterion) &&
                    byMilestone.TryGetValue(criterion.MilestoneId, out var criterionMilestone) &&
                    criterion.DeadlineAt is DateTimeOffset criterionDeadline)
                {
                    Queue(byProject[criterionMilestone.ProjectId].StudentUserId, $"V08:CDEAD:{a.Id:N}",
                        $"Giảng viên đã đặt deadline đầu việc '{criterion.Title}'. Ngày hiện tại là {TimeZoneInfo.ConvertTime(criterionDeadline, Vietnam):dd/MM/yyyy}.");
                }
                else if (a.EntityName == "Milestone" && byMilestone.TryGetValue(entityId, out var m)
                    && byProject.TryGetValue(m.ProjectId, out var p))
                {
                    var msg = a.Action switch {
                        "MILESTONE_PROPOSED" or "MILESTONE_PROPOSED_FROM_TEMPLATE" or "MILESTONE_PROPOSAL_RESUBMITTED"
                            => $"Sinh viên gửi đề xuất kế hoạch: {m.Title}.",
                        "MILESTONE_PROPOSAL_CONFIRMED" => $"Giảng viên đã xác nhận kế hoạch: {m.Title}.",
                        "MILESTONE_PROPOSAL_CHANGES_REQUESTED" => $"Giảng viên yêu cầu sửa kế hoạch: {m.Title}.",
                        "MILESTONE_CREATED" or "MILESTONE_CREATED_FROM_TEMPLATE" => $"Giảng viên đã tạo Milestone: {m.Title}.",
                        "MILESTONE_UPDATED" => $"Giảng viên đã cập nhật Milestone: {m.Title}.",
                        "MILESTONE_PROGRESS_UPDATED" => $"Sinh viên đã cập nhật tiến độ Milestone: {m.Title}.",
                        _ => null
                    };
                    if (msg is not null)
                    {
                        var to = a.Action is "MILESTONE_PROPOSAL_CONFIRMED" or "MILESTONE_PROPOSAL_CHANGES_REQUESTED" or
                            "MILESTONE_CREATED" or "MILESTONE_CREATED_FROM_TEMPLATE" or "MILESTONE_UPDATED"
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
