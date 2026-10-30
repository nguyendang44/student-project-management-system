using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using StudentProjects.Infrastructure.Persistence;

namespace StudentProjects.Api.Services;

/// <summary>
/// Resolves existing v0.8 notification event keys to internal, authorized deep links.
/// Works for historic notifications and does not require notification schema changes.
/// Missing/deleted objects or inaccessible Projects produce no link.
/// </summary>
public static class V08NotificationLinks
{
    public static async Task<Dictionary<string, string>> ResolveAsync(
        StudentProjectsDbContext db, ClaimsPrincipal user, Guid actor,
        IEnumerable<string> types, CancellationToken ct)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var keys = types.Distinct(StringComparer.Ordinal).ToArray();
        var milestoneIds = new HashSet<Guid>();
        var submissionIds = new HashSet<Guid>();
        var reviewIds = new HashSet<Guid>();
        var auditIds = new HashSet<Guid>();
        var parsed = new Dictionary<string, (string Kind, Guid EventId)>(StringComparer.Ordinal);

        foreach (var type in keys)
        {
            var fields = type.Split(':');
            if (fields.Length < 3 || fields[0] != "V08" || !Guid.TryParseExact(fields[2], "N", out var id))
                continue;
            var kind = fields[1];
            if (kind is "DUE" or "LATE") milestoneIds.Add(id);
            else if (kind == "SUB") submissionIds.Add(id);
            else if (kind == "REVIEW") reviewIds.Add(id);
            else if (kind is "PLAN" or "MEET" or "MEETOK") auditIds.Add(id);
            else continue;
            parsed[type] = (kind, id);
        }
        if (parsed.Count == 0) return result;

        var submissions = await db.MilestoneSubmissions.AsNoTracking()
            .Where(s => submissionIds.Contains(s.Id))
            .Select(s => new { s.Id, s.MilestoneId }).ToDictionaryAsync(s => s.Id, ct);
        foreach (var submission in submissions.Values) milestoneIds.Add(submission.MilestoneId);

        var reviews = await db.ProjectEvaluations.AsNoTracking()
            .Where(r => reviewIds.Contains(r.Id))
            .Select(r => new { r.Id, r.ProjectId, r.MilestoneId }).ToDictionaryAsync(r => r.Id, ct);
        foreach (var review in reviews.Values)
            if (review.MilestoneId.HasValue) milestoneIds.Add(review.MilestoneId.Value);

        var audits = await db.AuditEntries.AsNoTracking()
            .Where(a => auditIds.Contains(a.Id))
            .Select(a => new { a.Id, a.EntityName, a.EntityId }).ToDictionaryAsync(a => a.Id, ct);
        var meetingIds = new HashSet<Guid>();
        foreach (var audit in audits.Values)
        {
            if (!audit.EntityId.HasValue) continue;
            if (audit.EntityName == "Milestone") milestoneIds.Add(audit.EntityId.Value);
            if (audit.EntityName == "GuidanceSession") meetingIds.Add(audit.EntityId.Value);
        }

        var milestones = await db.Milestones.AsNoTracking()
            .Where(m => milestoneIds.Contains(m.Id))
            .Select(m => new { m.Id, m.ProjectId }).ToDictionaryAsync(m => m.Id, ct);
        var meetings = await db.GuidanceSessions.AsNoTracking()
            .Where(g => meetingIds.Contains(g.Id))
            .Select(g => new { g.Id, g.ProjectId }).ToDictionaryAsync(g => g.Id, ct);

        // A notification belongs to its recipient, but the target Project is checked again:
        // access may have changed since the event was created.
        var accessible = user.IsInRole("Admin")
            ? await db.Projects.AsNoTracking().Select(p => p.Id).ToHashSetAsync(ct)
            : await db.Projects.AsNoTracking()
                .Where(p => p.StudentUserId == actor || p.LecturerUserId == actor)
                .Select(p => p.Id).ToHashSetAsync(ct);

        foreach (var (type, entry) in parsed)
        {
            Guid projectId = Guid.Empty;
            Guid milestoneId = Guid.Empty;
            Guid meetingId = Guid.Empty;
            string page = "";
            if ((entry.Kind is "DUE" or "LATE") && milestones.TryGetValue(entry.EventId, out var direct))
            {
                projectId = direct.ProjectId;
                milestoneId = direct.Id;
                page = user.IsInRole("Student") ? "progress" : "milestones";
            }
            else if (entry.Kind == "SUB" && submissions.TryGetValue(entry.EventId, out var submission)
                && milestones.TryGetValue(submission.MilestoneId, out var submittedMilestone))
            {
                projectId = submittedMilestone.ProjectId;
                milestoneId = submittedMilestone.Id;
                page = "evaluations";
            }
            else if (entry.Kind == "REVIEW" && reviews.TryGetValue(entry.EventId, out var review))
            {
                // A project-level review without a Milestone cannot identify the
                // exact result page, so do not create a misleading deep link.
                if (!review.MilestoneId.HasValue ||
                    !milestones.TryGetValue(review.MilestoneId.Value, out var reviewed) ||
                    reviewed.ProjectId != review.ProjectId) continue;
                projectId = reviewed.ProjectId;
                milestoneId = reviewed.Id;
                page = "submissions";
            }
            else if ((entry.Kind is "PLAN" or "MEET" or "MEETOK")
                && audits.TryGetValue(entry.EventId, out var audit) && audit.EntityId.HasValue)
            {
                if (entry.Kind == "PLAN" && audit.EntityName == "Milestone"
                    && milestones.TryGetValue(audit.EntityId.Value, out var planned))
                {
                    projectId = planned.ProjectId;
                    milestoneId = planned.Id;
                    page = "milestones";
                }
                else if ((entry.Kind is "MEET" or "MEETOK") && audit.EntityName == "GuidanceSession"
                    && meetings.TryGetValue(audit.EntityId.Value, out var meeting))
                {
                    projectId = meeting.ProjectId;
                    meetingId = meeting.Id;
                    page = "guidance";
                }
            }
            if (projectId == Guid.Empty || !accessible.Contains(projectId) || page.Length == 0) continue;
            var link = $"/{page}?projectId={projectId:D}";
            if (milestoneId != Guid.Empty) link += $"&milestoneId={milestoneId:D}";
            if (meetingId != Guid.Empty) link += $"&entryId={meetingId:D}";
            result[type] = link;
        }
        return result;
    }
}
