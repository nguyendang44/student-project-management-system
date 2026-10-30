using System.Data;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using StudentProjects.Domain.Entities;
using StudentProjects.Domain.Enums;
using StudentProjects.Infrastructure.Persistence;

namespace StudentProjects.Api.Endpoints;

public sealed record CapacityUpdateV06(Guid RegistrationPeriodId, int MaxStudents);
public sealed record LecturerRequestCreateV06(Guid TopicId, Guid LecturerUserId, Guid RegistrationPeriodId);
public sealed record LecturerRequestRejectV06(string? Reason);
public sealed record TopicDraftV061(string? Title, string? Description, string? Objective, string? ExpectedContent, string? ProposedTechnology);
public sealed record CombinedApplicationV061(Guid? TopicId, Guid LecturerUserId, Guid RegistrationPeriodId, TopicDraftV061? Draft);
public sealed record RevisionRequestV061(string? Reason);
public sealed record ResubmitApplicationV061(TopicDraftV061? Draft);

/// <summary>v0.6 lecturer capacity, supervision requests and automatic project activation.</summary>
public static class LecturerSupervisionEndpoints
{
    public static void MapLecturerSupervisionEndpoints(this IEndpointRouteBuilder app)
    {
        var capacity = app.MapGroup("/api/v1/lecturer-capacity").RequireAuthorization();
        capacity.MapGet("", ListCapacity);
        capacity.MapPut("/me", UpdateCapacity).RequireAuthorization(x => x.RequireRole("Lecturer"));

        var requests = app.MapGroup("/api/v1/lecturer-requests").RequireAuthorization();
        requests.MapGet("", ListRequests);
        requests.MapPost("", CreateRequest).RequireAuthorization(x => x.RequireRole("Student"));
        requests.MapPost("/combined", SubmitCombined).RequireAuthorization(x => x.RequireRole("Student"));
        requests.MapPost("/{id:guid}/resubmit", ResubmitCombined).RequireAuthorization(x => x.RequireRole("Student"));
        requests.MapPost("/{id:guid}/cancel", CancelRequest).RequireAuthorization(x => x.RequireRole("Student"));
        requests.MapPost("/{id:guid}/request-revision", RequestRevision).RequireAuthorization(x => x.RequireRole("Lecturer"));
        requests.MapPost("/{id:guid}/accept", AcceptRequest).RequireAuthorization(x => x.RequireRole("Lecturer"));
        requests.MapPost("/{id:guid}/select", SelectOffer).RequireAuthorization(x => x.RequireRole("Student"));
        requests.MapPost("/{id:guid}/reject", RejectRequest).RequireAuthorization(x => x.RequireRole("Lecturer"));

        var projects = app.MapGroup("/api/v1/projects").RequireAuthorization();
        projects.MapGet("", ListProjects);
        projects.MapGet("/{id:guid}", GetProject);
    }

