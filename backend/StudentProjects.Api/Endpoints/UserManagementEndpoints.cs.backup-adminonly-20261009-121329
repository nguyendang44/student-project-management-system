using System.Net.Mail;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StudentProjects.Application.Contracts;
using StudentProjects.Domain.Entities;
using StudentProjects.Infrastructure.Persistence;

namespace StudentProjects.Api.Endpoints;

public static class UserManagementEndpoints
{
    public static void MapUserManagementEndpoints(this IEndpointRouteBuilder app)
    {
        var users = app.MapGroup("/api/v1/users").RequireAuthorization();
        // Server-side access control; the frontend menu is not a security boundary.
        users.MapGet("", List).RequireAuthorization(p => p.RequireRole("Admin"));
        users.MapGet("/{id:guid}", GetById).RequireAuthorization(p => p.RequireRole("Admin"));
        users.MapPost("", Create).RequireAuthorization(p => p.RequireRole("Admin"));
        users.MapPut("/{id:guid}", Update).RequireAuthorization(p => p.RequireRole("Admin"));
        users.MapPatch("/{id:guid}/status", SetStatus).RequireAuthorization(p => p.RequireRole("Admin"));
        users.MapGet("/me/profile", GetOwnProfile).RequireAuthorization(p => p.RequireRole("Student", "Lecturer"));
        users.MapPut("/me/profile", UpdateOwnProfile).RequireAuthorization(p => p.RequireRole("Student", "Lecturer"));

        // A safe, read-only directory without student personal data.
        app.MapGet("/api/v1/lecturers", LecturerDirectory)
            .RequireAuthorization(p => p.RequireRole("Student", "Lecturer", "Admin"));
    }

