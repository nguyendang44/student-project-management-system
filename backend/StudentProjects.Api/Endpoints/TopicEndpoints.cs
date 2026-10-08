using System.Security.Claims;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StudentProjects.Application.Contracts;
using StudentProjects.Domain.Entities;
using StudentProjects.Domain.Enums;
using StudentProjects.Infrastructure.Persistence;

namespace StudentProjects.Api.Endpoints;

public static class TopicEndpoints
{
    public static void MapTopicEndpoints(this IEndpointRouteBuilder app)
    {
        var topics = app.MapGroup("/api/v1/topics").RequireAuthorization();
        topics.MapGet("", ListTopics);
        topics.MapGet("/mine", MyReservedTopics).RequireAuthorization(p => p.RequireRole("Student"));
        topics.MapPost("/{id:guid}/withdraw", WithdrawTopic).RequireAuthorization(p => p.RequireRole("Student"));
        topics.MapGet("/{id:guid}", GetTopic);
        topics.MapDelete("/{id:guid}", DeleteTopic).RequireAuthorization(p => p.RequireRole("Admin", "Lecturer"));
        topics.MapGet("/{id:guid}/history", GetHistory);
        topics.MapPost("", CreateTopic).RequireAuthorization(p => p.RequireRole("Admin", "Lecturer", "Student"));
        topics.MapPatch("/{id:guid}", UpdateTopic);
        topics.MapPatch("/{id:guid}/registration", SetRegistration).RequireAuthorization(p => p.RequireRole("Admin", "Lecturer"));
        var proposals = app.MapGroup("/api/v1/topic-proposals").RequireAuthorization();
        proposals.MapGet("", ListProposals);
        proposals.MapPost("", Propose).RequireAuthorization(p => p.RequireRole("Student"));
        proposals.MapPost("/{id:guid}/submit", Resubmit).RequireAuthorization(p => p.RequireRole("Student"));
        proposals.MapPost("/{id:guid}/approve", Approve).RequireAuthorization(p => p.RequireRole("Lecturer"));
        proposals.MapPost("/{id:guid}/reject", Reject).RequireAuthorization(p => p.RequireRole("Lecturer"));
        var periods = app.MapGroup("/api/v1/registration-periods").RequireAuthorization();
        periods.MapGet("", ListPeriods);
        periods.MapPost("", CreatePeriod).RequireAuthorization(p => p.RequireRole("Admin"));
        periods.MapPatch("/{id:guid}/status", SetPeriodState).RequireAuthorization(p => p.RequireRole("Admin"));
        var registrations = app.MapGroup("/api/v1/topic-registrations").RequireAuthorization();
        registrations.MapGet("", ListRegistrations);
        registrations.MapPost("", RegisterTopic).RequireAuthorization(p => p.RequireRole("Student"));
        registrations.MapPost("/{id:guid}/cancel", CancelRegistration).RequireAuthorization(p => p.RequireRole("Student"));
        registrations.MapPost("/{id:guid}/accept", AcceptRegistration).RequireAuthorization(p => p.RequireRole("Lecturer"));
        registrations.MapPost("/{id:guid}/reject", RejectRegistration).RequireAuthorization(p => p.RequireRole("Lecturer"));
    }

    private static Guid Actor(ClaimsPrincipal user) => Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
    private static string Role(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.Role) ?? "";
    private static IResult Invalid(string msg) => Results.BadRequest(new { code = "INVALID_REQUEST", message = msg });
    private static IResult Conflict(string msg) => Results.Conflict(new { code = "CONFLICT", message = msg });
    private static bool Valid(TopicInput? value) => value is not null &&
        !string.IsNullOrWhiteSpace(value.Title) && value.Title.Trim().Length <= 300 &&
        !string.IsNullOrWhiteSpace(value.Description) && value.Description.Trim().Length <= 4000 &&
        (value.Objective is null || value.Objective.Length <= 2000) &&
        (value.ExpectedContent is null || value.ExpectedContent.Length <= 2000) &&
        (value.ProposedTechnology is null || value.ProposedTechnology.Length <= 1000);
    private static bool CompleteStudentProposal(TopicInput data) =>
        !string.IsNullOrWhiteSpace(data.Objective) && !string.IsNullOrWhiteSpace(data.ExpectedContent) &&
        !string.IsNullOrWhiteSpace(data.ProposedTechnology);
    private static bool CompleteStudentProposal(Topic topic) =>
        !string.IsNullOrWhiteSpace(topic.Objective) && !string.IsNullOrWhiteSpace(topic.ExpectedContent) &&
        !string.IsNullOrWhiteSpace(topic.ProposedTechnology);
    private static void Apply(Topic topic, TopicInput data)
    {
        topic.Title = data.Title!.Trim(); topic.Description = data.Description!.Trim();
        topic.Objective = data.Objective?.Trim(); topic.ExpectedContent = data.ExpectedContent?.Trim();
        topic.ProposedTechnology = data.ProposedTechnology?.Trim(); topic.UpdatedAt = DateTimeOffset.UtcNow;
    }
    // Relational locking is required because accepting a topic must be an exclusive operation.
    private static Task<Topic?> LockedTopic(StudentProjectsDbContext db, Guid id, CancellationToken ct) =>
        db.Database.IsSqlServer()
            ? db.Topics.FromSqlInterpolated($"SELECT * FROM [Topics] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {id}")
                .SingleOrDefaultAsync(ct)
            : db.Topics.FindAsync([id], ct).AsTask();

