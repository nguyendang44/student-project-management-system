using System.Data;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using StudentProjects.Api.Services;
using StudentProjects.Domain.Entities;
using StudentProjects.Domain.Enums;
using StudentProjects.Infrastructure.Persistence;

namespace StudentProjects.Api.Endpoints;

public static partial class V08Endpoints
{
    public sealed record GuidanceInput(DateTimeOffset MeetingAt, string? Summary, string? Feedback, string? NextActions,
        Guid? MilestoneId = null, Guid? GitProgressRunId = null, string? Questions = null, DateTimeOffset? ExpectedUpdatedAt = null);
    public sealed record GuidanceReviewInput(bool Confirm, string? Feedback, string? NextActions, DateTimeOffset ExpectedUpdatedAt);
    public sealed record GuidanceTaskInput(Guid MilestoneId, string? Title, string? AcceptanceCriteria, int Points,
        DateTimeOffset? DeadlineAt, DateTimeOffset ExpectedUpdatedAt);
    private static IResult GuidanceConflict(string message) => Results.Conflict(new { code = "INVALID_STATE", message });
    private static bool GuidanceActive(Project project) => project.Status is not (ProjectStatus.CANCELLED or ProjectStatus.COMPLETED);
    private static bool ReportValid(GuidanceInput? input) => input is not null &&
        GuidanceWorkflow.ValidReport(input.MeetingAt, input.Summary, input.Questions, DateTimeOffset.UtcNow) &&
        (input.Feedback?.Length ?? 0) <= 4000 && (input.NextActions?.Length ?? 0) <= 4000;
    private static bool HasLecturerContent(GuidanceInput input) =>
        !string.IsNullOrWhiteSpace(input.Feedback) || !string.IsNullOrWhiteSpace(input.NextActions);

    private static async Task<bool> GuidanceReferencesValid(StudentProjectsDbContext db, Guid projectId, GuidanceInput input, CancellationToken ct) =>
        (!input.MilestoneId.HasValue || await db.Milestones.AnyAsync(m => m.Id == input.MilestoneId &&
            m.ProjectId == projectId && m.ProposalStatus == "CONFIRMED", ct)) &&
        (!input.GitProgressRunId.HasValue || await db.GitProgressRuns.AnyAsync(r => r.Id == input.GitProgressRunId &&
            r.ProjectId == projectId && r.Status == "COMPLETED", ct));

    private static async Task<GuidanceSession?> LockGuidance(StudentProjectsDbContext db, Guid id, CancellationToken ct) =>
        db.Database.IsSqlServer()
            ? await db.GuidanceSessions.FromSqlInterpolated($"SELECT * FROM dbo.GuidanceSessions WITH (UPDLOCK,HOLDLOCK) WHERE Id = {id}").SingleOrDefaultAsync(ct)
            : await db.GuidanceSessions.SingleOrDefaultAsync(g => g.Id == id, ct);

    private static void GuidanceAudit(StudentProjectsDbContext db, Guid actor, Guid id, string action) =>
        db.AuditEntries.Add(new AuditEntry { ActorUserId = actor, Action = action, EntityName = "GuidanceSession", EntityId = id });