    private static Guid Actor(ClaimsPrincipal p) => Guid.TryParse(p.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
    private static string Role(ClaimsPrincipal p) => p.FindFirstValue(ClaimTypes.Role) ?? "";
    private static IResult Invalid(string message) => Results.BadRequest(new { code = "INVALID_REQUEST", message });
    private static IResult Conflict(string message) => Results.Conflict(new { code = "CONFLICT", message });

    private static Task<User?> LockStudent(StudentProjectsDbContext db, Guid id, CancellationToken ct) =>
        db.Database.IsSqlServer()
            ? db.Users.FromSqlInterpolated($"SELECT * FROM [Users] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {id}").SingleOrDefaultAsync(ct)
            : db.Users.FindAsync([id], ct).AsTask();

    private static Task<LecturerCapacity?> LockCapacity(StudentProjectsDbContext db, Guid lecturerId, Guid periodId, CancellationToken ct) =>
        db.Database.IsSqlServer()
            ? db.LecturerCapacities.FromSqlInterpolated($"SELECT * FROM [LecturerCapacities] WITH (UPDLOCK, HOLDLOCK) WHERE [LecturerUserId] = {lecturerId} AND [RegistrationPeriodId] = {periodId}").SingleOrDefaultAsync(ct)
            : db.LecturerCapacities.SingleOrDefaultAsync(c => c.LecturerUserId == lecturerId && c.RegistrationPeriodId == periodId, ct);

    // A single maximum per lecturer is shared by all periods. Active Projects, not
    // period-specific capacity counters, are authoritative for occupied places.
    private static async Task<(int Max, int Current)> GlobalCapacity(StudentProjectsDbContext db,
        Guid lecturerId, CancellationToken ct)
    {
        // Existing period-specific settings may contain a reduced "remaining" value
        // in a newer period. Adopt the highest configured maximum, not the latest row.
        var max = await db.LecturerCapacities.AsNoTracking()
            .Where(c => c.LecturerUserId == lecturerId)
            .Select(c => (int?)c.MaxStudents).MaxAsync(ct) ?? 0;
        var current = await db.Projects.CountAsync(p => p.LecturerUserId == lecturerId &&
            p.Status != ProjectStatus.CANCELLED && p.Status != ProjectStatus.COMPLETED, ct);
        return (max, current);
    }

    private static Task<Topic?> LockTopic(StudentProjectsDbContext db, Guid topicId, CancellationToken ct) =>
        db.Database.IsSqlServer()
            ? db.Topics.FromSqlInterpolated($"SELECT * FROM [Topics] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {topicId}").SingleOrDefaultAsync(ct)
            : db.Topics.FindAsync([topicId], ct).AsTask();

    private static async Task<bool> StudentOwnsTopic(StudentProjectsDbContext db, Guid studentId, Guid topicId, Guid periodId, CancellationToken ct)
    {
        var topic = await db.Topics.AsNoTracking().SingleOrDefaultAsync(t => t.Id == topicId, ct);
        if (topic is null) return false;
        var ownsOwnProposal = topic.ProposedByUserId == studentId &&
            (topic.Status == TopicStatus.APPROVED || topic.Status == TopicStatus.IN_PROGRESS || topic.Status == TopicStatus.COMPLETED) &&
            await db.Users.AnyAsync(u => u.Id == studentId && u.RoleId == RoleIds.Student, ct);
        if (ownsOwnProposal) return true;
        return await db.TopicRegistrations.AnyAsync(r => r.TopicId == topicId && r.StudentUserId == studentId &&
            r.RegistrationPeriodId == periodId && r.Status == RequestStatus.ACCEPTED, ct);
    }

    private static async Task<IResult> ListCapacity(Guid? periodId, StudentProjectsDbContext db, CancellationToken ct)
    {
        var periods = db.RegistrationPeriods.AsNoTracking();
        var selectedPeriod = periodId.HasValue
            ? await periods.SingleOrDefaultAsync(p => p.Id == periodId, ct)
            : await periods.Where(p => p.IsOpen && p.StartsAt <= DateTimeOffset.UtcNow && p.EndsAt > DateTimeOffset.UtcNow)
                .OrderByDescending(p => p.StartsAt).FirstOrDefaultAsync(ct);
        if (selectedPeriod is null) return periodId.HasValue ? Results.NotFound() : Results.Ok(Array.Empty<object>());
        var lecturers = await (from u in db.Users.AsNoTracking()
            join profile in db.Lecturers.AsNoTracking() on u.Id equals profile.UserId into profiles
            from p in profiles.DefaultIfEmpty()
            where u.RoleId == RoleIds.Lecturer && u.IsActive
            orderby u.FullName
            select new { u.Id, u.FullName, Specialty = p == null ? null : p.Specialty }).ToListAsync(ct);
        var capacities = await db.LecturerCapacities.AsNoTracking().ToListAsync(ct);
        var activeProjects = await db.Projects.AsNoTracking()
            .Where(p => p.Status != ProjectStatus.CANCELLED && p.Status != ProjectStatus.COMPLETED)
            .GroupBy(p => p.LecturerUserId)
            .Select(g => new { lecturerId = g.Key, count = g.Count() }).ToListAsync(ct);
        return Results.Ok(lecturers.Select(u =>
        {
            var max = capacities.Where(x => x.LecturerUserId == u.Id)
                .Select(x => x.MaxStudents).DefaultIfEmpty(0).Max();
            var count = activeProjects.FirstOrDefault(x => x.lecturerId == u.Id)?.count ?? 0;
            return new { lecturerUserId = u.Id, lecturerName = u.FullName, specialty = u.Specialty,
                registrationPeriodId = selectedPeriod.Id, periodName = selectedPeriod.Name,
                maxStudents = max, currentStudents = count, remaining = Math.Max(0, max - count),
                status = max > count ? "AVAILABLE" : "FULL" };
        }).ToArray());
    }

    private static async Task<IResult> UpdateCapacity(CapacityUpdateV06? body, StudentProjectsDbContext db,
        ClaimsPrincipal principal, CancellationToken ct)
    {
        if (body is null || body.RegistrationPeriodId == Guid.Empty || body.MaxStudents < 0 || body.MaxStudents > 1000)
            return Invalid("Registration period and maximum students (0 to 1000) are required.");
        if (!await db.RegistrationPeriods.AnyAsync(p => p.Id == body.RegistrationPeriodId, ct)) return Results.NotFound();
        var actor = Actor(principal);
        await using var tx = db.Database.IsSqlServer()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        // Lock the lecturer row before inserting/updating a capacity to serialize first-time setup.
        if (await LockStudent(db, actor, ct) is null) return Results.NotFound();
        var all = await db.LecturerCapacities.Where(c => c.LecturerUserId == actor).ToListAsync(ct);
        var current = await db.Projects.CountAsync(p => p.LecturerUserId == actor &&
            p.Status != ProjectStatus.CANCELLED && p.Status != ProjectStatus.COMPLETED, ct);
        if (body.MaxStudents < current) return Conflict($"Cannot set capacity below {current} active projects.");
        var capacity = all.FirstOrDefault(c => c.RegistrationPeriodId == body.RegistrationPeriodId);
        if (capacity is null)
        {
            capacity = new LecturerCapacity { LecturerUserId = actor,
                RegistrationPeriodId = body.RegistrationPeriodId,
                MaxStudents = body.MaxStudents, CurrentStudents = current };
            db.LecturerCapacities.Add(capacity);
        }
        foreach (var c in all)
        {
            c.MaxStudents = body.MaxStudents;
            // The stored counter is only a cache; all decisions use Projects directly.
            c.CurrentStudents = current;
            c.UpdatedAt = DateTimeOffset.UtcNow;
        }
        db.AuditEntries.Add(new AuditEntry { ActorUserId = actor, Action = "LECTURER_CAPACITY_UPDATED",
            EntityName = "LecturerCapacity", EntityId = capacity.Id });
        await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return Results.Ok(new { capacity.Id, capacity.RegistrationPeriodId, capacity.MaxStudents,
            currentStudents = current, remaining = body.MaxStudents - current });
    }

    private static async Task<IResult> ListRequests(StudentProjectsDbContext db, ClaimsPrincipal principal, CancellationToken ct)
    {
        var actor = Actor(principal);
        var role = Role(principal);
        var query = db.LecturerRequests.AsNoTracking().AsQueryable();
        if (role == "Student") query = query.Where(r => r.StudentUserId == actor);
        else if (role == "Lecturer") query = query.Where(r => r.LecturerUserId == actor);
        else if (role != "Admin") return Results.Forbid();
        var results = await (from r in query
            join s in db.Users.AsNoTracking() on r.StudentUserId equals s.Id
            join l in db.Users.AsNoTracking() on r.LecturerUserId equals l.Id
            join t in db.Topics.AsNoTracking() on r.TopicId equals t.Id
            join p in db.RegistrationPeriods.AsNoTracking() on r.RegistrationPeriodId equals p.Id
            orderby r.CreatedAt descending
            select new { r.Id, r.TopicId, topicTitle = t.Title, r.StudentUserId, studentName = s.FullName,
                r.LecturerUserId, lecturerName = l.FullName, r.RegistrationPeriodId, periodName = p.Name,
                status = r.Status.ToString(), r.RejectionReason, r.CreatedAt, r.IsCombined,
                r.DraftTitle, r.DraftDescription, r.DraftObjective, r.DraftExpectedContent,
                r.DraftProposedTechnology }).ToListAsync(ct);
        return Results.Ok(results);
    }

    private static async Task<IResult> CreateRequest(LecturerRequestCreateV06? body, StudentProjectsDbContext db,
        ClaimsPrincipal principal, CancellationToken ct)
    {
        if (body is null || body.TopicId == Guid.Empty || body.LecturerUserId == Guid.Empty || body.RegistrationPeriodId == Guid.Empty)
            return Invalid("Topic, lecturer and registration period are required.");
        var actor = Actor(principal);
        await using var tx = db.Database.IsSqlServer()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        if (await LockStudent(db, actor, ct) is null) return Results.NotFound();
        var now = DateTimeOffset.UtcNow;
        if (!await db.RegistrationPeriods.AnyAsync(p => p.Id == body.RegistrationPeriodId && p.IsOpen &&
            p.StartsAt <= now && p.EndsAt > now, ct)) return Conflict("Registration period is not currently open.");
        if (!await StudentOwnsTopic(db, actor, body.TopicId, body.RegistrationPeriodId, ct))
            return Conflict("Student must own the selected topic before requesting a supervisor.");
        if (await db.Projects.AnyAsync(p => p.StudentUserId == actor && p.Status != ProjectStatus.CANCELLED, ct) ||
            await db.LecturerRequests.AnyAsync(r => r.StudentUserId == actor && r.Status == RequestStatus.ACCEPTED, ct))
            return Conflict("Student already has an accepted supervisor.");
        if (!await db.Users.AnyAsync(u => u.Id == body.LecturerUserId && u.IsActive && u.RoleId == RoleIds.Lecturer, ct))
            return Results.NotFound();
        var (maximum, occupied) = await GlobalCapacity(db, body.LecturerUserId, ct);
        if (maximum <= occupied) return Conflict("Lecturer has no available capacity.");
        if (await db.LecturerRequests.AnyAsync(r => r.StudentUserId == actor && r.TopicId == body.TopicId &&
            r.LecturerUserId == body.LecturerUserId && r.RegistrationPeriodId == body.RegistrationPeriodId &&
            (r.Status == RequestStatus.PENDING || r.Status == RequestStatus.OFFERED ||
                r.Status == RequestStatus.REVISION_REQUIRED), ct)) return Conflict("An active request to this lecturer already exists.");
        var request = new LecturerRequest { StudentUserId = actor, TopicId = body.TopicId,
            LecturerUserId = body.LecturerUserId, RegistrationPeriodId = body.RegistrationPeriodId };
        db.LecturerRequests.Add(request);
        db.AuditEntries.Add(new AuditEntry { ActorUserId = actor, Action = "LECTURER_REQUEST_SUBMITTED",
            EntityName = "LecturerRequest", EntityId = request.Id });
        await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return Results.Created($"/api/v1/lecturer-requests/{request.Id}", new { request.Id, status = "PENDING" });
    }

    private static bool ValidDraft(TopicDraftV061? d) => d is not null &&
        !string.IsNullOrWhiteSpace(d.Title) && d.Title.Trim().Length <= 300 &&
        !string.IsNullOrWhiteSpace(d.Description) && d.Description.Trim().Length <= 4000 &&
        !string.IsNullOrWhiteSpace(d.Objective) && d.Objective.Trim().Length <= 2000 &&
        !string.IsNullOrWhiteSpace(d.ExpectedContent) && d.ExpectedContent.Trim().Length <= 2000 &&
        !string.IsNullOrWhiteSpace(d.ProposedTechnology) && d.ProposedTechnology.Trim().Length <= 1000;

    private static void ApplyDraft(LecturerRequest r, TopicDraftV061 d)
    {
        r.DraftTitle = d.Title!.Trim(); r.DraftDescription = d.Description!.Trim();
        r.DraftObjective = d.Objective!.Trim(); r.DraftExpectedContent = d.ExpectedContent!.Trim();
        r.DraftProposedTechnology = d.ProposedTechnology!.Trim();
    }

    private static void ApplyDraft(Topic t, LecturerRequest r)
    {
        t.Title = r.DraftTitle!; t.Description = r.DraftDescription!;
        t.Objective = r.DraftObjective; t.ExpectedContent = r.DraftExpectedContent;
        t.ProposedTechnology = r.DraftProposedTechnology; t.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static async Task<bool> HasTopic(StudentProjectsDbContext db, Guid studentId, CancellationToken ct) =>
        await db.Topics.AnyAsync(t => t.ProposedByUserId == studentId &&
            (t.Status == TopicStatus.APPROVED || t.Status == TopicStatus.IN_PROGRESS || t.Status == TopicStatus.COMPLETED), ct) ||
        await db.TopicRegistrations.AnyAsync(r => r.StudentUserId == studentId && r.Status == RequestStatus.ACCEPTED, ct);

    // Joint applications remain unassigned until the student chooses an offered supervisor.
    private static async Task<IResult> SubmitCombined(CombinedApplicationV061? body, StudentProjectsDbContext db,
        ClaimsPrincipal principal, CancellationToken ct)
    {
        if (body is null || body.LecturerUserId == Guid.Empty || body.RegistrationPeriodId == Guid.Empty ||
            (body.TopicId.HasValue && body.TopicId.Value == Guid.Empty))
            return Invalid("Select a lecturer, registration period and topic (or create a proposal).");
        if (body.Draft is not null && !ValidDraft(body.Draft)) return Invalid("Complete all five topic draft fields.");
        var actor = Actor(principal);
        await using var tx = db.Database.IsSqlServer()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        if (await LockStudent(db, actor, ct) is null) return Results.NotFound();
        var now = DateTimeOffset.UtcNow;
        if (!await db.RegistrationPeriods.AnyAsync(p => p.Id == body.RegistrationPeriodId && p.IsOpen &&
            p.StartsAt <= now && p.EndsAt > now, ct)) return Conflict("Registration period is not open.");
        if (await HasTopic(db, actor, ct) ||
            await db.Projects.AnyAsync(p => p.StudentUserId == actor && p.Status != ProjectStatus.CANCELLED, ct))
            return Conflict("You already own a topic or an active project.");
        if (!await db.Users.AnyAsync(u => u.Id == body.LecturerUserId && u.IsActive && u.RoleId == RoleIds.Lecturer, ct))
            return Results.NotFound();
        var (maximum, occupied) = await GlobalCapacity(db, body.LecturerUserId, ct);
        if (maximum <= occupied) return Conflict("Lecturer has no available capacity.");
        Topic topic;
        if (body.TopicId.HasValue)
        {
            topic = (await LockTopic(db, body.TopicId.Value, ct))!;
            if (topic is null) return Results.NotFound();
            var ownUnassignedProposal = topic.ProposedByUserId == actor &&
                topic.Status == TopicStatus.PENDING_APPROVAL &&
                await db.LecturerRequests.AnyAsync(r => r.TopicId == topic.Id && r.StudentUserId == actor &&
                    r.IsCombined && (r.Status == RequestStatus.PENDING || r.Status == RequestStatus.OFFERED ||
                        r.Status == RequestStatus.REVISION_REQUIRED), ct);
            if (!ownUnassignedProposal &&
                (topic.Status is not (TopicStatus.APPROVED or TopicStatus.PUBLISHED) || !topic.IsRegistrationOpen ||
                await db.TopicRegistrations.AnyAsync(r => r.TopicId == topic.Id && r.Status == RequestStatus.ACCEPTED, ct)))
                return Conflict("This topic is unavailable for new applications.");
        }
        else
        {
            if (!ValidDraft(body.Draft)) return Invalid("Complete all five proposal fields.");
            topic = new Topic { ProposedByUserId = actor, Status = TopicStatus.PENDING_APPROVAL,
                IsRegistrationOpen = false };
            topic.Title = body.Draft!.Title!.Trim(); topic.Description = body.Draft.Description!.Trim();
            topic.Objective = body.Draft.Objective!.Trim(); topic.ExpectedContent = body.Draft.ExpectedContent!.Trim();
            topic.ProposedTechnology = body.Draft.ProposedTechnology!.Trim();
            db.Topics.Add(topic);
            db.TopicStateHistories.Add(new TopicStateHistory { TopicId = topic.Id, ActorUserId = actor,
                FromStatus = TopicStatus.DRAFT, ToStatus = TopicStatus.PENDING_APPROVAL });
        }
        if (await db.LecturerRequests.AnyAsync(r => r.StudentUserId == actor && r.TopicId == topic.Id &&
            r.LecturerUserId == body.LecturerUserId &&
            (r.Status == RequestStatus.PENDING || r.Status == RequestStatus.REVISION_REQUIRED ||
                r.Status == RequestStatus.OFFERED), ct))
            return Conflict("You already have an open request for this topic and lecturer.");
        var application = new LecturerRequest { StudentUserId = actor, LecturerUserId = body.LecturerUserId,
            RegistrationPeriodId = body.RegistrationPeriodId, TopicId = topic.Id, IsCombined = true };
        ApplyDraft(application, body.Draft is not null && ValidDraft(body.Draft) ? body.Draft : new TopicDraftV061(
            topic.Title, topic.Description, topic.Objective ?? "Chưa xác định", topic.ExpectedContent ?? "Chưa xác định",
            topic.ProposedTechnology ?? "Chưa xác định"));
        db.LecturerRequests.Add(application);
        db.AuditEntries.Add(new AuditEntry { ActorUserId = actor, Action = "JOINT_TOPIC_LECTURER_SUBMITTED",
            EntityName = "LecturerRequest", EntityId = application.Id });
        await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return Results.Created($"/api/v1/lecturer-requests/{application.Id}",
            new { application.Id, topicId = topic.Id, status = "PENDING", combined = true });
    }

    // A student may cancel a pending/revision/rejected joint application.
    // Self-created proposals are deleted with all dependent rows; public topics stay intact.
    private static async Task<IResult> CancelRequest(Guid id, StudentProjectsDbContext db,
        ClaimsPrincipal principal, CancellationToken ct)
    {
        var actor = Actor(principal);
        var candidate = await db.LecturerRequests.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, ct);
        if (candidate is null) return Results.NotFound();
        if (candidate.StudentUserId != actor) return Results.Forbid();
        if (candidate.Status is not (RequestStatus.PENDING or RequestStatus.OFFERED or RequestStatus.REVISION_REQUIRED or RequestStatus.REJECTED))
            return Conflict("This application is no longer cancellable.");
        await using var tx = db.Database.IsSqlServer()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        if (await LockStudent(db, actor, ct) is null) return Results.NotFound();
        var request = await db.LecturerRequests.SingleOrDefaultAsync(r => r.Id == id, ct);
        if (request is null) return Results.NotFound();
        if (request.StudentUserId != actor) return Results.Forbid();
        if (request.Status is not (RequestStatus.PENDING or RequestStatus.OFFERED or RequestStatus.REVISION_REQUIRED or RequestStatus.REJECTED))
            return Conflict("This application is no longer cancellable.");
        var topic = await LockTopic(db, request.TopicId, ct);
        if (topic is null) return Results.NotFound();
        var anotherLiveRequest = await db.LecturerRequests.AnyAsync(r => r.TopicId == topic.Id && r.Id != id &&
            (r.Status == RequestStatus.PENDING || r.Status == RequestStatus.OFFERED ||
             r.Status == RequestStatus.REVISION_REQUIRED), ct);
        var isOwnProposal = request.IsCombined && topic.ProposedByUserId == actor && !anotherLiveRequest &&
            (topic.Status == TopicStatus.PENDING_APPROVAL || topic.Status == TopicStatus.REJECTED);
        if (isOwnProposal)
        {
            if (await db.Projects.AnyAsync(p => p.TopicId == topic.Id, ct) ||
                await db.TopicRegistrations.AnyAsync(r => r.TopicId == topic.Id && r.Status == RequestStatus.ACCEPTED, ct) ||
                await db.LecturerRequests.AnyAsync(r => r.TopicId == topic.Id && r.Status == RequestStatus.ACCEPTED, ct))
                return Conflict("The proposal is assigned and cannot be deleted.");
            // A self-created joint proposal is private. Remove its linked requests as well.
            var related = await db.LecturerRequests.Where(r => r.TopicId == topic.Id).ToListAsync(ct);
            if (related.Any(r => r.StudentUserId != actor))
                return Conflict("Proposal has requests from other students and cannot be deleted.");
            db.LecturerRequests.RemoveRange(related);
            db.TopicRegistrations.RemoveRange(await db.TopicRegistrations.Where(r => r.TopicId == topic.Id).ToListAsync(ct));
            db.TopicStateHistories.RemoveRange(await db.TopicStateHistories.Where(h => h.TopicId == topic.Id).ToListAsync(ct));
            db.Topics.Remove(topic);
        }
        else
        {
            request.Status = RequestStatus.CANCELLED;
            request.UpdatedAt = DateTimeOffset.UtcNow;
        }
        db.AuditEntries.Add(new AuditEntry { ActorUserId = actor,
            Action = isOwnProposal ? "STUDENT_JOINT_PROPOSAL_CANCELLED_DELETED" : "LECTURER_REQUEST_CANCELLED",
            EntityName = isOwnProposal ? "Topic" : "LecturerRequest",
            EntityId = isOwnProposal ? topic.Id : request.Id });
        await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return Results.Ok(new { requestId = id, topicId = topic.Id, status = isOwnProposal ? "DELETED" : "CANCELLED",
            deletedTopic = isOwnProposal });
    }

    private static async Task<IResult> RequestRevision(Guid id, RevisionRequestV061? body, StudentProjectsDbContext db,
        ClaimsPrincipal principal, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body?.Reason) || body.Reason.Trim().Length > 2000)
            return Invalid("Revision instructions are required (max 2000 characters).");
        var candidate = await db.LecturerRequests.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, ct);
        if (candidate is null) return Results.NotFound();
        if (candidate.LecturerUserId != Actor(principal)) return Results.Forbid();
        await using var tx = db.Database.IsSqlServer()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        if (await LockStudent(db, candidate.StudentUserId, ct) is null) return Results.NotFound();
        var request = await db.LecturerRequests.FindAsync([id], ct);
        if (request is null) return Results.NotFound();
        if (!request.IsCombined || request.Status != RequestStatus.PENDING)
            return Conflict("Only pending joint applications can be sent back for revision.");
        request.Status = RequestStatus.REVISION_REQUIRED;
        request.RejectionReason = body!.Reason!.Trim();
        request.UpdatedAt = DateTimeOffset.UtcNow;
        db.AuditEntries.Add(new AuditEntry { ActorUserId = Actor(principal), Action = "JOINT_APPLICATION_REVISION_REQUIRED",
            EntityName = "LecturerRequest", EntityId = id });
        await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return Results.Ok(new { request.Id, status = "REVISION_REQUIRED", request.RejectionReason });
    }

    private static async Task<IResult> ResubmitCombined(Guid id, ResubmitApplicationV061? body, StudentProjectsDbContext db,
        ClaimsPrincipal principal, CancellationToken ct)
    {
        if (!ValidDraft(body?.Draft)) return Invalid("Complete all five revised topic fields.");
        var candidate = await db.LecturerRequests.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, ct);
        if (candidate is null) return Results.NotFound();
        if (candidate.StudentUserId != Actor(principal)) return Results.Forbid();
        await using var tx = db.Database.IsSqlServer()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        if (await LockStudent(db, candidate.StudentUserId, ct) is null) return Results.NotFound();
        var request = await db.LecturerRequests.FindAsync([id], ct);
        if (request is null) return Results.NotFound();
        if (!request.IsCombined || request.Status != RequestStatus.REVISION_REQUIRED)
            return Conflict("This application is not awaiting your revision.");
        if (await HasTopic(db, request.StudentUserId, ct) ||
            await db.Projects.AnyAsync(p => p.StudentUserId == request.StudentUserId && p.Status != ProjectStatus.CANCELLED, ct))
            return Conflict("You already have an assigned topic/project.");
        ApplyDraft(request, body!.Draft!);
        request.Status = RequestStatus.PENDING;
        request.RejectionReason = null;
        request.UpdatedAt = DateTimeOffset.UtcNow;
        db.AuditEntries.Add(new AuditEntry { ActorUserId = Actor(principal), Action = "JOINT_APPLICATION_RESUBMITTED",
            EntityName = "LecturerRequest", EntityId = id });
        await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return Results.Ok(new { request.Id, status = "PENDING" });
    }

    // Lecturer offers supervision; student may receive offers from several lecturers.
    // An offer does not create a Project or consume a supervision place.
    private static async Task<IResult> AcceptRequest(Guid id, StudentProjectsDbContext db,
        ClaimsPrincipal principal, CancellationToken ct)
    {
        var lecturerId = Actor(principal);
        var candidate = await db.LecturerRequests.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, ct);
        if (candidate is null) return Results.NotFound();
        if (candidate.LecturerUserId != lecturerId) return Results.Forbid();
        await using var tx = db.Database.IsSqlServer()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        if (await LockStudent(db, candidate.StudentUserId, ct) is null) return Results.NotFound();
        if (await LockStudent(db, lecturerId, ct) is null) return Results.NotFound();
        var request = await db.LecturerRequests.SingleOrDefaultAsync(r => r.Id == id, ct);
        if (request is null) return Results.NotFound();
        if (request.LecturerUserId != lecturerId) return Results.Forbid();
        if (request.Status != RequestStatus.PENDING) return Conflict("Request is no longer pending.");
        if (await db.Projects.AnyAsync(p => p.StudentUserId == request.StudentUserId &&
            p.Status != ProjectStatus.CANCELLED, ct) ||
            await db.LecturerRequests.AnyAsync(r => r.StudentUserId == request.StudentUserId &&
            r.Status == RequestStatus.ACCEPTED, ct)) return Conflict("Student already selected a supervisor.");
        if (!await db.Users.AnyAsync(u => u.Id == lecturerId && u.RoleId == RoleIds.Lecturer && u.IsActive, ct))
            return Conflict("Lecturer account is inactive.");
        var (maximum, occupied) = await GlobalCapacity(db, lecturerId, ct);
        if (maximum <= occupied) return Conflict("Lecturer has no available capacity.");
        if (!request.IsCombined && !await StudentOwnsTopic(db, request.StudentUserId,
            request.TopicId, request.RegistrationPeriodId, ct))
            return Conflict("Student no longer owns the topic.");
        var topic = await db.Topics.AsNoTracking().SingleOrDefaultAsync(t => t.Id == request.TopicId, ct);
        if (topic is null) return Results.NotFound();
        if (request.IsCombined && !(topic.ProposedByUserId == request.StudentUserId &&
            topic.Status == TopicStatus.PENDING_APPROVAL) &&
            !((topic.Status == TopicStatus.APPROVED || topic.Status == TopicStatus.PUBLISHED) &&
              topic.IsRegistrationOpen))
            return Conflict("Topic is no longer available.");
        request.Status = RequestStatus.OFFERED;
        request.UpdatedAt = DateTimeOffset.UtcNow;
        db.AuditEntries.Add(new AuditEntry { ActorUserId = lecturerId,
            Action = "LECTURER_OFFERED_SUPERVISION", EntityName = "LecturerRequest", EntityId = request.Id });
        await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return Results.Ok(new { request.Id, status = "OFFERED", projectCreated = false });
    }

    private static async Task<IResult> SelectOffer(Guid id, StudentProjectsDbContext db, ClaimsPrincipal principal, CancellationToken ct)
    {
        var studentId = Actor(principal);
        var candidate = await db.LecturerRequests.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, ct);
        if (candidate is null) return Results.NotFound();
        if (candidate.StudentUserId != studentId) return Results.Forbid();
        await using var tx = db.Database.IsSqlServer()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        // Serialize final choices by this student before creating a Project.
        if (await LockStudent(db, candidate.StudentUserId, ct) is null) return Results.NotFound();
        var request = await db.LecturerRequests.SingleOrDefaultAsync(r => r.Id == id, ct);
        if (request is null) return Results.NotFound();
        if (request.StudentUserId != studentId) return Results.Forbid();
        if (request.Status != RequestStatus.OFFERED) return Conflict("Lecturer has not offered supervision for this request.");
        var lecturerId = request.LecturerUserId;
        // Lock lecturer as well as student before checking global occupied places.
        if (await LockStudent(db, lecturerId, ct) is null) return Results.NotFound();
        var (maxStudents, occupied) = await GlobalCapacity(db, lecturerId, ct);
        if (occupied >= maxStudents) return Conflict("Lecturer has reached their global supervision limit.");
        var topic = await LockTopic(db, request.TopicId, ct);
        if (topic is null) return Results.NotFound();
        var ownsTopic = await StudentOwnsTopic(db, request.StudentUserId, request.TopicId, request.RegistrationPeriodId, ct);
        if (!request.IsCombined && !ownsTopic) return Conflict("Student no longer owns this topic.");
        if (request.IsCombined)
        {
            if (await HasTopic(db, request.StudentUserId, ct)) return Conflict("Student already owns a topic.");
            var studentProposal = topic.ProposedByUserId == request.StudentUserId &&
                topic.Status == TopicStatus.PENDING_APPROVAL;
            var publicTopic = topic.Status is TopicStatus.APPROVED or TopicStatus.PUBLISHED;
            var changed = topic.Title != request.DraftTitle || topic.Description != request.DraftDescription ||
                (topic.Objective ?? "Chưa xác định") != request.DraftObjective ||
                (topic.ExpectedContent ?? "Chưa xác định") != request.DraftExpectedContent ||
                (topic.ProposedTechnology ?? "Chưa xác định") != request.DraftProposedTechnology;
            if (!studentProposal && !publicTopic) return Conflict("Requested topic no longer exists in a valid state.");
            if (publicTopic && !changed && !topic.IsRegistrationOpen)
                return Conflict("This public topic has been closed/assigned.");
            if (publicTopic && !changed && await db.TopicRegistrations.AnyAsync(r => r.TopicId == topic.Id &&
                r.Status == RequestStatus.ACCEPTED, ct)) return Conflict("Requested topic is already assigned.");
        }
        if (await db.Projects.AnyAsync(p => p.StudentUserId == request.StudentUserId && p.Status != ProjectStatus.CANCELLED, ct) ||
            await db.LecturerRequests.AnyAsync(r => r.StudentUserId == request.StudentUserId &&
                r.Status == RequestStatus.ACCEPTED, ct)) return Conflict("Student already has a supervisor.");
        if (!await db.Users.AnyAsync(u => u.Id == lecturerId && u.RoleId == RoleIds.Lecturer && u.IsActive, ct))
            return Conflict("Lecturer account is inactive.");
        if (request.IsCombined)
        {
            var priorChoices = await db.TopicRegistrations.Where(r =>
                r.StudentUserId == request.StudentUserId).ToListAsync(ct);
            TopicRegistration? acceptedChoice = null;
            var isPersonalProposal = topic.ProposedByUserId == request.StudentUserId &&
                topic.Status == TopicStatus.PENDING_APPROVAL;
            if (isPersonalProposal)
            {
                ApplyDraft(topic, request);
                topic.Status = TopicStatus.APPROVED; topic.IsRegistrationOpen = false;
                db.TopicStateHistories.Add(new TopicStateHistory { TopicId = topic.Id,
                    ActorUserId = lecturerId, FromStatus = TopicStatus.PENDING_APPROVAL,
                    ToStatus = TopicStatus.APPROVED });
            }
            else
            {
                // Revised a shared topic: create a personal approved copy without editing the public source.
                var changed = topic.Title != request.DraftTitle || topic.Description != request.DraftDescription ||
                    (topic.Objective ?? "Chưa xác định") != request.DraftObjective ||
                    (topic.ExpectedContent ?? "Chưa xác định") != request.DraftExpectedContent ||
                    (topic.ProposedTechnology ?? "Chưa xác định") != request.DraftProposedTechnology;
                if (changed)
                {
                    var personal = new Topic { ProposedByUserId = request.StudentUserId,
                        Status = TopicStatus.APPROVED, IsRegistrationOpen = false };
                    ApplyDraft(personal, request);
                    db.Topics.Add(personal);
                    db.TopicStateHistories.Add(new TopicStateHistory { TopicId = personal.Id,
                        ActorUserId = lecturerId, FromStatus = TopicStatus.PENDING_APPROVAL,
                        ToStatus = TopicStatus.APPROVED, Reason = "Revised from a public topic during joint review" });
                    topic = personal;
                    request.TopicId = personal.Id;
                }
                else
                {
                    topic.IsRegistrationOpen = false;
                    // Reuse an earlier PENDING topic application, if present, rather than
                    // violate the existing filtered unique index by inserting another row.
                    acceptedChoice = priorChoices.FirstOrDefault(r => r.TopicId == topic.Id &&
                        r.Status == RequestStatus.PENDING);
                    if (acceptedChoice is not null)
                    {
                        acceptedChoice.Status = RequestStatus.ACCEPTED;
                        acceptedChoice.UpdatedAt = DateTimeOffset.UtcNow;
                    }
                    else db.TopicRegistrations.Add(new TopicRegistration { StudentUserId = request.StudentUserId,
                        TopicId = topic.Id, RegistrationPeriodId = request.RegistrationPeriodId,
                        Status = RequestStatus.ACCEPTED });
                    var competitors = await db.TopicRegistrations.Where(r => r.TopicId == topic.Id &&
                        r.StudentUserId != request.StudentUserId && r.Status == RequestStatus.PENDING).ToListAsync(ct);
                    foreach (var competitor in competitors) { competitor.Status = RequestStatus.REJECTED;
                        competitor.UpdatedAt = DateTimeOffset.UtcNow; }
                }
            }
            // Only the accepted choice is retained for this student.
            db.TopicRegistrations.RemoveRange(priorChoices.Where(r => r != acceptedChoice));
        }
        request.Status = RequestStatus.ACCEPTED;
        request.UpdatedAt = DateTimeOffset.UtcNow;
        var otherRequests = await db.LecturerRequests.Where(r => r.StudentUserId == request.StudentUserId &&
            r.Id != id && (r.Status == RequestStatus.PENDING || r.Status == RequestStatus.OFFERED ||
            r.Status == RequestStatus.REVISION_REQUIRED)).ToListAsync(ct);
        foreach (var other in otherRequests)
        {
            other.Status = RequestStatus.CANCELLED;
            other.UpdatedAt = DateTimeOffset.UtcNow;
        }
        if (request.IsCombined)
        {
            var otherIds = otherRequests.Where(r => r.IsCombined).Select(r => r.TopicId).Distinct().ToList();
            var otherOwnDrafts = await db.Topics.Where(t => otherIds.Contains(t.Id) &&
                t.Id != topic.Id && t.ProposedByUserId == request.StudentUserId &&
                t.Status == TopicStatus.PENDING_APPROVAL)
                .ToListAsync(ct);
            foreach (var otherDraft in otherOwnDrafts)
            {
                otherDraft.Status = TopicStatus.CANCELLED;
                otherDraft.UpdatedAt = DateTimeOffset.UtcNow;
                db.TopicStateHistories.Add(new TopicStateHistory { TopicId = otherDraft.Id,
                    ActorUserId = lecturerId, FromStatus = TopicStatus.PENDING_APPROVAL,
                    ToStatus = TopicStatus.CANCELLED, Reason = "Another joint application was accepted" });
            }
        }
        if (request.IsCombined && !topic.IsRegistrationOpen)
        {
            // Other students applying for this SAME assigned shared topic lose that choice.
            // Do not reject candidates for a public original when an edited personal copy was chosen.
            var competitors = await db.LecturerRequests.Where(r => r.TopicId == topic.Id &&
                r.StudentUserId != request.StudentUserId && r.IsCombined &&
                (r.Status == RequestStatus.PENDING || r.Status == RequestStatus.OFFERED ||
                 r.Status == RequestStatus.REVISION_REQUIRED)).ToListAsync(ct);
            foreach (var other in competitors)
            {
                other.Status = RequestStatus.REJECTED;
                other.RejectionReason = "This topic was assigned to another student.";
                other.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }
        var project = new Project { StudentUserId = request.StudentUserId,
            LecturerUserId = lecturerId, TopicId = topic.Id, RegistrationPeriodId = request.RegistrationPeriodId,
            AcceptedLecturerRequestId = request.Id, Status = ProjectStatus.REGISTERED };
        db.Projects.Add(project);
        db.AuditEntries.Add(new AuditEntry { ActorUserId = studentId, Action = "STUDENT_SELECTED_LECTURER_PROJECT_CREATED",
            EntityName = "Project", EntityId = project.Id });
        await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return Results.Ok(new { request.Id, status = "ACCEPTED", projectId = project.Id,
            cancelledOtherRequests = otherRequests.Count, currentStudents = occupied + 1 });
    }

    private static async Task<IResult> RejectRequest(Guid id, LecturerRequestRejectV06? body, StudentProjectsDbContext db,
        ClaimsPrincipal principal, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body?.Reason) || body.Reason.Trim().Length > 2000)
            return Invalid("Rejection reason is required (max 2000 characters).");
        var candidate = await db.LecturerRequests.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, ct);
        if (candidate is null) return Results.NotFound();
        if (candidate.LecturerUserId != Actor(principal)) return Results.Forbid();
        await using var tx = db.Database.IsSqlServer()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        // Reject and Accept use the same student lock so they cannot overwrite each other.
        if (await LockStudent(db, candidate.StudentUserId, ct) is null) return Results.NotFound();
        var request = await db.LecturerRequests.SingleOrDefaultAsync(r => r.Id == id, ct);
        if (request is null) return Results.NotFound();
        if (request.LecturerUserId != Actor(principal)) return Results.Forbid();
        if (request.Status != RequestStatus.PENDING) return Conflict("Request is no longer pending.");
        request.Status = RequestStatus.REJECTED;
        request.RejectionReason = body!.Reason!.Trim();
        request.UpdatedAt = DateTimeOffset.UtcNow;
        if (request.IsCombined)
        {
            var topic = await LockTopic(db, request.TopicId, ct);
            if (topic is not null && topic.ProposedByUserId == request.StudentUserId &&
                topic.Status == TopicStatus.PENDING_APPROVAL &&
                !await db.LecturerRequests.AnyAsync(r => r.TopicId == topic.Id && r.Id != request.Id &&
                    (r.Status == RequestStatus.PENDING || r.Status == RequestStatus.OFFERED ||
                     r.Status == RequestStatus.REVISION_REQUIRED), ct))
            {
                topic.Status = TopicStatus.REJECTED; topic.UpdatedAt = DateTimeOffset.UtcNow;
                db.TopicStateHistories.Add(new TopicStateHistory { TopicId = topic.Id,
                    ActorUserId = Actor(principal), FromStatus = TopicStatus.PENDING_APPROVAL,
                    ToStatus = TopicStatus.REJECTED, Reason = request.RejectionReason });
            }
        }
        db.AuditEntries.Add(new AuditEntry { ActorUserId = Actor(principal), Action = "LECTURER_REQUEST_REJECTED",
            EntityName = "LecturerRequest", EntityId = request.Id });
        await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return Results.Ok(new { request.Id, status = "REJECTED", request.RejectionReason });
    }

    private static IQueryable<object> ProjectProjection(StudentProjectsDbContext db, IQueryable<Project> source) =>
        from project in source
        join topic in db.Topics.AsNoTracking() on project.TopicId equals topic.Id
        join student in db.Users.AsNoTracking() on project.StudentUserId equals student.Id
        join lecturer in db.Users.AsNoTracking() on project.LecturerUserId equals lecturer.Id
        join period in db.RegistrationPeriods.AsNoTracking() on project.RegistrationPeriodId equals period.Id
        select new { project.Id, project.TopicId, topicTitle = topic.Title, project.StudentUserId,
            studentName = student.FullName, project.LecturerUserId, lecturerName = lecturer.FullName,
            project.RegistrationPeriodId, periodName = period.Name, status = project.Status.ToString(), project.CreatedAt };

    private static async Task<IResult> ListProjects(StudentProjectsDbContext db, ClaimsPrincipal principal, CancellationToken ct)
    {
        var role = Role(principal); var actor = Actor(principal);
        var query = db.Projects.AsNoTracking().AsQueryable();
        if (role == "Student") query = query.Where(p => p.StudentUserId == actor);
        else if (role == "Lecturer") query = query.Where(p => p.LecturerUserId == actor);
        else if (role != "Admin") return Results.Forbid();
        return Results.Ok(await ProjectProjection(db, query.OrderByDescending(p => p.CreatedAt)).ToListAsync(ct));
    }

    private static async Task<IResult> GetProject(Guid id, StudentProjectsDbContext db, ClaimsPrincipal principal, CancellationToken ct)
    {
        var actor = Actor(principal); var role = Role(principal);
        var query = db.Projects.AsNoTracking().Where(p => p.Id == id);
        if (role == "Student") query = query.Where(p => p.StudentUserId == actor);
        else if (role == "Lecturer") query = query.Where(p => p.LecturerUserId == actor);
        else if (role != "Admin") return Results.Forbid();
        var item = await ProjectProjection(db, query).SingleOrDefaultAsync(ct);
        return item is null ? Results.NotFound() : Results.Ok(item);
    }
}