    private static readonly string[] ManagedRoles = ["Student", "Lecturer"];
    private static bool IsManaged(string role) => ManagedRoles.Contains(role, StringComparer.Ordinal);
    private static IResult Invalid(string detail) => Results.BadRequest(new { code = "INVALID_REQUEST", message = detail });
    private static IResult Conflict(string detail) => Results.Conflict(new { code = "CONFLICT", message = detail });
    private static bool ValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 256) return false;
        return MailAddress.TryCreate(email, out var parsed) &&
               string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase);
    }
    private static bool ValidName(string? name) => !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= 200;
    private static Guid Actor(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    private static IResult? CheckProfileFields(string role, string? studentCode, string? faculty, string? specialty)
    {
        if (role == "Student" && (string.IsNullOrWhiteSpace(studentCode) || studentCode.Trim().Length > 40))
            return Invalid("Student code is required (max 40 characters).");
        if (faculty?.Length > 200 || specialty?.Length > 300)
            return Invalid("Faculty or specialty exceeds allowed length.");
        return null;
    }
    private static async Task<bool> IsDuplicate(StudentProjectsDbContext db, string email,
        string? studentCode, Guid? exceptId, CancellationToken ct)
    {
        if (await db.Users.AnyAsync(u => u.Email == email && (!exceptId.HasValue || u.Id != exceptId.Value), ct))
            return true;
        if (!string.IsNullOrEmpty(studentCode))
            return await db.Students.AnyAsync(s => s.StudentCode == studentCode &&
                (!exceptId.HasValue || s.UserId != exceptId.Value), ct);
        return false;
    }
    private static bool UniqueConflict(DbUpdateException ex) =>
        ex.InnerException is SqlException sql && sql.Number is 2601 or 2627;

    // All predicates are applied to EF entity columns before DTO construction.
    // SQL Server cannot translate arbitrary member access on new UserListItem(...) in WHERE/ORDER BY.
    private static IQueryable<UserListItem> SelectUsers(StudentProjectsDbContext db,
        Guid? userId = null, string? roleFilter = null, string? search = null)
    {
        var query =
            from user in db.Users.AsNoTracking()
            join role in db.Roles.AsNoTracking() on user.RoleId equals role.Id
            join student in db.Students.AsNoTracking() on user.Id equals student.UserId into students
            from student in students.DefaultIfEmpty()
            join lecturer in db.Lecturers.AsNoTracking() on user.Id equals lecturer.UserId into lecturers
            from lecturer in lecturers.DefaultIfEmpty()
            where role.Name == "Student" || role.Name == "Lecturer"
            select new { user, role, student, lecturer };

        if (userId.HasValue)
        {
            var id = userId.Value;
            query = query.Where(x => x.user.Id == id);
        }
        if (!string.IsNullOrWhiteSpace(roleFilter))
            query = query.Where(x => x.role.Name == roleFilter);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.user.FullName.Contains(term) || x.user.Email.Contains(term) ||
                (x.student != null && x.student.StudentCode.Contains(term)));
        }

        return query.OrderBy(x => x.user.FullName).ThenBy(x => x.user.Id)
            .Select(x => new UserListItem(x.user.Id, x.user.FullName, x.user.Email,
                x.role.Name, x.user.IsActive,
                x.student == null ? null : x.student.StudentCode,
                x.student == null ? null : x.student.Faculty,
                x.lecturer == null ? null : x.lecturer.Specialty));
    }

    private static async Task<IResult> List(StudentProjectsDbContext db, string? role,
        string? search, int? page, int? pageSize, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(role) && !IsManaged(role)) return Invalid("Role must be Student or Lecturer.");
        var currentPage = page ?? 1;
        var take = pageSize ?? 20;
        if (currentPage < 1 || currentPage > 1_000_000 || take < 1 || take > 100 || search?.Length > 200)
            return Invalid("Invalid paging or search query.");
        var query = SelectUsers(db, roleFilter: role, search: search);
        var count = await query.CountAsync(ct);
        var items = await query.Skip((currentPage - 1) * take).Take(take).ToListAsync(ct);
        return Results.Ok(new PagedUsersResponse(items, count, currentPage, take));
    }

    private static async Task<IResult> GetById(Guid id, StudentProjectsDbContext db, CancellationToken ct)
    {
        var item = await SelectUsers(db, userId: id).SingleOrDefaultAsync(ct);
        return item is null ? Results.NotFound() : Results.Ok(item);
    }

    private static async Task<IResult> Create(CreateManagedUserRequest? req,
        ClaimsPrincipal principal, StudentProjectsDbContext db,
        IPasswordHasher<User> hasher, CancellationToken ct)
    {
        if (req is null || !IsManaged(req.Role ?? "")) return Invalid("Only Student and Lecturer roles may be created.");
        if (!ValidName(req.FullName) || !ValidEmail(req.Email) ||
            string.IsNullOrEmpty(req.Password) || req.Password.Length < 12 || req.Password.Length > 128)
            return Invalid("A valid name, email and password (12-128 characters) are required.");
        var badProfile = CheckProfileFields(req.Role!, req.StudentCode, req.Faculty, req.Specialty);
        if (badProfile is not null) return badProfile;
        var email = req.Email!.Trim().ToLowerInvariant();
        var code = req.Role == "Student" ? req.StudentCode!.Trim().ToUpperInvariant() : null;
        if (await IsDuplicate(db, email, code, null, ct)) return Conflict("Email or student code already exists.");
        var roleId = req.Role == "Student" ? RoleIds.Student : RoleIds.Lecturer;
        var user = new User { FullName = req.FullName!.Trim(), Email = email, RoleId = roleId, IsActive = true };
        user.PasswordHash = hasher.HashPassword(user, req.Password!);
        db.Users.Add(user);
        if (req.Role == "Student")
            db.Students.Add(new StudentProfile { UserId = user.Id, StudentCode = code!, Faculty = req.Faculty?.Trim() });
        else db.Lecturers.Add(new LecturerProfile { UserId = user.Id, Specialty = req.Specialty?.Trim() });
        db.AuditEntries.Add(new AuditEntry { ActorUserId = Actor(principal), Action = "USER_CREATED", EntityName = "User", EntityId = user.Id });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (UniqueConflict(ex)) { return Conflict("Email or student code already exists."); }
        var created = await SelectUsers(db, userId: user.Id).SingleAsync(ct);
        return Results.Created($"/api/v1/users/{user.Id}", created);
    }

    private static async Task<IResult> Update(Guid id, UpdateManagedUserRequest? req,
        ClaimsPrincipal principal, StudentProjectsDbContext db, CancellationToken ct)
    {
        if (req is null || !ValidName(req.FullName) || !ValidEmail(req.Email))
            return Invalid("Valid full name and email are required.");
        var user = await db.Users.Include(x => x.Role).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (user is null || !IsManaged(user.Role.Name)) return Results.NotFound();
        var badProfile = CheckProfileFields(user.Role.Name, req.StudentCode, req.Faculty, req.Specialty);
        if (badProfile is not null) return badProfile;
        var email = req.Email!.Trim().ToLowerInvariant();
        var code = user.Role.Name == "Student" ? req.StudentCode!.Trim().ToUpperInvariant() : null;
        if (await IsDuplicate(db, email, code, id, ct)) return Conflict("Email or student code already exists.");
        if (user.Email != email) user.TokenVersion++; // invalidate JWT when email changes
        user.Email = email;
        user.FullName = req.FullName!.Trim();
        user.UpdatedAt = DateTimeOffset.UtcNow;
        if (user.Role.Name == "Student")
        {
            var profile = await db.Students.SingleOrDefaultAsync(s => s.UserId == id, ct);
            if (profile is null) return Results.Problem("Student profile missing; repair database consistency.", statusCode: 409);
            profile.StudentCode = code!;
            profile.Faculty = req.Faculty?.Trim();
            profile.UpdatedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            var profile = await db.Lecturers.SingleOrDefaultAsync(s => s.UserId == id, ct);
            if (profile is null) return Results.Problem("Lecturer profile missing; repair database consistency.", statusCode: 409);
            profile.Specialty = req.Specialty?.Trim();
            profile.UpdatedAt = DateTimeOffset.UtcNow;
        }
        db.AuditEntries.Add(new AuditEntry { ActorUserId = Actor(principal), Action = "USER_UPDATED", EntityName = "User", EntityId = id });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (UniqueConflict(ex)) { return Conflict("Email or student code already exists."); }
        return Results.Ok(await SelectUsers(db, userId: id).SingleAsync(ct));
    }

    private static async Task<IResult> SetStatus(Guid id, SetUserStatusRequest? req,
        ClaimsPrincipal principal, StudentProjectsDbContext db, CancellationToken ct)
    {
        if (req?.IsActive is null) return Invalid("isActive must be true or false.");
        var user = await db.Users.Include(x => x.Role).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (user is null || !IsManaged(user.Role.Name)) return Results.NotFound();
        if (user.IsActive != req.IsActive.Value)
        {
            user.IsActive = req.IsActive.Value;
            user.TokenVersion++; // revoke all tokens on either status transition
            user.UpdatedAt = DateTimeOffset.UtcNow;
            db.AuditEntries.Add(new AuditEntry { ActorUserId = Actor(principal),
                Action = req.IsActive.Value ? "USER_REACTIVATED" : "USER_DISABLED", EntityName = "User", EntityId = id });
            await db.SaveChangesAsync(ct);
        }
        return Results.Ok(await SelectUsers(db, userId: id).SingleAsync(ct));
    }

    private static async Task<IResult> GetOwnProfile(ClaimsPrincipal principal,
        StudentProjectsDbContext db, CancellationToken ct)
    {
        var id = Actor(principal);
        var profile = await SelectUsers(db, userId: id).SingleOrDefaultAsync(ct);
        return profile is null ? Results.NotFound() : Results.Ok(profile);
    }

    private static async Task<IResult> UpdateOwnProfile(UpdateOwnProfileRequest? req,
        ClaimsPrincipal principal, StudentProjectsDbContext db, CancellationToken ct)
    {
        if (req is null || !ValidName(req.FullName) || req.Faculty?.Length > 200 || req.Specialty?.Length > 300)
            return Invalid("Invalid name or profile information.");
        var id = Actor(principal);
        var user = await db.Users.Include(x => x.Role).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (user is null || !IsManaged(user.Role.Name)) return Results.NotFound();
        user.FullName = req.FullName!.Trim();
        user.UpdatedAt = DateTimeOffset.UtcNow;
        if (user.Role.Name == "Student")
        {
            var student = await db.Students.SingleOrDefaultAsync(x => x.UserId == id, ct);
            if (student is null) return Results.Problem("Student profile missing.", statusCode: 409);
            student.Faculty = req.Faculty?.Trim();
            student.UpdatedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            var lecturer = await db.Lecturers.SingleOrDefaultAsync(x => x.UserId == id, ct);
            if (lecturer is null) return Results.Problem("Lecturer profile missing.", statusCode: 409);
            lecturer.Specialty = req.Specialty?.Trim();
            lecturer.UpdatedAt = DateTimeOffset.UtcNow;
        }
        db.AuditEntries.Add(new AuditEntry { ActorUserId = id, Action = "OWN_PROFILE_UPDATED", EntityName = "User", EntityId = id });
        await db.SaveChangesAsync(ct);
        return Results.Ok(await SelectUsers(db, userId: id).SingleAsync(ct));
    }

    private static async Task<IResult> LecturerDirectory(StudentProjectsDbContext db, CancellationToken ct)
    {
        // Project directory values directly from mapped tables, not from a DTO query.
        var items = await (
            from user in db.Users.AsNoTracking()
            join role in db.Roles.AsNoTracking() on user.RoleId equals role.Id
            join lecturer in db.Lecturers.AsNoTracking() on user.Id equals lecturer.UserId
            where role.Name == "Lecturer" && user.IsActive
            orderby user.FullName, user.Id
            select new { user.Id, user.FullName, lecturer.Specialty }
        ).ToListAsync(ct);
        return Results.Ok(items);
    }

}