    private static async Task<IResult> GuidanceContext(Guid projectId, ClaimsPrincipal user, StudentProjectsDbContext db, CancellationToken ct)
    {
        var project = await db.Projects.AsNoTracking().SingleOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null) return Results.NotFound();
        if (!CanRead(project, user)) return Results.Forbid();
        var milestones = await db.Milestones.AsNoTracking().Where(m => m.ProjectId == projectId && m.ProposalStatus == "CONFIRMED")
            .OrderBy(m => m.CreatedAt).Select(m => new { m.Id, m.Title, m.Status }).ToListAsync(ct);
        var reports = await db.GitProgressRuns.AsNoTracking().Where(r => r.ProjectId == projectId && r.Status == "COMPLETED")
            .OrderByDescending(r => r.CreatedAt).Take(100).Select(r => new GitProgressRun { Id = r.Id, ProjectId = r.ProjectId,
                RepositoryLinkId = r.RepositoryLinkId, BaseSha = r.BaseSha, HeadSha = r.HeadSha, CreatedAt = r.CreatedAt,
                CompletedAt = r.CompletedAt, ModelName = r.ModelName }).ToListAsync(ct);
        return Results.Ok(new { Milestones = milestones, Reports = GitProgressRunHistory.Latest(reports).Select(r =>
            new { r.Id, r.BaseSha, r.HeadSha, r.ModelName, r.CompletedAt }), CanWrite = CanWrite(project, user) && GuidanceActive(project) });
    }

    private static async Task<IResult> Guidance(Guid projectId, ClaimsPrincipal user, StudentProjectsDbContext db, CancellationToken ct)
    {
        var project = await db.Projects.AsNoTracking().SingleOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null) return Results.NotFound();
        if (!CanRead(project, user)) return Results.Forbid();
        var rows = await db.GuidanceSessions.AsNoTracking().Where(g => g.ProjectId == projectId)
            .OrderByDescending(g => g.MeetingAt).ThenByDescending(g => g.CreatedAt).Take(200).ToListAsync(ct);
        var milestoneIds = rows.Where(g => g.MilestoneId.HasValue).Select(g => g.MilestoneId!.Value).Distinct().ToArray();
        var titles = await db.Milestones.AsNoTracking().Where(m => milestoneIds.Contains(m.Id) && m.ProjectId == projectId)
            .ToDictionaryAsync(m => m.Id, m => m.Title, ct);
        var runIds = rows.Where(g => g.GitProgressRunId.HasValue).Select(g => g.GitProgressRunId!.Value).Distinct().ToArray();
        var runs = await db.GitProgressRuns.AsNoTracking().Where(r => runIds.Contains(r.Id) && r.ProjectId == projectId)
            .Select(r => new { r.Id, r.BaseSha, r.HeadSha }).ToDictionaryAsync(r => r.Id, ct);
        var taskIds = rows.Where(g => g.NextCriterionId.HasValue).Select(g => g.NextCriterionId!.Value).Distinct().ToArray();
        var tasks = await db.MilestoneCriteria.AsNoTracking().Where(c => taskIds.Contains(c.Id) && db.Milestones.Any(m => m.Id == c.MilestoneId && m.ProjectId == projectId)).ToListAsync(ct);
        var records = await db.MilestoneCriterionAssessments.AsNoTracking().Where(a => taskIds.Contains(a.CriterionId) &&
            (a.Source != "AI" || a.Note == MilestoneAutoAssessment.EvidenceVersion)).ToListAsync(ct);
        return Results.Ok(rows.Select(g => new {
            g.Id, g.ProjectId, g.CreatedByUserId, g.MeetingAt, g.Summary, g.Questions, g.Feedback, g.NextActions,
            g.LecturerConfirmedAt, g.CreatedAt, g.UpdatedAt, g.MilestoneId, MilestoneTitle = g.MilestoneId.HasValue ? titles.GetValueOrDefault(g.MilestoneId.Value) : null,
            g.GitProgressRunId, Git = g.GitProgressRunId.HasValue ? runs.GetValueOrDefault(g.GitProgressRunId.Value) : null,
            Status = GuidanceWorkflow.Status(g), NextTask = tasks.Where(t => t.Id == g.NextCriterionId).Select(t => new {
                t.Id, t.MilestoneId, t.Title, t.IsDeleted, t.Points,
                Outcome = MilestoneCriterionProgress.EffectiveOutcome(t, records) }).FirstOrDefault()
        }));
    }

    private static async Task<IResult> CreateGuidance(Guid projectId, GuidanceInput? input, ClaimsPrincipal user,
        StudentProjectsDbContext db, CancellationToken ct)
    {
        var project = await db.Projects.SingleOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null) return Results.NotFound();
        if (!CanWrite(project, user)) return Results.Forbid();
        if (!GuidanceActive(project)) return GuidanceConflict("Đồ án đã đóng, không thể thêm nhật ký.");
        if (!ReportValid(input)) return Bad("Ngày gặp hoặc nội dung không hợp lệ; tối đa 4000 ký tự mỗi trường.");
        if (user.IsInRole("Student") && HasLecturerContent(input!))
            return Bad("Sinh viên chỉ ghi báo cáo và câu hỏi; nhận xét và việc giao tiếp do giảng viên nhập.");
        if (!await GuidanceReferencesValid(db, projectId, input!, ct)) return Bad("Milestone hoặc báo cáo Git không thuộc đồ án hoặc chưa sẵn sàng.");
        var entry = new GuidanceSession { ProjectId = projectId, CreatedByUserId = Actor(user), MeetingAt = input!.MeetingAt,
            Summary = input.Summary!.Trim(), Questions = (input.Questions ?? "").Trim(), MilestoneId = input.MilestoneId,
            GitProgressRunId = input.GitProgressRunId, Feedback = (input.Feedback ?? "").Trim(), NextActions = (input.NextActions ?? "").Trim() };
        db.GuidanceSessions.Add(entry);
        GuidanceAudit(db, Actor(user), entry.Id, "GUIDANCE_LOG_CREATED");
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/v1/v08/projects/{projectId}/guidance", new { entry.Id });
    }

    private static async Task<IResult> UpdateGuidance(Guid id, GuidanceInput? input, ClaimsPrincipal user,
        StudentProjectsDbContext db, CancellationToken ct)
    {
        await using var transaction = db.Database.IsSqlServer() ? await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct) : null;
        var entry = await LockGuidance(db, id, ct);
        if (entry is null) return Results.NotFound();
        var project = await db.Projects.AsNoTracking().SingleOrDefaultAsync(p => p.Id == entry.ProjectId, ct);
        if (project is null) return Results.NotFound();
        if (!user.IsInRole("Student") || !CanWrite(project, user) || entry.CreatedByUserId != Actor(user)) return Results.Forbid();
        if (!GuidanceActive(project) || !GuidanceWorkflow.CanResubmit(entry, Actor(user))) return GuidanceConflict("Nhật ký đã xác nhận hoặc đồ án đã đóng.");
        if (!ReportValid(input) || HasLecturerContent(input!)) return Bad("Chỉ được sửa báo cáo và câu hỏi của sinh viên; mỗi trường tối đa 4000 ký tự.");
        if (input!.ExpectedUpdatedAt != entry.UpdatedAt) return GuidanceConflict("Nhật ký vừa thay đổi. Hãy tải lại trước khi gửi.");
        if (!await GuidanceReferencesValid(db, entry.ProjectId, input, ct)) return Bad("Liên kết Milestone/báo cáo Git không hợp lệ.");
        entry.MeetingAt = input.MeetingAt; entry.Summary = input.Summary!.Trim(); entry.Questions = (input.Questions ?? "").Trim();
        entry.MilestoneId = input.MilestoneId; entry.GitProgressRunId = input.GitProgressRunId;
        GuidanceWorkflow.Resubmit(entry, Actor(user), DateTimeOffset.UtcNow);
        GuidanceAudit(db, Actor(user), entry.Id, "GUIDANCE_LOG_RESUBMITTED");
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return GuidanceConflict("Nhật ký vừa thay đổi. Hãy tải lại."); }
        if (transaction is not null) await transaction.CommitAsync(ct);
        return Results.Ok(new { entry.Id });
    }

    private static Task<IResult> ReviewGuidance(Guid id, GuidanceReviewInput? input, ClaimsPrincipal user,
        StudentProjectsDbContext db, CancellationToken ct) => SaveGuidanceReview(id, input, false, user, db, ct);
    private static Task<IResult> ConfirmGuidance(Guid id, ClaimsPrincipal user, StudentProjectsDbContext db, CancellationToken ct) =>
        SaveGuidanceReview(id, null, true, user, db, ct);

    private static async Task<IResult> SaveGuidanceReview(Guid id, GuidanceReviewInput? input, bool legacyConfirm, ClaimsPrincipal user,
        StudentProjectsDbContext db, CancellationToken ct)
    {
        await using var transaction = db.Database.IsSqlServer() ? await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct) : null;
        var entry = await LockGuidance(db, id, ct);
        if (entry is null) return Results.NotFound();
        var project = await db.Projects.AsNoTracking().SingleOrDefaultAsync(p => p.Id == entry.ProjectId, ct);
        if (project is null) return Results.NotFound();
        if (!user.IsInRole("Lecturer") || !CanWrite(project, user)) return Results.Forbid();
        if (!GuidanceActive(project) || GuidanceWorkflow.Status(entry) == "CONFIRMED") return GuidanceConflict("Nhật ký đã xác nhận hoặc đồ án đã đóng.");
        if (!legacyConfirm && (input is null || input.ExpectedUpdatedAt != entry.UpdatedAt)) return GuidanceConflict("Nhật ký vừa thay đổi. Hãy tải lại trước khi nhận xét.");
        try { GuidanceWorkflow.Review(entry, legacyConfirm || input!.Confirm, legacyConfirm ? entry.Feedback : input!.Feedback ?? "",
            legacyConfirm ? entry.NextActions : input!.NextActions ?? "", DateTimeOffset.UtcNow); }
        catch (ArgumentException ex) { return Bad(ex.Message); }
        GuidanceAudit(db, Actor(user), entry.Id, entry.LecturerConfirmedAt.HasValue ? "GUIDANCE_LOG_CONFIRMED" : "GUIDANCE_LOG_CHANGES_REQUESTED");
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return GuidanceConflict("Nhật ký vừa thay đổi. Hãy tải lại."); }
        if (transaction is not null) await transaction.CommitAsync(ct);
        return Results.Ok(new { entry.Id, entry.LecturerConfirmedAt, Status = GuidanceWorkflow.Status(entry) });
    }

    private static async Task<IResult> CreateGuidanceTask(Guid id, GuidanceTaskInput? input, ClaimsPrincipal user,
        StudentProjectsDbContext db, CancellationToken ct)
    {
        await using var transaction = db.Database.IsSqlServer() ? await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct) : null;
        var entry = await LockGuidance(db, id, ct);
        if (entry is null) return Results.NotFound();
        var project = await db.Projects.AsNoTracking().SingleOrDefaultAsync(p => p.Id == entry.ProjectId, ct);
        if (project is null) return Results.NotFound();
        if (!user.IsInRole("Lecturer") || !CanWrite(project, user)) return Results.Forbid();
        if (!GuidanceActive(project) || GuidanceWorkflow.Status(entry) != "CONFIRMED" || string.IsNullOrWhiteSpace(entry.NextActions))
            return GuidanceConflict("Cần nhật ký đã xác nhận và có công việc tiếp theo.");
        if (entry.NextCriterionId.HasValue) return GuidanceConflict("Nhật ký này đã được thêm thành đầu việc; xem liên kết đầu việc đã tạo.");
        if (input is null || string.IsNullOrWhiteSpace(input.Title) || input.Title.Trim().Length > 200 ||
            string.IsNullOrWhiteSpace(input.AcceptanceCriteria) || input.AcceptanceCriteria.Trim().Length > 250 || input.Points is < 1 or > 100 ||
            (input.DeadlineAt.HasValue && input.DeadlineAt.Value.UtcDateTime.Year is < 2 or > 9998)) return Bad("Nhập tên (tối đa 200), điều kiện đạt (tối đa 250), trọng số 1–100 và deadline hợp lệ.");
        if (input.ExpectedUpdatedAt != entry.UpdatedAt) return GuidanceConflict("Nhật ký vừa thay đổi. Hãy tải lại.");
        var milestone = db.Database.IsSqlServer()
            ? await db.Milestones.FromSqlInterpolated($"SELECT * FROM dbo.Milestones WITH (UPDLOCK,HOLDLOCK) WHERE Id = {input.MilestoneId}").SingleOrDefaultAsync(ct)
            : await db.Milestones.SingleOrDefaultAsync(m => m.Id == input.MilestoneId, ct);
        if (milestone is null || milestone.ProjectId != project.Id || milestone.ProposalStatus != "CONFIRMED") return Bad("Chọn Milestone đã xác nhận thuộc đúng đồ án.");
        if (await db.MilestoneCriteria.CountAsync(c => c.MilestoneId == milestone.Id && !c.IsDeleted, ct) >= 20)
            return GuidanceConflict("Milestone đã có tối đa 20 đầu việc.");
        var deadline = input.DeadlineAt?.ToOffset(TimeSpan.FromHours(7)).Date;
        var criterion = new MilestoneCriterion { MilestoneId = milestone.Id, Title = input.Title.Trim(),
            AcceptanceCriteria = input.AcceptanceCriteria.Trim(), Points = input.Points,
            DeadlineAt = deadline.HasValue ? new DateTimeOffset(deadline.Value.AddDays(1).AddTicks(-1), TimeSpan.FromHours(7)) : null };
        db.MilestoneCriteria.Add(criterion); entry.NextCriterionId = criterion.Id; entry.UpdatedAt = DateTimeOffset.UtcNow;
        GuidanceAudit(db, Actor(user), entry.Id, "GUIDANCE_TASK_ADDED");
        try {
            await db.SaveChangesAsync(ct);
            await MilestoneAutoAssessment.SyncAsync(db, milestone.Id, ct);
        }
        catch (DbUpdateConcurrencyException) { return GuidanceConflict("Nhật ký vừa thay đổi. Hãy tải lại."); }
        if (transaction is not null) await transaction.CommitAsync(ct);
        return Results.Created($"/api/v1/v07/milestones/{milestone.Id}/criteria", new { criterion.Id, criterion.MilestoneId });
    }
}
