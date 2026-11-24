using System.Data;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using StudentProjects.Api.Services;
using StudentProjects.Domain.Entities;
using StudentProjects.Domain.Enums;
using StudentProjects.Infrastructure.Persistence;

namespace StudentProjects.Api.Endpoints;

public static partial class ProjectProgressV07Endpoints
{
    private const string MilestoneAiEvidenceVersion = MilestoneAutoAssessment.EvidenceVersion;

    public sealed record CriterionInput(string? Title, string? AcceptanceCriteria, int Points, DateTimeOffset? DeadlineAt = null);
    public sealed record CriterionAiInput(Guid GitRunId);
    public sealed record CriterionReviewInput(bool? IsMet, Guid? ProposalId, string? Note);

    private static void MapMilestoneCriteria(RouteGroupBuilder api)
    {
        api.MapGet("/projects/{projectId:guid}/milestone-progress", AssessedProgress);
        api.MapGet("/milestones/{id:guid}/criteria", CriterionDetails);
        api.MapPost("/milestones/{id:guid}/criteria", AddCriterion);
        api.MapPost("/milestones/{id:guid}/criteria/suggest", SuggestCriterionPlan).RequireRateLimiting("analysis-start");
        api.MapPost("/milestones/{id:guid}/criteria/plan", SaveCriterionPlan);
        api.MapPut("/milestones/{id:guid}/criteria/{criterionId:guid}", EditCriterion);
        api.MapDelete("/milestones/{id:guid}/criteria/{criterionId:guid}", DeleteCriterion);
        api.MapPut("/milestones/{id:guid}/criteria/{criterionId:guid}/deadline", UpdateCriterionDeadline);
        api.MapPost("/milestones/{id:guid}/criteria/recheck", RecheckCriteria).RequireRateLimiting("analysis-start");
        api.MapPost("/milestones/{id:guid}/criteria/ai", ProposeCriteria).RequireRateLimiting("analysis-start");
        api.MapPost("/milestones/{id:guid}/criteria/{criterionId:guid}/review", ReviewCriterion);
    }

    private static async Task<IResult> AssessedProgress(Guid projectId, ClaimsPrincipal user,
        StudentProjectsDbContext db, CancellationToken ct)
    {
        var project = await db.Projects.AsNoTracking().SingleOrDefaultAsync(x => x.Id == projectId, ct);
        if (project is null) return Results.NotFound();
        if (!CanRead(project, user)) return Results.Forbid();

        var milestones = await db.Milestones.AsNoTracking().Where(x => x.ProjectId == projectId)
            .Select(x => new { x.Id, x.Status, x.ProposalStatus }).ToListAsync(ct);
        var milestoneIds = milestones.Select(x => x.Id).ToArray();
        var criteria = await db.MilestoneCriteria.AsNoTracking()
            .Where(x => !x.IsDeleted && milestoneIds.Contains(x.MilestoneId)).ToListAsync(ct);
        var criterionIds = criteria.Select(x => x.Id).ToArray();
        var records = await db.MilestoneCriterionAssessments.AsNoTracking().Where(x => criterionIds.Contains(x.CriterionId) &&
            (x.Source != "AI" || x.Note == MilestoneAiEvidenceVersion))
            .Select(x => new MilestoneCriterionAssessment
            {
                Id = x.Id, CriterionId = x.CriterionId, Source = x.Source, Outcome = x.Outcome, CreatedAt = x.CreatedAt
            }).ToListAsync(ct);
        var byMilestone = criteria.ToLookup(x => x.MilestoneId);
        var byCriterion = records.ToLookup(x => x.CriterionId);
        var rows = milestones.Select(m =>
        {
            var items = byMilestone[m.Id].ToArray();
            var progress = MilestoneCriterionProgress.Calculate(items, items.SelectMany(x => byCriterion[x.Id]));
            var legacyApproved = items.Length == 0 && m.Status == MilestoneStatus.APPROVED;
            return new { milestoneId = m.Id, confirmedPercent = legacyApproved ? 100 : progress.Confirmed,
                proposedPercent = progress.Proposed, legacyApproved, criteriaCount = items.Length,
                reviewedCount = progress.Reviewed, planned = m.ProposalStatus == "CONFIRMED" };
        }).ToArray();
        var planned = rows.Where(x => x.planned).ToArray();
        return Results.Ok(new
        {
            milestones = rows,
            deadlineWarning = ProgressDeadlinePolicy.Evaluate(project.StartAt, project.DeadlineAt,
                planned.Length == 0 ? null : Math.Round(planned.Average(x => (decimal)x.confirmedPercent), 1),
                DateTimeOffset.UtcNow, Active(project)),
            confirmedPercent = planned.Length == 0 ? (decimal?)null :
                Math.Round(planned.Average(x => (decimal)x.confirmedPercent), 1),
            proposedPercent = planned.Any(x => x.proposedPercent.HasValue)
                ? Math.Round(planned.Average(x => (decimal)(x.proposedPercent ?? x.confirmedPercent)), 1) : (decimal?)null,
            proposed = planned.Count(x => x.proposedPercent.HasValue),
            tracked = planned.Length,
            reviewed = planned.Count(x => x.reviewedCount > 0 || x.legacyApproved),
            completed = planned.Count(x => x.confirmedPercent == 100)
        });
    }

