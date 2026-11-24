using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StudentProjects.Domain.Entities;
using StudentProjects.Domain.Enums;
using StudentProjects.Infrastructure.Persistence;

namespace StudentProjects.Api.Services;

/// <summary>Only validated Git evidence earns criterion weights; lecturer decisions take precedence.</summary>
public static class MilestoneAutoAssessment
{
    public const string EvidenceVersion = "milestone-evidence-v3";

    public static Notification? ReviewNotification(Project project, Milestone milestone, MilestoneCriterion criterion,
        MilestoneCriterionAssessment review, IEnumerable<MilestoneCriterionAssessment> history)
    {
        var records = history.ToArray();
        var before = MilestoneCriterionProgress.EffectiveOutcome(criterion, records);
        var lastNote = records.Where(a => a.Source == "REVIEW" && a.CriterionId == criterion.Id &&
                (!criterion.DefinitionChangedAt.HasValue || a.CreatedAt >= criterion.DefinitionChangedAt.Value))
            .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id).FirstOrDefault()?.Note;
        if (before == review.Outcome && lastNote == review.Note) return null;
        return new Notification
        {
            RecipientUserId = project.StudentUserId, Type = $"V08:CREVIEW:{criterion.Id:N}:{review.Id:N}",
            Message = $"Giảng viên đã sửa kết quả đầu việc '{criterion.Title}' trong Milestone '{milestone.Title}': " +
                (review.Outcome == "full" ? "đạt, được tính trọng số." : "cần bổ sung, chưa được tính trọng số.") +
                (review.Note is null ? "" : $" Ghi chú: {review.Note}")
        };
    }

    public static async Task SyncAsync(StudentProjectsDbContext db, Guid milestoneId, CancellationToken ct)
    {
        var milestone = await db.Milestones.SingleAsync(m => m.Id == milestoneId, ct);
        if (milestone.ProposalStatus != "CONFIRMED") return;
        var criteria = await db.MilestoneCriteria.AsNoTracking()
            .Where(c => c.MilestoneId == milestoneId && !c.IsDeleted).ToListAsync(ct);
        if (criteria.Count == 0)
        {
            milestone.PercentComplete = 0;
            milestone.Status = MilestoneStatus.PENDING;
            milestone.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return;
        }
        var ids = criteria.Select(c => c.Id).ToArray();
        var history = await db.MilestoneCriterionAssessments.AsNoTracking()
            .Where(a => ids.Contains(a.CriterionId) && (a.Source != "AI" || a.Note == EvidenceVersion)).ToListAsync(ct);
        var score = MilestoneCriterionProgress.Calculate(criteria, history);
        var status = score.Confirmed == 100 ? MilestoneStatus.APPROVED :
            history.Any(a => a.Source == "REVIEW" && criteria.Any(c => c.Id == a.CriterionId &&
                MilestoneCriterionProgress.EffectiveOutcome(c, history) != "full" &&
                (!c.DefinitionChangedAt.HasValue || a.CreatedAt >= c.DefinitionChangedAt.Value)))
                ? MilestoneStatus.REVISION_REQUIRED :
            history.Count > 0 ? MilestoneStatus.IN_PROGRESS : MilestoneStatus.PENDING;
        // The existing database field is integer; exact normalized percentages remain in the criteria API.
        var percent = score.Confirmed == 100 ? 100 : (int)Math.Floor(score.Confirmed);
        if (milestone.PercentComplete != percent || milestone.Status != status)
        {
            milestone.PercentComplete = percent;
            milestone.Status = status;
            milestone.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    public static async Task EvaluateAsync(StudentProjectsDbContext db, AiGitProgressClient ai,
        Guid milestoneId, GitProgressRun run, Guid actor, CancellationToken ct, bool force = false, IReadOnlyList<GitProgressRun>? reports = null)
    {
        var criteria = await db.MilestoneCriteria.AsNoTracking()
            .Where(c => c.MilestoneId == milestoneId && !c.IsDeleted).OrderBy(c => c.CreatedAt).ThenBy(c => c.Id).ToListAsync(ct);
        var ids = criteria.Select(c => c.Id).ToArray();
        var previous = await db.MilestoneCriterionAssessments.AsNoTracking()
            .Where(a => ids.Contains(a.CriterionId) && a.Source == "AI" && a.Note == EvidenceVersion && a.GitProgressRunId == run.Id)
            .ToListAsync(ct);
        if (!force) criteria.RemoveAll(c => previous.Any(a => a.CriterionId == c.Id &&
            (!c.DefinitionChangedAt.HasValue || a.CreatedAt >= c.DefinitionChangedAt.Value)));
        WordComparisonResult? result = null;
        if (criteria.Count > 0)
        {
            var evidence = (reports ?? [run]).SelectMany(source => GitProgressWordComparison.ReadEvidence(
                source.EvidenceJson, complete: true, source: source)).Select((line, i) => line with { Id = i + 1 }).ToArray();
            if (evidence.Length == 0) throw new InvalidDataException("Báo cáo Git chưa có dòng bằng chứng để đối chiếu.");
            var claims = criteria.Select((c, i) => new WordReportLine(i + 1,
                $"{c.Title}. Điều kiện đạt: {c.AcceptanceCriteria}")).ToArray();
            result = await ai.CompareMilestoneAsync(claims, evidence, ct);
        }
        await using var transaction = db.Database.IsSqlServer()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct) : null;
        var milestone = transaction is null
            ? await db.Milestones.SingleOrDefaultAsync(m => m.Id == milestoneId, ct)
            : await db.Milestones.FromSqlInterpolated(
                $"SELECT * FROM dbo.Milestones WITH (UPDLOCK, HOLDLOCK) WHERE Id = {milestoneId}").SingleOrDefaultAsync(ct);
        if (milestone is null || milestone.ProjectId != run.ProjectId || milestone.ProposalStatus != "CONFIRMED")
            throw new InvalidDataException("Kế hoạch Milestone đã thay đổi trong khi đối chiếu.");
        var project = await db.Projects.AsNoTracking().SingleAsync(p => p.Id == run.ProjectId, ct);
        if (project.Status is ProjectStatus.CANCELLED or ProjectStatus.COMPLETED)
            throw new InvalidDataException("Đồ án đã đóng.");
        var definitions = await db.MilestoneCriteria.AsNoTracking().Where(c => ids.Contains(c.Id)).ToListAsync(ct);
        if (criteria.Any(c => !definitions.Any(d => d.Id == c.Id && !d.IsDeleted && d.DefinitionChangedAt == c.DefinitionChangedAt)))
            throw new InvalidDataException("Đầu việc đã sửa, vui lòng thử lại.");
        previous = await db.MilestoneCriterionAssessments.AsNoTracking()
            .Where(a => ids.Contains(a.CriterionId) && a.Source == "AI" && a.Note == EvidenceVersion && a.GitProgressRunId == run.Id)
            .ToListAsync(ct);
        if (force && result is not null)
        {
            var replacedIds = criteria.Select(c => c.Id).ToArray();
            var superseded = await db.MilestoneCriterionAssessments
                .Where(a => replacedIds.Contains(a.CriterionId) && a.Source == "AI" &&
                    (a.Note == EvidenceVersion || a.Note == "milestone-evidence-v2")).ToListAsync(ct);
            foreach (var old in superseded) old.Note = EvidenceVersion + "-superseded";
        }
        if (result is not null)
            foreach (var row in result.Items)
            {
                var criterion = criteria[row.Id - 1];
                if (!force && previous.Any(a => a.CriterionId == criterion.Id &&
                    (!criterion.DefinitionChangedAt.HasValue || a.CreatedAt >= criterion.DefinitionChangedAt.Value))) continue;
                db.MilestoneCriterionAssessments.Add(new MilestoneCriterionAssessment
                {
                    CriterionId = criterion.Id, ActorUserId = actor, GitProgressRunId = run.Id,
                    Source = "AI", Outcome = row.Status, Note = EvidenceVersion,
                    EvidenceJson = JsonSerializer.Serialize(row.Evidence)
                });
            }
        await db.SaveChangesAsync(ct);
        await SyncAsync(db, milestoneId, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
    }
}

