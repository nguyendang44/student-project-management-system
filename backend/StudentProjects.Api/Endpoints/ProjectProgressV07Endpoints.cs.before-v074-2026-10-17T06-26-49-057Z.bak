using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using StudentProjects.Domain.Entities;
using StudentProjects.Domain.Enums;
using StudentProjects.Infrastructure.Persistence;

namespace StudentProjects.Api.Endpoints;

/// <summary>
/// v0.7: scoped Project milestones, progress history, submissions and lecturer reviews.
/// Dedicated /v07 namespace avoids intercepting legacy skeleton endpoints.
/// Authorization is enforced on every read/write against the Project owner relationship.
/// </summary>
public static class ProjectProgressV07Endpoints
{
    public static void MapProjectProgressV07Endpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1/v07").RequireAuthorization(p => p.RequireRole("Student", "Lecturer", "Admin"));
        api.MapGet("/projects", Projects);
        api.MapGet("/projects/{projectId:guid}/milestones", Milestones);
        api.MapPost("/projects/{projectId:guid}/milestones", CreateMilestone);
        api.MapPut("/milestones/{id:guid}", UpdateMilestone);
        api.MapGet("/milestones/{id:guid}/history", History);
        api.MapPost("/milestones/{id:guid}/progress", UpdateProgress);
        api.MapPost("/milestones/{id:guid}/submit", Submit);
        api.MapPost("/milestones/{id:guid}/review", Review);
        api.MapPost("/milestones/{id:guid}/proposal-review", ReviewProposal);
    }

    private static Guid Actor(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
    private static string Role(ClaimsPrincipal user) =>
        user.IsInRole("Admin") ? "Admin" : user.IsInRole("Lecturer") ? "Lecturer" :
        user.IsInRole("Student") ? "Student" : "";
    private static bool CanRead(Project project, ClaimsPrincipal user) =>
        Role(user) == "Admin" ||
        (Role(user) == "Student" && project.StudentUserId == Actor(user)) ||
        (Role(user) == "Lecturer" && project.LecturerUserId == Actor(user));
    private static bool CanWrite(Project p, ClaimsPrincipal user, string role) =>
        Role(user) == role && (role == "Student" ? p.StudentUserId : p.LecturerUserId) == Actor(user);
    private static bool Active(Project p) => p.Status != ProjectStatus.COMPLETED && p.Status != ProjectStatus.CANCELLED;
    private static IResult Invalid(string message) => Results.BadRequest(new { code = "INVALID_DATA", message });
    private static IResult Conflict(string message) => Results.Conflict(new { code = "INVALID_STATE", message });
    private static string Text(string? value) => (value ?? "").Trim();

    private static async Task<IResult> Projects(ClaimsPrincipal principal, StudentProjectsDbContext db, CancellationToken ct)
    {
        var who = Actor(principal);
        if (who == Guid.Empty) return Results.Unauthorized();
        var role = Role(principal);
        var query = db.Projects.AsNoTracking();
        if (role == "Student") query = query.Where(p => p.StudentUserId == who);
        else if (role == "Lecturer") query = query.Where(p => p.LecturerUserId == who);
        else if (role != "Admin") return Results.Forbid();
        var projects = await query.OrderByDescending(p => p.CreatedAt).ToListAsync(ct);
        var topicIds = projects.Select(p => p.TopicId).Distinct().ToArray();
        var studentIds = projects.Select(p => p.StudentUserId).Distinct().ToArray();
        var lecturerIds = projects.Select(p => p.LecturerUserId).Distinct().ToArray();
        var ids = studentIds.Concat(lecturerIds).Distinct().ToArray();
        var topics = await db.Topics.AsNoTracking().Where(t => topicIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Title, ct);
        var people = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        var projectIds = projects.Select(p => p.Id).ToArray();
        var milestones = await db.Milestones.AsNoTracking().Where(m => projectIds.Contains(m.ProjectId))
            .Select(m => new { m.ProjectId, m.Status, m.PercentComplete, m.ProposalStatus }).ToListAsync(ct);
        var items = projects.Select(p => new
        {
            p.Id, p.TopicId, TopicTitle = topics.GetValueOrDefault(p.TopicId, "Đề tài"),
            p.StudentUserId, StudentName = people.GetValueOrDefault(p.StudentUserId, "Sinh viên"),
            p.LecturerUserId, LecturerName = people.GetValueOrDefault(p.LecturerUserId, "Giảng viên"),
            Status = p.Status.ToString(), p.Description,
            MilestoneCount = milestones.Count(m => m.ProjectId == p.Id),
            ApprovedCount = milestones.Count(m => m.ProjectId == p.Id && m.Status == MilestoneStatus.APPROVED),
            PercentComplete = milestones.Any(m => m.ProjectId == p.Id && m.ProposalStatus == "CONFIRMED")
                ? (int)Math.Round(milestones.Where(m => m.ProjectId == p.Id && m.ProposalStatus == "CONFIRMED")
                    .Average(m => (double)m.PercentComplete)) : 0
        });
        return Results.Ok(items);
    }

    private static async Task<IResult> Milestones(Guid projectId, ClaimsPrincipal principal,
        StudentProjectsDbContext db, CancellationToken ct)
    {
        var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null) return Results.NotFound();
        if (!CanRead(project, principal)) return Results.Forbid();
        var records = await db.Milestones.AsNoTracking().Where(m => m.ProjectId == projectId)
            .OrderBy(m => m.DeadlineAt).ThenBy(m => m.CreatedAt).ToListAsync(ct);
        var now = DateTimeOffset.UtcNow;
        return Results.Ok(records.Select(m => new
        {
            m.Id, m.ProjectId, m.Title, m.Description, m.StartAt, m.DeadlineAt,
            m.ExpectedResult, m.PercentComplete, Status = m.Status.ToString(),
            m.ProposedByStudent, m.ProposalStatus, m.ProposalFeedback,
            IsOverdue = m.ProposalStatus == "CONFIRMED" && m.DeadlineAt < now && m.Status != MilestoneStatus.APPROVED,
            m.CreatedAt, m.UpdatedAt
        }));
    }

    public sealed record SaveMilestone(string? Title, string? Description,
        DateTimeOffset StartAt, DateTimeOffset DeadlineAt, string? ExpectedResult);

    private static string? Validate(SaveMilestone? req)
    {
        if (req is null || Text(req.Title).Length is < 1 or > 300) return "Tên Milestone phải dài từ 1 đến 300 ký tự.";
        if (Text(req.Description).Length > 4000 || Text(req.ExpectedResult).Length > 2000)
            return "Nội dung mô tả hoặc kết quả dự kiến quá dài.";
        if (req.StartAt == default || req.DeadlineAt == default || req.DeadlineAt < req.StartAt)
            return "Deadline phải bằng hoặc sau ngày bắt đầu.";
        return null;
    }

    private static async Task<IResult> CreateMilestone(Guid projectId, SaveMilestone? req,
        ClaimsPrincipal principal, StudentProjectsDbContext db, CancellationToken ct)
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null) return Results.NotFound();
        var creator = Role(principal);
        if (creator is not ("Lecturer" or "Student") || !CanWrite(project, principal, creator))
            return Results.Forbid();
        if (!Active(project)) return Conflict("Project đã kết thúc, không thể thêm Milestone.");
        var error = Validate(req); if (error != null) return Invalid(error);
        var item = new Milestone
        {
            ProjectId = projectId, Title = Text(req!.Title), Description = Text(req.Description),
            StartAt = req.StartAt, DeadlineAt = req.DeadlineAt, ExpectedResult = Text(req.ExpectedResult),
            Status = MilestoneStatus.PENDING, PercentComplete = 0,
            ProposedByStudent = creator == "Student",
            ProposalStatus = creator == "Student" ? "PENDING_REVIEW" : "CONFIRMED"
        };
        db.Milestones.Add(item);
        Audit(db, Actor(principal), creator == "Student" ? "MILESTONE_PROPOSED" : "MILESTONE_CREATED", "Milestone", item.Id);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/v1/v07/projects/{projectId}/milestones", new { item.Id });
    }

    private static async Task<IResult> UpdateMilestone(Guid id, SaveMilestone? req,
        ClaimsPrincipal principal, StudentProjectsDbContext db, CancellationToken ct)
    {
        var item = await db.Milestones.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (item is null) return Results.NotFound();
        var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == item.ProjectId, ct);
        if (project is null) return Results.NotFound();
        var byLecturer = CanWrite(project, principal, "Lecturer");
        var studentRevision = CanWrite(project, principal, "Student") && item.ProposedByStudent
            && item.ProposalStatus == "CHANGES_REQUESTED";
        if (!byLecturer && !studentRevision) return Results.Forbid();
        if (!Active(project) || item.Status is MilestoneStatus.SUBMITTED or MilestoneStatus.APPROVED)
            return Conflict("Không được sửa Milestone đang nộp chờ duyệt, đã duyệt hoặc Project đã kết thúc.");
        var error = Validate(req); if (error != null) return Invalid(error);
        item.Title = Text(req!.Title); item.Description = Text(req.Description);
        item.StartAt = req.StartAt; item.DeadlineAt = req.DeadlineAt;
        item.ExpectedResult = Text(req.ExpectedResult); item.UpdatedAt = DateTimeOffset.UtcNow;
        if (studentRevision) item.ProposalStatus = "PENDING_REVIEW";
        Audit(db, Actor(principal), studentRevision ? "MILESTONE_PROPOSAL_RESUBMITTED" : "MILESTONE_UPDATED", "Milestone", id);
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { item.Id });
    }

    public sealed record ProposalReviewInput(bool IsConfirmed, string? Feedback);
    private static async Task<IResult> ReviewProposal(Guid id, ProposalReviewInput? req,
        ClaimsPrincipal principal, StudentProjectsDbContext db, CancellationToken ct)
    {
        var item = await db.Milestones.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (item is null) return Results.NotFound();
        var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == item.ProjectId, ct);
        if (project is null) return Results.NotFound();
        if (!CanWrite(project, principal, "Lecturer")) return Results.Forbid();
        if (!Active(project) || !item.ProposedByStudent || item.ProposalStatus != "PENDING_REVIEW")
            return Conflict("Chỉ được duyệt bản đề xuất Milestone đang chờ giảng viên xem xét.");
        if (req is null || Text(req.Feedback).Length > 2000 || (!req.IsConfirmed && Text(req.Feedback).Length == 0))
            return Invalid("Khi yêu cầu sửa cần ghi nhận xét (tối đa 2000 ký tự).");
        item.ProposalStatus = req.IsConfirmed ? "CONFIRMED" : "CHANGES_REQUESTED";
        item.ProposalFeedback = Text(req.Feedback);
        item.UpdatedAt = DateTimeOffset.UtcNow;
        Audit(db, Actor(principal), req.IsConfirmed ? "MILESTONE_PROPOSAL_CONFIRMED" : "MILESTONE_PROPOSAL_CHANGES_REQUESTED", "Milestone", id);
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { item.Id, item.ProposalStatus, item.ProposalFeedback });
    }

    private static async Task<IResult> History(Guid id, ClaimsPrincipal principal,
        StudentProjectsDbContext db, CancellationToken ct)
    {
        var item = await db.Milestones.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct);
        if (item is null) return Results.NotFound();
        var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == item.ProjectId, ct);
        if (project is null) return Results.NotFound();
        if (!CanRead(project, principal)) return Results.Forbid();
        var progress = await db.ProgressUpdates.AsNoTracking().Where(x => x.MilestoneId == id)
            .OrderBy(x => x.CreatedAt).Select(x => new { x.Id, x.PercentComplete, x.WorkDone, x.Note, x.CreatedAt }).ToListAsync(ct);
        var submissions = await db.MilestoneSubmissions.AsNoTracking().Where(x => x.MilestoneId == id)
            .OrderBy(x => x.SubmittedAt).Select(x => new { x.Id, x.ResultSummary, x.DocumentReference, x.SubmittedAt }).ToListAsync(ct);
        var reviews = await db.ProjectEvaluations.AsNoTracking().Where(x => x.MilestoneId == id)
            .OrderBy(x => x.CreatedAt).Select(x => new { x.Id, x.Feedback, x.IsApproved, x.CreatedAt }).ToListAsync(ct);
        return Results.Ok(new { Progress = progress, Submissions = submissions, Reviews = reviews });
    }

    public sealed record ProgressInput(int PercentComplete, string? WorkDone, string? Note);
    private static async Task<IResult> UpdateProgress(Guid id, ProgressInput? req, ClaimsPrincipal principal,
        StudentProjectsDbContext db, CancellationToken ct)
    {
        var item = await db.Milestones.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (item is null) return Results.NotFound();
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == item.ProjectId, ct);
        if (project is null) return Results.NotFound();
        if (!CanWrite(project, principal, "Student")) return Results.Forbid();
        if (item.ProposalStatus != "CONFIRMED")
            return Conflict("Milestone do sinh viên đề xuất cần được giảng viên xác nhận trước khi thực hiện.");
        if (!Active(project) || item.Status is MilestoneStatus.SUBMITTED or MilestoneStatus.APPROVED)
            return Conflict("Milestone đang chờ duyệt hoặc đã kết thúc.");
        if (req is null || req.PercentComplete is < 0 or > 100 || Text(req.WorkDone).Length is < 1 or > 4000 || Text(req.Note).Length > 2000)
            return Invalid("Tiến độ phải từ 0 đến 100, nội dung đã làm từ 1 đến 4000 ký tự.");
        db.ProgressUpdates.Add(new ProgressUpdate { MilestoneId = id, StudentUserId = Actor(principal),
            PercentComplete = req.PercentComplete, WorkDone = Text(req.WorkDone), Note = Text(req.Note) });
        item.PercentComplete = req.PercentComplete; item.Status = MilestoneStatus.IN_PROGRESS;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        if (project.Status == ProjectStatus.REGISTERED || project.Status == ProjectStatus.DRAFT)
        { project.Status = ProjectStatus.IN_PROGRESS; project.UpdatedAt = DateTimeOffset.UtcNow; }
        Audit(db, Actor(principal), "MILESTONE_PROGRESS_UPDATED", "Milestone", id);
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { item.Id, item.PercentComplete, Status = item.Status.ToString() });
    }

    public sealed record SubmitInput(string? ResultSummary, string? DocumentReference);
    private static async Task<IResult> Submit(Guid id, SubmitInput? req, ClaimsPrincipal principal,
        StudentProjectsDbContext db, CancellationToken ct)
    {
        var item = await db.Milestones.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (item is null) return Results.NotFound();
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == item.ProjectId, ct);
        if (project is null) return Results.NotFound();
        if (!CanWrite(project, principal, "Student")) return Results.Forbid();
        if (item.ProposalStatus != "CONFIRMED")
            return Conflict("Milestone do sinh viên đề xuất cần được giảng viên xác nhận trước khi thực hiện.");
        if (!Active(project) || item.Status == MilestoneStatus.APPROVED)
            return Conflict("Milestone đang chờ duyệt hoặc đã kết thúc.");
        if (req is null || Text(req.ResultSummary).Length is < 1 or > 4000 || Text(req.DocumentReference).Length > 1000)
            return Invalid("Phải có tóm tắt kết quả (1-4000 ký tự); đường dẫn tài liệu tối đa 1000 ký tự.");
        var isResubmission = item.Status == MilestoneStatus.SUBMITTED;
        db.MilestoneSubmissions.Add(new MilestoneSubmission { MilestoneId = id,
            SubmittedByStudentUserId = Actor(principal), ResultSummary = Text(req.ResultSummary),
            DocumentReference = Text(req.DocumentReference), SubmittedAt = DateTimeOffset.UtcNow });
        item.Status = MilestoneStatus.SUBMITTED; item.UpdatedAt = DateTimeOffset.UtcNow;
        if (project.Status == ProjectStatus.REGISTERED || project.Status == ProjectStatus.DRAFT)
        { project.Status = ProjectStatus.IN_PROGRESS; project.UpdatedAt = DateTimeOffset.UtcNow; }
        Audit(db, Actor(principal), isResubmission ? "MILESTONE_RESUBMITTED" : "MILESTONE_SUBMITTED", "Milestone", id);
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { item.Id, Status = item.Status.ToString() });
    }

    public sealed record ReviewInput(bool IsApproved, string? Feedback);
    private static async Task<IResult> Review(Guid id, ReviewInput? req, ClaimsPrincipal principal,
        StudentProjectsDbContext db, CancellationToken ct)
    {
        var item = await db.Milestones.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (item is null) return Results.NotFound();
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == item.ProjectId, ct);
        if (project is null) return Results.NotFound();
        if (!CanWrite(project, principal, "Lecturer")) return Results.Forbid();
        if (!Active(project) || item.ProposalStatus != "CONFIRMED" || item.Status != MilestoneStatus.SUBMITTED)
            return Conflict("Chỉ được đánh giá kết quả Milestone đã xác nhận và ở trạng thái SUBMITTED.");
        if (req is null || Text(req.Feedback).Length > 4000 || (!req.IsApproved && Text(req.Feedback).Length == 0))
            return Invalid("Yêu cầu chỉnh sửa phải có nhận xét. Nhận xét tối đa 4000 ký tự.");
        db.ProjectEvaluations.Add(new ProjectEvaluation { ProjectId = project.Id, MilestoneId = id,
            LecturerUserId = Actor(principal), IsApproved = req.IsApproved, Feedback = Text(req.Feedback) });
        item.Status = req.IsApproved ? MilestoneStatus.APPROVED : MilestoneStatus.REVISION_REQUIRED;
        if (req.IsApproved) item.PercentComplete = 100;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        Audit(db, Actor(principal), req.IsApproved ? "MILESTONE_APPROVED" : "MILESTONE_REVISION_REQUIRED", "Milestone", id);
        // Completion is calculated while the scoped milestone is tracked; only at least one milestone.
        var all = await db.Milestones.Where(m => m.ProjectId == project.Id).ToListAsync(ct);
        if (req.IsApproved && all.Count > 0 && all.All(m => m.Status == MilestoneStatus.APPROVED))
        { project.Status = ProjectStatus.COMPLETED; project.UpdatedAt = DateTimeOffset.UtcNow; }
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { item.Id, Status = item.Status.ToString(), ProjectStatus = project.Status.ToString() });
    }

    private static void Audit(StudentProjectsDbContext db, Guid actor, string action, string entityName, Guid id) =>
        db.AuditEntries.Add(new AuditEntry { ActorUserId = actor, Action = action, EntityName = entityName, EntityId = id });
}