    private static async Task<(Milestone? Milestone, Project? Project)> CriterionScope(Guid id,
        StudentProjectsDbContext db, CancellationToken ct)
    {
        var milestone = await db.Milestones.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        var project = milestone is null ? null : await db.Projects.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == milestone.ProjectId, ct);
        return (milestone, project);
    }

    private static Task<Milestone?> LockCriterionMilestone(StudentProjectsDbContext db, Guid id, CancellationToken ct) =>
        db.Milestones.FromSqlInterpolated(
            $"SELECT * FROM dbo.Milestones WITH (UPDLOCK, HOLDLOCK) WHERE Id = {id}")
            .AsNoTracking().FirstOrDefaultAsync(ct);

    private static async Task<IResult> CriterionDetails(Guid id, ClaimsPrincipal user,
        StudentProjectsDbContext db, CancellationToken ct)
    {
        var (milestone, project) = await CriterionScope(id, db, ct);
        if (milestone is null || project is null) return Results.NotFound();
        if (!CanRead(project, user)) return Results.Forbid();
        var items = await db.MilestoneCriteria.AsNoTracking().Where(x => x.MilestoneId == id && !x.IsDeleted)
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(ct);
        var ids = items.Select(x => x.Id).ToArray();
        var history = await db.MilestoneCriterionAssessments.AsNoTracking().Where(x => ids.Contains(x.CriterionId) &&
            (x.Source != "AI" || (x.Note == MilestoneAiEvidenceVersion || x.Note == MilestoneAiEvidenceVersion + "-superseded")))
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).ToListAsync(ct);
        var reportIds = history.Where(x => x.GitProgressRunId.HasValue).Select(x => x.GitProgressRunId!.Value)
            .Distinct().ToArray();
        var reports = await db.GitProgressRuns.AsNoTracking().Where(x => reportIds.Contains(x.Id))
            .Select(x => new { x.Id, x.BaseSha, x.HeadSha }).ToDictionaryAsync(x => x.Id, ct);
        var byCriterion = history.ToLookup(x => x.CriterionId);
        var completedReports = await (from run in db.GitProgressRuns.AsNoTracking()
            join repository in db.Repositories.AsNoTracking() on run.RepositoryLinkId equals repository.Id
            where run.ProjectId == project.Id && run.Status == "COMPLETED"
            orderby run.CreatedAt descending
            select new { run.Id, run.BaseSha, run.HeadSha, run.CompletedAt, repository.RepositoryUrl })
            .Take(60).ToListAsync(ct);
        var progress = MilestoneCriterionProgress.Calculate(items, history);
        var totalPoints = items.Sum(c => c.Points);
        var check = await MilestoneAssessmentStatus.ReadAsync(db, id, project.Id, ct);
        return Results.Ok(new { check, confirmedPercent = progress.Confirmed, proposedPercent = progress.Proposed, reports = completedReports, criteria = items.Select(x => new
        {
            x.Id, x.Title, x.AcceptanceCriteria, x.Points, x.DeadlineAt,
            effectiveOutcome = MilestoneCriterionProgress.EffectiveOutcome(x, byCriterion[x.Id]),
            awardedPoints = MilestoneCriterionProgress.EffectiveOutcome(x, byCriterion[x.Id]) == "full" ? x.Points : 0,
            deadlineWarning = ProgressDeadlinePolicy.Evaluate(null, x.DeadlineAt,
                MilestoneCriterionProgress.EffectiveOutcome(x, byCriterion[x.Id]) == "full" ? 100m : 0m,
                DateTimeOffset.UtcNow, Active(project) && milestone.ProposalStatus == "CONFIRMED"),
            percentOfMilestone = totalPoints == 0 ? 0 : Math.Round(x.Points * 100m / totalPoints, 2),
            history = byCriterion[x.Id].Select(a => new
            {
                isCurrent = (!x.DefinitionChangedAt.HasValue || a.CreatedAt >= x.DefinitionChangedAt.Value) &&
                    (a.Source != "AI" || a.Note == MilestoneAiEvidenceVersion),
                a.Id, a.Source, a.Outcome, note = a.Source == "AI" ? null : a.Note, a.CreatedAt,
                git = a.GitProgressRunId is Guid run ? reports.GetValueOrDefault(run) : null,
                evidence = JsonSerializer.Deserialize<WordEvidence[]>(a.EvidenceJson,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web))
            })
        }) });
    }

    private static Task<IResult> AddCriterion(Guid id, CriterionInput? input, ClaimsPrincipal user,
        StudentProjectsDbContext db, CancellationToken ct) =>
        PersistCriterionPlan(id, input is null ? null : [input], user, db, ct);

    private static async Task<IResult> UpdateCriterionDeadline(Guid id, Guid criterionId, ProjectDeadlineInput? input,
        ClaimsPrincipal user, StudentProjectsDbContext db, CancellationToken ct)
    {
        var (milestone, project) = await CriterionScope(id, db, ct);
        if (milestone is null || project is null) return Results.NotFound();
        if (!CanWrite(project, user, "Lecturer")) return Results.Forbid();
        if (!Active(project))
            return Conflict("Không thể đổi hạn đầu việc trong đồ án đã đóng.");
        if (input?.DeadlineAt is null || input.DeadlineAt.Value.UtcDateTime.Year is < 2 or > 9998)
            return Invalid("Chọn ngày hoàn thành đầu việc hợp lệ.");
        await using var transaction = db.Database.IsSqlServer()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct) : null;
        if (transaction is not null)
        {
            var locked = await LockCriterionMilestone(db, id, ct);
            if (locked is null) return Results.NotFound();
        }
        var criterion = await db.MilestoneCriteria.SingleOrDefaultAsync(x => x.Id == criterionId && x.MilestoneId == id && !x.IsDeleted, ct);
        if (criterion is null) return Results.NotFound();
        var deadline = EndOfDeadlineDay(input.DeadlineAt.Value);
        if (criterion.DeadlineAt == deadline) return Results.Ok(new { criterion.Id, criterion.DeadlineAt });
        criterion.DeadlineAt = deadline;
        criterion.UpdatedAt = DateTimeOffset.UtcNow;
        Audit(db, Actor(user), "CRITERION_DEADLINE_UPDATED", "MilestoneCriterion", criterion.Id);
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return Results.Ok(new { criterion.Id, criterion.DeadlineAt });
    }

    private static async Task<IResult> RecheckCriteria(Guid id, ClaimsPrincipal user,
        StudentProjectsDbContext db, CancellationToken ct)
    {
        var (milestone, project) = await CriterionScope(id, db, ct);
        if (milestone is null || project is null) return Results.NotFound();
        if (!CanWrite(project, user, "Student") && !CanWrite(project, user, "Lecturer")) return Results.Forbid();
        if (!Active(project) || milestone.ProposalStatus != "CONFIRMED")
            return Conflict("Cần kế hoạch đã xác nhận và đồ án đang hoạt động.");
        if (!await db.MilestoneCriteria.AnyAsync(c => c.MilestoneId == id && !c.IsDeleted, ct))
            return Conflict("Milestone chưa có đầu việc để kiểm tra.");
        var report = await db.GitProgressRuns.AsNoTracking().Where(r => r.ProjectId == project.Id && r.Status == "COMPLETED")
            .OrderByDescending(r => r.CompletedAt).ThenByDescending(r => r.CreatedAt).FirstOrDefaultAsync(ct);
        if (report is null) return Conflict("Chưa có báo cáo Git hoàn tất. Hãy so sánh hai commit trước.");
        var pending = await db.AuditEntries.AsNoTracking().Where(a =>
            a.Action == MilestoneAssessmentRequests.Manual && a.EntityId == id &&
            !db.AuditEntries.Any(done => done.Action == MilestoneAssessmentRequests.Done && done.EntityId == a.Id))
            .OrderByDescending(a => a.CreatedAt).FirstOrDefaultAsync(ct);
        if (pending is not null) return Results.Accepted(value: new { pending.Id, queued = true });
        var request = new MilestoneAssessmentRequest { MilestoneId = id, RunId = report.Id };
        // The audit row retains the exact report selected at the time of this explicit request.
        var entry = MilestoneAssessmentRequests.CreateRecheck(request, Actor(user));
        db.AuditEntries.Add(entry);
        await db.SaveChangesAsync(ct);
        return Results.Accepted(value: new { entry.Id, report.BaseSha, report.HeadSha });
    }

    private static async Task<IResult> ProposeCriteria(Guid id, CriterionAiInput? input, ClaimsPrincipal user,
        StudentProjectsDbContext db, AiGitProgressClient ai, CancellationToken ct)
    {
        var (milestone, project) = await CriterionScope(id, db, ct);
        if (milestone is null || project is null) return Results.NotFound();
        if (!CanWrite(project, user, "Student") && !CanWrite(project, user, "Lecturer")) return Results.Forbid();
        if (!Active(project) || milestone.ProposalStatus != "CONFIRMED")
            return Conflict("Cần Milestone có kế hoạch đã xác nhận và đồ án đang hoạt động.");
        if (input is null || input.GitRunId == Guid.Empty) return Invalid("Chọn báo cáo Git đã hoàn tất.");
        var run = await db.GitProgressRuns.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == input.GitRunId && x.ProjectId == project.Id && x.Status == "COMPLETED", ct);
        if (run is null) return Invalid("Báo cáo Git phải đã hoàn tất và thuộc cùng đồ án.");
        try
        {
            if (!await db.MilestoneCriteria.AnyAsync(c => c.MilestoneId == id && !c.IsDeleted, ct))
                return Conflict("Giảng viên cần thêm đầu việc trước khi đối chiếu.");
            await MilestoneAutoAssessment.EvaluateAsync(db, ai, id, run, Actor(user), ct);
            return Results.Ok();
        }
        catch (InvalidDataException ex) { return Results.Json(new { message = ex.Message }, statusCode: 502); }
        catch (Exception ex) when (!ct.IsCancellationRequested &&
            ex is HttpRequestException or JsonException or OperationCanceledException)
        {
            return Results.Json(new { message = "Ollama chưa trả kết quả đánh giá hợp lệ. Vui lòng thử lại." }, statusCode: 502);
        }
    }

    private static async Task<IResult> ReviewCriterion(Guid id, Guid criterionId, CriterionReviewInput? input,
        ClaimsPrincipal user, StudentProjectsDbContext db, CancellationToken ct)
    {
        var (milestone, project) = await CriterionScope(id, db, ct);
        if (milestone is null || project is null) return Results.NotFound();
        if (!CanWrite(project, user, "Lecturer")) return Results.Forbid();
        if (!Active(project) || milestone.ProposalStatus != "CONFIRMED")
            return Conflict("Cần Milestone có kế hoạch đã xác nhận và đồ án đang hoạt động.");
        if (input?.IsMet is null || !await db.MilestoneCriteria.AnyAsync(x => x.Id == criterionId && x.MilestoneId == id && !x.IsDeleted, ct))
            return Invalid("Chọn đầu việc và kết quả xác nhận.");
        MilestoneCriterionAssessment? proposal = null;
        if (input.ProposalId is Guid proposalId)
        {
            proposal = await db.MilestoneCriterionAssessments.AsNoTracking().SingleOrDefaultAsync(
                x => x.Id == proposalId && x.CriterionId == criterionId && x.Source == "AI" &&
                    x.Note == MilestoneAiEvidenceVersion, ct);
            if (proposal is null) return Invalid("Đề xuất AI không thuộc đầu việc này.");
        }
        var note = Text(input.Note);
        if (note.Length > 1000 ||
            (note.Length == 0 && (input.IsMet == false || proposal?.Outcome != "full")))
            return Invalid("Ghi căn cứ xác nhận hoặc lý do chưa đạt (tối đa 1000 ký tự).");
        await using var transaction = db.Database.IsSqlServer()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct) : null;
        if (transaction is not null)
        {
            var locked = await LockCriterionMilestone(db, id, ct);
            if (locked?.ProposalStatus != "CONFIRMED")
                return Conflict("Kế hoạch Milestone đã thay đổi, vui lòng tải lại.");
        }
        var currentCriterion = await db.MilestoneCriteria.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == criterionId && c.MilestoneId == id && !c.IsDeleted, ct);
        if (currentCriterion is null) return Results.NotFound();
        if (proposal is not null && currentCriterion.DefinitionChangedAt.HasValue &&
            proposal.CreatedAt < currentCriterion.DefinitionChangedAt.Value)
            return Conflict("Nội dung đầu việc đã sửa. Hãy chạy lại AI hoặc xác nhận bằng căn cứ mới.");
        var review = new MilestoneCriterionAssessment
        {
            CriterionId = criterionId, ActorUserId = Actor(user), GitProgressRunId = proposal?.GitProgressRunId,
            Source = "REVIEW", Outcome = input.IsMet.Value ? "full" : "missing",
            Note = note.Length > 0 ? note : null, EvidenceJson = proposal?.EvidenceJson ?? "[]"
        };
        db.MilestoneCriterionAssessments.Add(review);
        var oldRecords = await db.MilestoneCriterionAssessments.AsNoTracking()
            .Where(a => a.CriterionId == criterionId && (a.Source != "AI" || a.Note == MilestoneAiEvidenceVersion)).ToListAsync(ct);
        var notification = MilestoneAutoAssessment.ReviewNotification(project, milestone, currentCriterion, review, oldRecords);
        if (notification is not null)
        {
            Audit(db, Actor(user), "CRITERION_REVIEW_UPDATED", "MilestoneCriterion", criterionId);
            db.Notifications.Add(notification);
        }
        await db.SaveChangesAsync(ct);
        await MilestoneAutoAssessment.SyncAsync(db, id, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return Results.Ok();
    }
}