    // Every ownership-granting path locks the same student row first.
    // Locking only the topic row would not serialize approvals for two different topics.
    private static Task<User?> LockedStudent(StudentProjectsDbContext db, Guid id, CancellationToken ct) =>
        db.Database.IsSqlServer()
            ? db.Users.FromSqlInterpolated($"SELECT * FROM [Users] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {id}")
                .SingleOrDefaultAsync(ct)
            : db.Users.FindAsync([id], ct).AsTask();

    private static async Task<bool> StudentHasReservedTopic(StudentProjectsDbContext db, Guid studentId, CancellationToken ct) =>
        await db.Topics.AnyAsync(t => t.ProposedByUserId == studentId &&
            (t.Status == TopicStatus.APPROVED || t.Status == TopicStatus.IN_PROGRESS ||
             t.Status == TopicStatus.COMPLETED), ct) ||
        await db.TopicRegistrations.AnyAsync(r => r.StudentUserId == studentId &&
            r.Status == RequestStatus.ACCEPTED, ct);

    private static IQueryable<Topic> VisibleTopics(StudentProjectsDbContext db, ClaimsPrincipal principal)
    {
        var id = Actor(principal); var role = Role(principal);
        var baseQuery = db.Topics.AsNoTracking();
        if (role == "Student") baseQuery = baseQuery.Where(t =>
            t.Status == TopicStatus.APPROVED || t.Status == TopicStatus.PUBLISHED || t.ProposedByUserId == id);
        else if (role == "Lecturer") baseQuery = baseQuery.Where(t =>
            t.Status == TopicStatus.APPROVED || t.Status == TopicStatus.PUBLISHED ||
            t.Status == TopicStatus.CANCELLED || t.ProposedByUserId == id ||
            (t.Status == TopicStatus.PENDING_APPROVAL &&
                (!db.LecturerRequests.Any(r => r.TopicId == t.Id && r.IsCombined &&
                    (r.Status == RequestStatus.PENDING || r.Status == RequestStatus.REVISION_REQUIRED)) ||
                 db.LecturerRequests.Any(r => r.TopicId == t.Id && r.IsCombined &&
                    r.LecturerUserId == id &&
                    (r.Status == RequestStatus.PENDING || r.Status == RequestStatus.REVISION_REQUIRED)))));
        else if (role != "Admin") baseQuery = baseQuery.Where(t => false);
        return baseQuery;
    }
    // A student-created approved topic belongs to that student without a separate registration.
    // Published topics are reserved by the accepted registration. Both use existing v0.5 tables.
    private static IQueryable<TopicItem> ProjectTopics(StudentProjectsDbContext db, IQueryable<Topic> query) =>
        from t in query.OrderBy(t => t.Title).ThenBy(t => t.Id)
        join author in db.Users.AsNoTracking() on t.ProposedByUserId equals author.Id
        select new TopicItem(t.Id, t.Title, t.Description, t.Objective, t.ExpectedContent,
            t.ProposedTechnology, t.ProposedByUserId, author.FullName, t.Status.ToString(),
            t.IsRegistrationOpen, t.CreatedAt,
            author.RoleId == RoleIds.Student &&
                (t.Status == TopicStatus.APPROVED || t.Status == TopicStatus.IN_PROGRESS || t.Status == TopicStatus.COMPLETED)
                ? (Guid?)t.ProposedByUserId
                : db.TopicRegistrations.Where(r => r.TopicId == t.Id && r.Status == RequestStatus.ACCEPTED)
                    .Select(r => (Guid?)r.StudentUserId).FirstOrDefault(),
            // Requests can be deleted with their topic, but assigned topics cannot.
            !db.TopicRegistrations.Any(r => r.TopicId == t.Id && r.Status == RequestStatus.ACCEPTED) &&
            t.Status != TopicStatus.IN_PROGRESS && t.Status != TopicStatus.COMPLETED &&
            !(author.RoleId == RoleIds.Student && t.Status == TopicStatus.APPROVED));
    private static IQueryable<TopicItem> QueryTopics(StudentProjectsDbContext db, ClaimsPrincipal principal) =>
        ProjectTopics(db, VisibleTopics(db, principal));
    private static async Task<IResult> ListTopics(StudentProjectsDbContext db, ClaimsPrincipal principal,
        string? search, string? status, int? page, int? pageSize, CancellationToken ct)
    {
        int current = page ?? 1, take = pageSize ?? 20;
        if (current < 1 || current > 100000 || take < 1 || take > 100 || search?.Length > 200) return Invalid("Invalid paging/search.");
        var query = VisibleTopics(db, principal);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.Title.Contains(search.Trim()));
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<TopicStatus>(status, true, out var wanted)) return Invalid("Invalid status.");
            query = query.Where(x => x.Status == wanted);
        }
        int total = await query.CountAsync(ct);
        // Filter and order on entity columns before projecting TopicItem.
        var items = await ProjectTopics(db, query).Skip((current - 1) * take).Take(take).ToListAsync(ct);
        return Results.Ok(new TopicPage(items, total, current, take));
    }
    private static async Task<IResult> MyReservedTopics(StudentProjectsDbContext db, ClaimsPrincipal principal, CancellationToken ct)
    {
        var studentId = Actor(principal);
        var query = db.Topics.AsNoTracking().Where(t =>
            (t.ProposedByUserId == studentId &&
             (t.Status == TopicStatus.APPROVED || t.Status == TopicStatus.IN_PROGRESS || t.Status == TopicStatus.COMPLETED)) ||
            db.TopicRegistrations.Any(r => r.TopicId == t.Id && r.StudentUserId == studentId &&
                r.Status == RequestStatus.ACCEPTED));
        return Results.Ok(await ProjectTopics(db, query).ToListAsync(ct));
    }

    // DELETE /api/v1/topics/{id}: Admin may delete any creator's topic; Lecturer only their own.
    // Delete request rows together with the topic, but never delete an assigned topic or project.
    private static async Task<IResult> DeleteTopic(Guid id, StudentProjectsDbContext db,
        ClaimsPrincipal principal, CancellationToken ct)
    {
        var role = Role(principal);
        var actor = Actor(principal);
        await using var transaction = db.Database.IsSqlServer()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        // Matches the lock used by registration, acceptance and proposal approval.
        var topic = await LockedTopic(db, id, ct);
        if (topic is null) return Results.NotFound();
        if (role == "Lecturer" && topic.ProposedByUserId != actor) return Results.Forbid();
        if (role is not ("Admin" or "Lecturer")) return Results.Forbid();

        // An ACCEPTED registration means the topic is owned. Other requests are
        // deleted in the same transaction as the topic, regardless of their status.
        if (await db.TopicRegistrations.AnyAsync(r => r.TopicId == id && r.Status == RequestStatus.ACCEPTED, ct))
            return Conflict("Topic is assigned to a student; ownership must be relinquished before deletion.");
        // Projects and LecturerRequests are declared in EF but NOT migrated until later versions.
        // Never query absent SQL Server tables (that would cause API 500 in v0.5).
        var projectTableExists = !db.Database.IsSqlServer() ||
            await db.Database.SqlQueryRaw<int>(
                "SELECT CASE WHEN OBJECT_ID(N'dbo.Projects', N'U') IS NULL THEN 0 ELSE 1 END AS [Value]")
                .SingleAsync(ct) == 1;
        if (projectTableExists && await db.Projects.AnyAsync(p => p.TopicId == id, ct))
            return Conflict("Topic belongs to a project and cannot be deleted.");
        var lecturerRequestTableExists = !db.Database.IsSqlServer() ||
            await db.Database.SqlQueryRaw<int>(
                "SELECT CASE WHEN OBJECT_ID(N'dbo.LecturerRequests', N'U') IS NULL THEN 0 ELSE 1 END AS [Value]")
                .SingleAsync(ct) == 1;
        if (lecturerRequestTableExists && await db.LecturerRequests.AnyAsync(r => r.TopicId == id &&
            r.Status == RequestStatus.ACCEPTED, ct))
            return Conflict("Topic has an accepted lecturer request and cannot be deleted.");
        if (topic.Status is TopicStatus.IN_PROGRESS or TopicStatus.COMPLETED)
            return Conflict("Active or completed topic cannot be deleted.");
        if (topic.Status == TopicStatus.APPROVED &&
            await db.Users.AnyAsync(u => u.Id == topic.ProposedByUserId && u.RoleId == RoleIds.Student, ct))
            return Conflict("Approved student proposal already belongs to its author; it cannot be deleted before ownership is relinquished.");

        // Both child tables use NoAction foreign keys; delete them explicitly.
        // Keep these deletes and the topic delete atomic to avoid orphaned rows.
        var registrations = await db.TopicRegistrations.Where(r => r.TopicId == id).ToListAsync(ct);
        var histories = await db.TopicStateHistories.Where(h => h.TopicId == id).ToListAsync(ct);
        // v0.6: unassigned topic may have old, pending or rejected supervisor requests.
        // Remove their rows before deleting the topic (foreign key is NoAction).
        if (lecturerRequestTableExists)
        {
            var oldRequests = await db.LecturerRequests.Where(r => r.TopicId == id).ToListAsync(ct);
            db.LecturerRequests.RemoveRange(oldRequests);
        }
        db.TopicRegistrations.RemoveRange(registrations);
        db.TopicStateHistories.RemoveRange(histories);
        db.Topics.Remove(topic);
        db.AuditEntries.Add(new AuditEntry { ActorUserId = actor, Action = "TOPIC_DELETED",
            EntityName = "Topic", EntityId = id });
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> GetTopic(Guid id, StudentProjectsDbContext db, ClaimsPrincipal principal, CancellationToken ct)
    {
        var item = await ProjectTopics(db, VisibleTopics(db, principal).Where(t => t.Id == id)).SingleOrDefaultAsync(ct);
        return item is null ? Results.NotFound() : Results.Ok(item);
    }
    private static async Task<IResult> GetHistory(Guid id, StudentProjectsDbContext db, ClaimsPrincipal principal, CancellationToken ct)
    {
        var topic = await db.Topics.AsNoTracking().Where(t => t.Id == id)
            .Select(t => new { t.Id, t.ProposedByUserId, t.Status }).SingleOrDefaultAsync(ct);
        if (topic is null) return Results.NotFound();
        var role = Role(principal);
        if (role == "Student" && topic.ProposedByUserId != Actor(principal)) return Results.NotFound();
        if (role == "Lecturer" && topic.Status == TopicStatus.DRAFT && topic.ProposedByUserId != Actor(principal)) return Results.NotFound();
        var items = await db.TopicStateHistories.AsNoTracking().Where(h => h.TopicId == id)
            .OrderBy(h => h.CreatedAt).Select(h => new TopicTransitionItem(h.FromStatus.ToString(),
                h.ToStatus.ToString(), h.Reason, h.CreatedAt)).ToListAsync(ct);
        return Results.Ok(items);
    }
    private static async Task<IResult> CreateTopic(TopicInput? input, StudentProjectsDbContext db, ClaimsPrincipal principal, CancellationToken ct)
    {
        if (!Valid(input)) return Invalid("Title and description are required and must satisfy length limits.");
        var role = Role(principal);
        if (role == "Student" && !CompleteStudentProposal(input!))
            return Invalid("Objective, expected content and technology are required for student topics.");
        var status = role == "Student" ? TopicStatus.DRAFT : TopicStatus.APPROVED;
        var topic = new Topic { ProposedByUserId = Actor(principal), Status = status, IsRegistrationOpen = false };
        Apply(topic, input!);
        db.Topics.Add(topic);
        db.TopicStateHistories.Add(new TopicStateHistory { TopicId = topic.Id, ActorUserId = Actor(principal), FromStatus = status, ToStatus = status });
        db.AuditEntries.Add(new AuditEntry { ActorUserId = Actor(principal), Action = "TOPIC_CREATED", EntityName = "Topic", EntityId = topic.Id });
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/v1/topics/{topic.Id}", await ProjectTopics(db, VisibleTopics(db, principal).Where(t => t.Id == topic.Id)).SingleAsync(ct));
    }
    private static async Task<IResult> UpdateTopic(Guid id, TopicInput? input, StudentProjectsDbContext db, ClaimsPrincipal principal, CancellationToken ct)
    {
        if (!Valid(input)) return Invalid("Title and description are required and must satisfy length limits.");
        var topic = await db.Topics.FindAsync([id], ct);
        if (topic is null) return Results.NotFound();
        var role = Role(principal); var actor = Actor(principal);
        // CANCELLED student proposals and subsequently PUBLISHED topics become staff-managed.
        bool staffManaged = topic.Status is TopicStatus.CANCELLED or TopicStatus.PUBLISHED;
        bool allowed = role == "Admin" ||
            (role == "Lecturer" && (topic.ProposedByUserId == actor || staffManaged)) ||
            (role == "Student" && topic.ProposedByUserId == actor &&
             (topic.Status is TopicStatus.DRAFT or TopicStatus.REJECTED));
        if (!allowed) return Results.Forbid();
        if (role == "Student" && !CompleteStudentProposal(input!))
            return Invalid("Objective, expected content and technology are required for student topics.");
        if (topic.Status is TopicStatus.IN_PROGRESS or TopicStatus.COMPLETED) return Conflict("Active or completed topic cannot be edited.");
        Apply(topic, input!);
        // Registration status is deliberately not modified by the content edit endpoint.
        db.AuditEntries.Add(new AuditEntry { ActorUserId = actor, Action = "TOPIC_UPDATED", EntityName = "Topic", EntityId = id });
        await db.SaveChangesAsync(ct);
        return Results.Ok(await ProjectTopics(db, VisibleTopics(db, principal).Where(t => t.Id == id)).SingleAsync(ct));
    }
    private static async Task<IResult> SetRegistration(Guid id, PeriodStateInput? input,
        ClaimsPrincipal principal, StudentProjectsDbContext db, CancellationToken ct)
    {
        if (input?.IsOpen is null) return Invalid("isOpen required.");
        await using var transaction = db.Database.IsSqlServer()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var topic = await LockedTopic(db, id, ct);
        if (topic is null) return Results.NotFound();
        var role = Role(principal); var actor = Actor(principal);
        if (role != "Admin" && (role != "Lecturer" ||
            (topic.ProposedByUserId != actor && topic.Status is not (TopicStatus.CANCELLED or TopicStatus.PUBLISHED))))
            return Results.Forbid();
        // A relinquished student proposal may be republished, but never restored to APPROVED:
        // APPROVED would automatically reassign it to the original student.
        if (topic.Status is not (TopicStatus.APPROVED or TopicStatus.PUBLISHED or TopicStatus.CANCELLED))
            return Conflict("Only approved, published, or relinquished topics can change registration availability.");
        if (topic.Status == TopicStatus.CANCELLED && !input.IsOpen.Value)
            return Conflict("Relinquished topic is already closed. Open it to republish.");
        var proposerIsStudent = await db.Users.AnyAsync(u => u.Id == topic.ProposedByUserId && u.RoleId == RoleIds.Student, ct);
        if (proposerIsStudent && topic.Status == TopicStatus.APPROVED)
            return Conflict("An approved student proposal is reserved for its author until relinquished.");
        if (input.IsOpen.Value && await db.TopicRegistrations.AnyAsync(r => r.TopicId == id && r.Status == RequestStatus.ACCEPTED, ct))
            return Conflict("This topic already belongs to a student.");
        if (topic.Status == TopicStatus.CANCELLED)
        {
            topic.Status = TopicStatus.PUBLISHED;
            db.TopicStateHistories.Add(new TopicStateHistory {
                TopicId = id, ActorUserId = actor, FromStatus = TopicStatus.CANCELLED,
                ToStatus = TopicStatus.PUBLISHED, Reason = "Staff republished a relinquished topic"
            });
        }
        topic.IsRegistrationOpen = input.IsOpen.Value; topic.UpdatedAt = DateTimeOffset.UtcNow;
        db.AuditEntries.Add(new AuditEntry { ActorUserId = actor, Action = input.IsOpen.Value ? "TOPIC_OPENED" : "TOPIC_CLOSED", EntityName = "Topic", EntityId = id });
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return Results.Ok(await ProjectTopics(db, VisibleTopics(db, principal).Where(t => t.Id == id)).SingleAsync(ct));
    }
    private static async Task<IResult> ListProposals(StudentProjectsDbContext db, ClaimsPrincipal principal, CancellationToken ct)
    {
        var query = VisibleTopics(db, principal).Where(x => x.Status == TopicStatus.PENDING_APPROVAL || x.Status == TopicStatus.REJECTED);
        var items = await ProjectTopics(db, query).Take(100).ToListAsync(ct);
        return Results.Ok(items);
    }
    private static async Task<IResult> Propose(TopicInput? input, StudentProjectsDbContext db, ClaimsPrincipal principal, CancellationToken ct)
    {
        if (!Valid(input) || string.IsNullOrWhiteSpace(input!.Objective) ||
            string.IsNullOrWhiteSpace(input.ExpectedContent) || string.IsNullOrWhiteSpace(input.ProposedTechnology))
            return Invalid("Title, description, objective, expected content and technology are required.");
        var actor = Actor(principal);
        if (await StudentHasReservedTopic(db, actor, ct))
            return Conflict("You already own a topic. Relinquish it before proposing another.");
        var topic = new Topic { ProposedByUserId = actor, Status = TopicStatus.PENDING_APPROVAL, IsRegistrationOpen = false };
        Apply(topic, input!);
        db.Topics.Add(topic);
        db.TopicStateHistories.Add(new TopicStateHistory { TopicId = topic.Id, ActorUserId = actor,
            FromStatus = TopicStatus.DRAFT, ToStatus = TopicStatus.PENDING_APPROVAL });
        db.AuditEntries.Add(new AuditEntry { ActorUserId = actor, Action = "TOPIC_PROPOSED", EntityName = "Topic", EntityId = topic.Id });
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/v1/topics/{topic.Id}", await ProjectTopics(db, VisibleTopics(db, principal).Where(t => t.Id == topic.Id)).SingleAsync(ct));
    }
    private static async Task<IResult> Resubmit(Guid id, StudentProjectsDbContext db, ClaimsPrincipal principal, CancellationToken ct)
    {
        var topic = await db.Topics.FindAsync([id], ct);
        if (topic is null) return Results.NotFound();
        var actor = Actor(principal);
        if (topic.ProposedByUserId != actor) return Results.Forbid();
        if (topic.Status is not (TopicStatus.DRAFT or TopicStatus.REJECTED)) return Conflict("Only draft/rejected proposals can be submitted.");
        if (!CompleteStudentProposal(topic)) return Invalid("Complete all proposal fields before submitting.");
        if (await StudentHasReservedTopic(db, actor, ct))
            return Conflict("You already own a topic. Relinquish it before submitting another.");
        var previous = topic.Status; topic.Status = TopicStatus.PENDING_APPROVAL; topic.UpdatedAt = DateTimeOffset.UtcNow;
        db.TopicStateHistories.Add(new TopicStateHistory { TopicId = id, ActorUserId = actor,
            FromStatus = previous, ToStatus = topic.Status });
        await db.SaveChangesAsync(ct);
        return Results.Ok(await ProjectTopics(db, VisibleTopics(db, principal).Where(t => t.Id == id)).SingleAsync(ct));
    }
    private static async Task<IResult> Approve(Guid id, StudentProjectsDbContext db, ClaimsPrincipal principal, CancellationToken ct) =>
        await Review(id, null, true, db, principal, ct);
    private static async Task<IResult> Reject(Guid id, RejectTopicRequest? input, StudentProjectsDbContext db,
        ClaimsPrincipal principal, CancellationToken ct) => await Review(id, input?.Reason, false, db, principal, ct);
    private static async Task<IResult> Review(Guid id, string? reason, bool approve, StudentProjectsDbContext db,
        ClaimsPrincipal principal, CancellationToken ct)
    {
        if (!approve && (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 2000)) return Invalid("Rejection reason is required (max 2000 chars).");
        var candidate = await db.Topics.AsNoTracking().Where(t => t.Id == id)
            .Select(t => new { t.ProposedByUserId }).SingleOrDefaultAsync(ct);
        if (candidate is null) return Results.NotFound();
        if (candidate.ProposedByUserId == Actor(principal)) return Results.Forbid();
        await using var transaction = db.Database.IsSqlServer()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        // Approval and accepted registration both serialize on this student, not the topic.
        if (approve && await LockedStudent(db, candidate.ProposedByUserId, ct) is null)
            return Results.NotFound();
        var topic = await LockedTopic(db, id, ct);
        if (topic is null) return Results.NotFound();
        if (topic.Status != TopicStatus.PENDING_APPROVAL) return Conflict("Proposal is not pending review.");
        if (await db.LecturerRequests.AnyAsync(r => r.TopicId == id && r.IsCombined &&
            (r.Status == RequestStatus.PENDING || r.Status == RequestStatus.REVISION_REQUIRED), ct))
            return Conflict("This proposal must be reviewed by its selected lecturer in the joint application.");
        if (topic.ProposedByUserId == Actor(principal)) return Results.Forbid();
        var proposerRole = await db.Users.Where(u => u.Id == topic.ProposedByUserId).Select(u => u.Role.Name).SingleOrDefaultAsync(ct);
        if (proposerRole != "Student") return Conflict("Only student proposals can be reviewed.");
        if (approve && await StudentHasReservedTopic(db, topic.ProposedByUserId, ct))
            return Conflict("Student already owns a topic. Only one topic can be approved per student.");
        if (approve)
        {
            // A student's approved self-proposal has implicit ownership (no ACCEPTED
            // registration row). Remove ALL their earlier requests, including history.
            var previousRequests = await db.TopicRegistrations.Where(r =>
                r.StudentUserId == topic.ProposedByUserId).ToListAsync(ct);
            db.TopicRegistrations.RemoveRange(previousRequests);
        }
        topic.Status = approve ? TopicStatus.APPROVED : TopicStatus.REJECTED;
        topic.IsRegistrationOpen = false; topic.UpdatedAt = DateTimeOffset.UtcNow;
        db.TopicStateHistories.Add(new TopicStateHistory { TopicId = id, ActorUserId = Actor(principal),
            FromStatus = TopicStatus.PENDING_APPROVAL, ToStatus = topic.Status, Reason = approve ? null : reason!.Trim() });
        db.AuditEntries.Add(new AuditEntry { ActorUserId = Actor(principal), Action = approve ? "TOPIC_APPROVED" : "TOPIC_REJECTED", EntityName = "Topic", EntityId = id });
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        // A rejected proposal is no longer visible in the lecturer's general topic list.
        // The reviewer was authorized above, so return the reviewed topic directly
        // rather than filtering out the just-rejected row and throwing from SingleAsync.
        var reviewedTopic = await ProjectTopics(db, db.Topics.AsNoTracking().Where(t => t.Id == id)).SingleAsync(ct);
        return Results.Ok(reviewedTopic);
    }
    private static async Task<IResult> ListPeriods(StudentProjectsDbContext db, CancellationToken ct)
    {
        var items = await db.RegistrationPeriods.AsNoTracking().OrderByDescending(p => p.StartsAt)
            .Select(p => new PeriodItem(p.Id, p.Name, p.StartsAt, p.EndsAt, p.IsOpen)).Take(100).ToListAsync(ct);
        return Results.Ok(items);
    }
    private static async Task<IResult> CreatePeriod(PeriodInput? input, StudentProjectsDbContext db, CancellationToken ct)
    {
        if (input is null || string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length > 200 ||
            input.EndsAt <= input.StartsAt || input.StartsAt == default || input.EndsAt == default)
            return Invalid("Valid period name and start/end times are required.");
        var period = new RegistrationPeriod { Name = input.Name.Trim(), StartsAt = input.StartsAt,
            EndsAt = input.EndsAt, IsOpen = input.IsOpen };
        db.RegistrationPeriods.Add(period); await db.SaveChangesAsync(ct);
        return Results.Created($"/api/v1/registration-periods/{period.Id}",
            new PeriodItem(period.Id, period.Name, period.StartsAt, period.EndsAt, period.IsOpen));
    }
    private static async Task<IResult> SetPeriodState(Guid id, PeriodStateInput? input, StudentProjectsDbContext db, CancellationToken ct)
    {
        if (input?.IsOpen is null) return Invalid("isOpen required.");
        var period = await db.RegistrationPeriods.FindAsync([id], ct);
        if (period is null) return Results.NotFound();
        period.IsOpen = input.IsOpen.Value; period.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Results.Ok(new PeriodItem(period.Id, period.Name, period.StartsAt, period.EndsAt, period.IsOpen));
    }
    private static async Task<IResult> ListRegistrations(StudentProjectsDbContext db, ClaimsPrincipal principal, CancellationToken ct)
    {
        var actor = Actor(principal); var role = Role(principal);
        var registrations = db.TopicRegistrations.AsNoTracking();
        if (role == "Student") registrations = registrations.Where(r => r.StudentUserId == actor);
        else if (role == "Lecturer") registrations = registrations.Where(r => db.Topics.Any(t => t.Id == r.TopicId &&
            (t.Status == TopicStatus.PUBLISHED || t.ProposedByUserId == actor ||
             db.Users.Any(u => u.Id == t.ProposedByUserId && u.RoleId == RoleIds.Admin))));
        else if (role != "Admin") return Results.Forbid();
        var items = await (from r in registrations
            join t in db.Topics on r.TopicId equals t.Id
            join s in db.Users on r.StudentUserId equals s.Id
            join p in db.RegistrationPeriods on r.RegistrationPeriodId equals p.Id
            orderby r.CreatedAt descending
            select new RegistrationItem(r.Id, r.TopicId, t.Title, r.StudentUserId,
                s.FullName, r.RegistrationPeriodId, p.Name, r.Status.ToString(), r.CreatedAt)).Take(100).ToListAsync(ct);
        return Results.Ok(items);
    }
    private static async Task<IResult> RegisterTopic(RegisterTopicRequest? input, StudentProjectsDbContext db,
        ClaimsPrincipal principal, CancellationToken ct)
    {
        if (input is null || input.TopicId == Guid.Empty || input.RegistrationPeriodId == Guid.Empty) return Invalid("Topic and registration period are required.");
        var actor = Actor(principal);
        await using var transaction = db.Database.IsSqlServer()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        // Keep lock ordering identical to the acceptance/approval paths. This ensures
        // a new PENDING request cannot slip in while a lecturer assigns a topic.
        if (await LockedStudent(db, actor, ct) is null) return Results.NotFound();
        var topic = await LockedTopic(db, input.TopicId, ct);
        var period = await db.RegistrationPeriods.AsNoTracking().SingleOrDefaultAsync(p => p.Id == input.RegistrationPeriodId, ct);
        if (topic is null || period is null) return Results.NotFound();
        var now = DateTimeOffset.UtcNow;
        if (topic.Status is not (TopicStatus.APPROVED or TopicStatus.PUBLISHED) || !topic.IsRegistrationOpen)
            return Conflict("Topic registration is closed.");
        if (topic.Status == TopicStatus.APPROVED &&
            await db.Users.AnyAsync(u => u.Id == topic.ProposedByUserId && u.RoleId == RoleIds.Student, ct))
            return Conflict("Approved student proposals are reserved for their author.");
        if (await db.TopicRegistrations.AnyAsync(r => r.TopicId == topic.Id && r.Status == RequestStatus.ACCEPTED, ct))
            return Conflict("This topic has already been assigned to another student.");
        if (await StudentHasReservedTopic(db, actor, ct))
            return Conflict("You already own a topic. Relinquish it before registering for another.");
        if (!period.IsOpen || period.StartsAt > now || period.EndsAt <= now) return Conflict("Registration period is not currently open.");
        // Multiple different topics may be pending even within the same period.
        // Duplicate active requests for the SAME topic (across periods) are not allowed.
        if (await db.TopicRegistrations.AnyAsync(r => r.StudentUserId == actor && r.TopicId == topic.Id &&
            (r.Status == RequestStatus.PENDING || r.Status == RequestStatus.ACCEPTED), ct))
            return Conflict("You already have an active registration for this topic.");
        var registration = new TopicRegistration { StudentUserId = actor, TopicId = topic.Id,
            RegistrationPeriodId = period.Id, Status = RequestStatus.PENDING };
        db.TopicRegistrations.Add(registration);
        db.AuditEntries.Add(new AuditEntry { ActorUserId = actor, Action = "TOPIC_REGISTERED", EntityName = "TopicRegistration", EntityId = registration.Id });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && sql.Number is 2601 or 2627)
        { return Conflict("You already have an active registration for this topic."); }
        if (transaction is not null) await transaction.CommitAsync(ct);
        return Results.Created($"/api/v1/topic-registrations/{registration.Id}",
            new RegistrationItem(registration.Id, topic.Id, topic.Title, actor,
                (await db.Users.Where(u => u.Id == actor).Select(u => u.FullName).SingleAsync(ct)),
                period.Id, period.Name, registration.Status.ToString(), registration.CreatedAt));
    }
    private static async Task<IResult> AcceptRegistration(Guid id, StudentProjectsDbContext db,
        ClaimsPrincipal principal, CancellationToken ct) =>
        await ReviewRegistration(id, true, db, principal, ct);

    private static async Task<IResult> RejectRegistration(Guid id, StudentProjectsDbContext db,
        ClaimsPrincipal principal, CancellationToken ct) =>
        await ReviewRegistration(id, false, db, principal, ct);

    private static async Task<IResult> ReviewRegistration(Guid id, bool accept, StudentProjectsDbContext db,
        ClaimsPrincipal principal, CancellationToken ct)
    {
        var reg = await db.TopicRegistrations.FindAsync([id], ct);
        if (reg is null) return Results.NotFound();
        // Serializing on the Topic row prevents two lecturers from accepting competing requests.
        // EF InMemory used by integration tests does not support relational transactions/SQL locks.
        await using var transaction = db.Database.IsSqlServer()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        if (accept && await LockedStudent(db, reg.StudentUserId, ct) is null)
            return Results.NotFound();
        var topic = await LockedTopic(db, reg.TopicId, ct);
        // Recheck registration after the topic lock, in case it was cancelled meanwhile.
        if (db.Database.IsSqlServer()) await db.Entry(reg).ReloadAsync(ct);
        if (topic is null) return Results.NotFound();
        // A lecturer owns their published topic; admin-published topics can be reviewed by any lecturer
        // until a dedicated supervisor-assignment workflow is introduced.
        if (topic.Status != TopicStatus.PUBLISHED && topic.ProposedByUserId != Actor(principal) &&
            !await db.Users.AnyAsync(u => u.Id == topic.ProposedByUserId && u.RoleId == RoleIds.Admin, ct))
            return Results.Forbid();
        if (reg.Status != RequestStatus.PENDING) return Conflict("The registration is no longer pending.");
        if (accept)
        {
            if (topic.Status is not (TopicStatus.APPROVED or TopicStatus.PUBLISHED) || !topic.IsRegistrationOpen)
                return Conflict("Topic registration is no longer open.");
            if (await db.TopicRegistrations.AnyAsync(r => r.TopicId == topic.Id && r.Status == RequestStatus.ACCEPTED, ct))
                return Conflict("This topic already belongs to another student.");
            if (await StudentHasReservedTopic(db, reg.StudentUserId, ct))
                return Conflict("This student already owns a topic. Only one topic may be assigned.");
            reg.Status = RequestStatus.ACCEPTED;
            topic.IsRegistrationOpen = false;
            topic.UpdatedAt = DateTimeOffset.UtcNow;
            // The accepted choice is the student's only remaining registration.
            // Delete pending, rejected and cancelled history across ALL periods.
            var otherChoices = await db.TopicRegistrations.Where(r =>
                r.StudentUserId == reg.StudentUserId && r.Id != reg.Id).ToListAsync(ct);
            db.TopicRegistrations.RemoveRange(otherChoices);
            // Other students awaiting THIS topic lose their opportunity because it is taken.
            // REJECTED (not CANCELLED) distinguishes these from the owner's other choices.
            var competing = await db.TopicRegistrations.Where(r => r.TopicId == topic.Id &&
                r.Id != reg.Id && r.Status == RequestStatus.PENDING).ToListAsync(ct);
            foreach (var other in competing)
            {
                other.Status = RequestStatus.REJECTED;
                other.UpdatedAt = DateTimeOffset.UtcNow;
                db.AuditEntries.Add(new AuditEntry { ActorUserId = Actor(principal),
                    Action = "TOPIC_REGISTRATION_REJECTED_TOPIC_ASSIGNED", EntityName = "TopicRegistration", EntityId = other.Id });
            }
        }
        else reg.Status = RequestStatus.REJECTED;
        reg.UpdatedAt = DateTimeOffset.UtcNow;
        db.AuditEntries.Add(new AuditEntry { ActorUserId = Actor(principal),
            Action = accept ? "TOPIC_REGISTRATION_ACCEPTED" : "TOPIC_REGISTRATION_REJECTED",
            EntityName = "TopicRegistration", EntityId = id });
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return Results.Ok(new { reg.Id, status = reg.Status.ToString(), topicId = topic.Id,
            reservedForStudentUserId = accept ? reg.StudentUserId : (Guid?)null });
    }

    // Relinquishing an owned topic releases it and automatically reopens registration.
    // Cancelling a PENDING request is separate and does not change topic availability.
    private static async Task<IResult> WithdrawTopic(Guid id, StudentProjectsDbContext db,
        ClaimsPrincipal principal, CancellationToken ct)
    {
        var actor = Actor(principal);
        await using var transaction = db.Database.IsSqlServer()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var topic = await LockedTopic(db, id, ct);
        if (topic is null) return Results.NotFound();
        if (topic.Status is not (TopicStatus.APPROVED or TopicStatus.PUBLISHED))
            return Conflict("Only approved or published topics that have not started can be relinquished.");

        var studentProposed = topic.Status == TopicStatus.APPROVED &&
            await db.Users.AsNoTracking().AnyAsync(
            u => u.Id == topic.ProposedByUserId && u.RoleId == RoleIds.Student, ct);
        if (studentProposed)
        {
            if (topic.ProposedByUserId != actor) return Results.Forbid();
            // The original proposal was implicitly owned by its student author.
            // Keep the author/history, but PUBLISHED no longer implies ownership.
            // Do not reopen a record that has an additional accepted owner due to legacy data.
            if (await db.TopicRegistrations.AnyAsync(r => r.TopicId == id && r.Status == RequestStatus.ACCEPTED, ct))
                return Conflict("Topic is also assigned through a registration; resolve existing ownership before reopening.");
        }
        else
        {
            var accepted = await db.TopicRegistrations.SingleOrDefaultAsync(r =>
                r.TopicId == id && r.StudentUserId == actor && r.Status == RequestStatus.ACCEPTED, ct);
            if (accepted is null)
            {
                // A student must not be able to relinquish their own PUBLISHED topic twice.
                if (topic.Status == TopicStatus.PUBLISHED && topic.ProposedByUserId == actor &&
                    await db.Users.AnyAsync(u => u.Id == actor && u.RoleId == RoleIds.Student, ct))
                    return Conflict("You have already relinquished this topic.");
                return Results.Forbid();
            }
            if (await db.TopicRegistrations.AnyAsync(r => r.TopicId == id &&
                r.StudentUserId != actor && r.Status == RequestStatus.ACCEPTED, ct))
                return Conflict("Another student still owns this topic; it cannot be automatically reopened.");
            accepted.Status = RequestStatus.CANCELLED;
            accepted.UpdatedAt = DateTimeOffset.UtcNow;
        }

        // v0.6: a topic assigned to an active Project cannot be relinquished.
        if (await db.Projects.AnyAsync(p => p.TopicId == id && p.Status != ProjectStatus.CANCELLED, ct))
            return Conflict("Topic has an active project. Contact Admin to resolve the assignment.");
        // Requests to supervise a relinquished topic must not remain actionable.
        var pendingSupervision = await db.LecturerRequests.Where(r => r.TopicId == id &&
            r.StudentUserId == actor && r.Status == RequestStatus.PENDING).ToListAsync(ct);
        foreach (var req in pendingSupervision) { req.Status = RequestStatus.CANCELLED; req.UpdatedAt = DateTimeOffset.UtcNow; }

        // Release and reopen atomically: the topic is immediately available for NEW requests,
        // but a student can register only when there is an active registration period.
        // Do not restore APPROVED, which would implicitly reserve a student-authored topic again.
        var previousStatus = topic.Status;
        topic.Status = TopicStatus.PUBLISHED;
        topic.IsRegistrationOpen = true;
        topic.UpdatedAt = DateTimeOffset.UtcNow;
        if (previousStatus != TopicStatus.PUBLISHED)
            db.TopicStateHistories.Add(new TopicStateHistory {
                TopicId = id, ActorUserId = actor, FromStatus = previousStatus,
                ToStatus = TopicStatus.PUBLISHED,
                Reason = "Student relinquished ownership; registration automatically reopened"
            });
        db.AuditEntries.Add(new AuditEntry { ActorUserId = actor,
            Action = "TOPIC_OWNERSHIP_WITHDRAWN", EntityName = "Topic", EntityId = id });
        db.AuditEntries.Add(new AuditEntry { ActorUserId = actor,
            Action = "TOPIC_AUTO_REOPENED", EntityName = "Topic", EntityId = id });
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return Results.Ok(new { topicId = id, status = "PUBLISHED", isRegistrationOpen = true,
            reservedForStudentUserId = (Guid?)null });
    }

    private static async Task<IResult> CancelRegistration(Guid id, StudentProjectsDbContext db,
        ClaimsPrincipal principal, CancellationToken ct)
    {
        var registration = await db.TopicRegistrations.FindAsync([id], ct);
        if (registration is null) return Results.NotFound();
        if (registration.StudentUserId != Actor(principal)) return Results.Forbid();
        await using var transaction = db.Database.IsSqlServer()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        if (await LockedTopic(db, registration.TopicId, ct) is null) return Results.NotFound();
        if (db.Database.IsSqlServer()) await db.Entry(registration).ReloadAsync(ct);
        if (registration.Status != RequestStatus.PENDING) return Conflict("Only pending registrations can be cancelled.");
        registration.Status = RequestStatus.CANCELLED; registration.UpdatedAt = DateTimeOffset.UtcNow;
        db.AuditEntries.Add(new AuditEntry { ActorUserId = Actor(principal), Action = "TOPIC_REGISTRATION_CANCELLED", EntityName = "TopicRegistration", EntityId = id });
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return Results.Ok(new { registration.Id, status = registration.Status.ToString() });
    }
}
