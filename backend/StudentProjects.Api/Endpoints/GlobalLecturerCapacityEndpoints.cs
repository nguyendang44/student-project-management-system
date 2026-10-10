using System.Data;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using StudentProjects.Domain.Entities;
using StudentProjects.Domain.Enums;
using StudentProjects.Infrastructure.Persistence;

namespace StudentProjects.Api.Endpoints;

/// <summary>
/// v0.6.14: capacity is a lecturer-wide limit, independent of registration windows.
/// Keep legacy period rows synchronized so the existing combined-registration code
/// and every previously created registration period apply the same limit.
/// </summary>
public static class GlobalLecturerCapacityEndpoints
{
    public static void MapGlobalLecturerCapacityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/lecturer-capacities/global")
            .RequireAuthorization(p => p.RequireRole("Student", "Lecturer", "Admin"));
        group.MapGet("", List);
        group.MapPut("/{lecturerId:guid}", Update);
    }

    private static async Task<IResult> List(StudentProjectsDbContext db, CancellationToken ct)
    {
        var people = await (from u in db.Users.AsNoTracking()
                            join l in db.Lecturers.AsNoTracking() on u.Id equals l.UserId
                            join role in db.Roles.AsNoTracking() on u.RoleId equals role.Id
                            where u.IsActive && role.Name == "Lecturer"
                            orderby u.FullName, u.Id
                            select new { u.Id, u.FullName, l.Specialty }).ToListAsync(ct);

        var limits = await db.LecturerCapacities.AsNoTracking()
            .GroupBy(c => c.LecturerUserId)
            .Select(g => new { LecturerUserId = g.Key, MaxStudents = g.Max(c => c.MaxStudents) })
            .ToDictionaryAsync(x => x.LecturerUserId, x => x.MaxStudents, ct);

        var active = await db.Projects.AsNoTracking()
            .Where(p => p.Status != ProjectStatus.CANCELLED && p.Status != ProjectStatus.COMPLETED)
            .GroupBy(p => p.LecturerUserId)
            .Select(g => new { LecturerUserId = g.Key, CurrentStudents = g.Count() })
            .ToDictionaryAsync(x => x.LecturerUserId, x => x.CurrentStudents, ct);

        var items = people.Select(p =>
        {
            var maximum = limits.GetValueOrDefault(p.Id);
            var enrolled = active.GetValueOrDefault(p.Id);
            var remaining = Math.Max(0, maximum - enrolled);
            return new
            {
                LecturerUserId = p.Id,
                LecturerName = p.FullName,
                Specialty = p.Specialty,
                MaxStudents = maximum,
                CurrentStudents = enrolled,
                Remaining = remaining,
                Status = remaining > 0 ? "AVAILABLE" : "FULL"
            };
        }).ToList();
        return Results.Ok(items);
    }

    public sealed record UpdateLimit(int MaxStudents);

    private static async Task<IResult> Update(Guid lecturerId, UpdateLimit? body,
        ClaimsPrincipal principal, StudentProjectsDbContext db, CancellationToken ct)
    {
        if (body is null || body.MaxStudents is < 0 or > 1000)
            return Results.BadRequest(new { code = "INVALID_CAPACITY", message = "Giới hạn phải từ 0 đến 1000." });

        var actingAdmin = principal.IsInRole("Admin");
        var userIdText = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var isOwner = Guid.TryParse(userIdText, out var userId) && userId == lecturerId;
        if (!actingAdmin && (!principal.IsInRole("Lecturer") || !isOwner))
            return Results.Forbid();

        // Serializable protects the global count and synchronized period limits against concurrent updates.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var exists = await (from u in db.Users
                            join l in db.Lecturers on u.Id equals l.UserId
                            join role in db.Roles on u.RoleId equals role.Id
                            where u.Id == lecturerId && u.IsActive && role.Name == "Lecturer"
                            select u.Id).AnyAsync(ct);
        if (!exists) return Results.NotFound(new { code = "LECTURER_NOT_FOUND", message = "Không tìm thấy giảng viên đang hoạt động." });

        var currentStudents = await db.Projects.CountAsync(p => p.LecturerUserId == lecturerId &&
            p.Status != ProjectStatus.CANCELLED && p.Status != ProjectStatus.COMPLETED, ct);
        if (body.MaxStudents < currentStudents)
            return Results.Conflict(new { code = "CAPACITY_BELOW_CURRENT", message = $"Giảng viên đang hướng dẫn {currentStudents} sinh viên; không thể giảm dưới số này." });

        var periodIds = await db.RegistrationPeriods.Select(p => p.Id).ToListAsync(ct);
        if (periodIds.Count == 0)
            return Results.Conflict(new { code = "PERIOD_NOT_CONFIGURED", message = "Chưa có đợt đăng ký trong hệ thống. Admin cần tạo ít nhất một đợt để lưu sức chứa." });

        var entries = await db.LecturerCapacities.Where(c => c.LecturerUserId == lecturerId).ToListAsync(ct);
        var byPeriod = entries.ToDictionary(c => c.RegistrationPeriodId);
        var now = DateTimeOffset.UtcNow;
        foreach (var periodId in periodIds)
        {
            if (!byPeriod.TryGetValue(periodId, out var entry))
            {
                entry = new LecturerCapacity
                {
                    LecturerUserId = lecturerId,
                    RegistrationPeriodId = periodId,
                    MaxStudents = body.MaxStudents,
                    CurrentStudents = currentStudents
                };
                db.LecturerCapacities.Add(entry);
            }
            else
            {
                entry.MaxStudents = body.MaxStudents;
                entry.CurrentStudents = currentStudents;
                entry.UpdatedAt = now;
            }
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Results.Ok(new
        {
            lecturerUserId = lecturerId,
            maxStudents = body.MaxStudents,
            currentStudents,
            remaining = body.MaxStudents - currentStudents,
            updatedPeriods = periodIds.Count
        });
    }
}
