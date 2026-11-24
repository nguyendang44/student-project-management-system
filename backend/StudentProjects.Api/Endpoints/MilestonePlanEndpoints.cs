using System.Data;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StudentProjects.Api.Services;
using StudentProjects.Domain.Entities;
using StudentProjects.Domain.Enums;
using StudentProjects.Infrastructure.Persistence;

namespace StudentProjects.Api.Endpoints;

public static partial class ProjectProgressV07Endpoints
{
    public sealed record CriterionPlanInput(List<CriterionInput>? Items);

    private static string? CriterionError(CriterionInput? input)
    {
        if (input is null || Text(input.Title).Length is < 1 or > 200 ||
            Text(input.AcceptanceCriteria).Length is < 1 or > 250 || input.Points is < 1 or > 100)
            return "Nhập tên, điều kiện đạt và trọng số nguyên từ 1 đến 100 cho mỗi đầu việc.";
        if (input.DeadlineAt.HasValue && input.DeadlineAt.Value.UtcDateTime.Year is < 2 or > 9998)
            return "Ngày hoàn thành đầu việc nằm ngoài phạm vi hỗ trợ.";
        return null;
    }

    private static async Task<IResult> SuggestCriterionPlan(Guid id, ClaimsPrincipal user,
        StudentProjectsDbContext db, AiGitProgressClient ai, CancellationToken ct)
    {
        var (milestone, project) = await CriterionScope(id, db, ct);
        if (milestone is null || project is null) return Results.NotFound();
        if (!CanWrite(project, user, "Lecturer")) return Results.Forbid();
        if (!Active(project))
            return Conflict("Chỉ đề xuất đầu việc cho Milestone và đồ án đang hoạt động.");
        var existing = await db.MilestoneCriteria.AsNoTracking().Where(c => c.MilestoneId == id && !c.IsDeleted)
            .Select(c => new { c.Title, c.AcceptanceCriteria, c.Points }).ToListAsync(ct);
        if (existing.Count >= 20) return Conflict("Milestone đã có 20 đầu việc.");
        var topic = await db.Topics.AsNoTracking().SingleOrDefaultAsync(t => t.Id == project.TopicId, ct);
        if (topic is null) return Results.NotFound();
        try
        {
            var items = await ai.SuggestMilestonePlanAsync(new
            {
                project = new { topic.Title, topic.Description, topic.Objective,
                    topic.ExpectedContent, topic.ProposedTechnology, projectDescription = project.Description },
                milestone = new { milestone.Title, milestone.Description, milestone.ExpectedResult },
                existing
            }, 20 - existing.Count, ct);
            var titles = existing.Select(c => c.Title).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var unique = items.Where(item => !titles.Contains(item.Title)).ToArray();
            if (unique.Length == 0) return Conflict("Các đầu việc AI đề xuất đã có trong Milestone.");
            return Results.Ok(new { items = unique });
        }
        catch (InvalidDataException ex) { return Results.Json(new { message = ex.Message }, statusCode: 502); }
        catch (Exception ex) when (!ct.IsCancellationRequested &&
            ex is HttpRequestException or JsonException or OperationCanceledException)
        {
            return Results.Json(new { message = "Ollama chưa tạo được đề xuất. Kiểm tra Ollama rồi thử lại." }, statusCode: 502);
        }
    }

    private static Task<IResult> SaveCriterionPlan(Guid id, CriterionPlanInput? input, ClaimsPrincipal user,
        StudentProjectsDbContext db, CancellationToken ct) =>
        PersistCriterionPlan(id, input?.Items, user, db, ct);

    private static async Task<IResult> PersistCriterionPlan(Guid id, IReadOnlyList<CriterionInput>? inputs,
        ClaimsPrincipal user, StudentProjectsDbContext db, CancellationToken ct)
    {
        var (milestone, project) = await CriterionScope(id, db, ct);
        if (milestone is null || project is null) return Results.NotFound();
        if (!CanWrite(project, user, "Lecturer")) return Results.Forbid();
        if (!Active(project))
            return Conflict("Không thể thêm đầu việc vào đồ án đã đóng.");
        if (inputs is null || inputs.Count is < 1 or > 20) return Invalid("Chọn từ 1 đến 20 đầu việc.");
        foreach (var item in inputs)
        {
            var error = CriterionError(item);
            if (error is not null) return Invalid(error);
        }
        await using var transaction = db.Database.IsSqlServer()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct) : null;
        if (transaction is not null)
        {
            var locked = await LockCriterionMilestone(db, id, ct);
            if (locked is null) return Results.NotFound();
        }
        var existing = await db.MilestoneCriteria.AsNoTracking().Where(c => c.MilestoneId == id && !c.IsDeleted).ToListAsync(ct);
        if (existing.Count + inputs.Count > 20) return Conflict("Mỗi Milestone tối đa 20 đầu việc.");
        var names = existing.Select(c => c.Title).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (inputs.Any(item => !names.Add(Text(item.Title)))) return Conflict("Có đầu việc trùng tên trong Milestone.");
        var ids = new List<Guid>();
        foreach (var input in inputs)
        {
            var criterion = new MilestoneCriterion
            {
                MilestoneId = id, Title = Text(input.Title), AcceptanceCriteria = Text(input.AcceptanceCriteria),
                Points = input.Points, DeadlineAt = input.DeadlineAt.HasValue ? EndOfDeadlineDay(input.DeadlineAt.Value) : null
            };
            ids.Add(criterion.Id);
            db.MilestoneCriteria.Add(criterion);
            if (criterion.DeadlineAt.HasValue)
                Audit(db, Actor(user), "CRITERION_DEADLINE_UPDATED", "MilestoneCriterion", criterion.Id);
        }
        await db.SaveChangesAsync(ct);
        await MilestoneAutoAssessment.SyncAsync(db, id, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return Results.Ok(new { id = ids[0], ids, createdCount = ids.Count });
    }

    private static async Task<IResult> EditCriterion(Guid id, Guid criterionId, CriterionInput? input,
        ClaimsPrincipal user, StudentProjectsDbContext db, CancellationToken ct)
    {
        var (milestone, project) = await CriterionScope(id, db, ct);
        if (milestone is null || project is null) return Results.NotFound();
        if (!CanWrite(project, user, "Lecturer")) return Results.Forbid();
        if (!Active(project))
            return Conflict("Không thể sửa đầu việc trong đồ án đã đóng.");
        var error = CriterionError(input);
        if (error is not null) return Invalid(error);
        await using var transaction = db.Database.IsSqlServer()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct) : null;
        if (transaction is not null)
        {
            var locked = await LockCriterionMilestone(db, id, ct);
            if (locked is null) return Results.NotFound();
        }
        var criterion = await db.MilestoneCriteria.SingleOrDefaultAsync(c => c.Id == criterionId && c.MilestoneId == id && !c.IsDeleted, ct);
        if (criterion is null) return Results.NotFound();
        if (await db.MilestoneCriteria.AnyAsync(c => c.MilestoneId == id && !c.IsDeleted &&
            c.Id != criterionId && c.Title == Text(input!.Title), ct)) return Conflict("Tên đầu việc đã có trong Milestone.");
        var title = Text(input!.Title);
        var acceptance = Text(input.AcceptanceCriteria);
        var now = DateTimeOffset.UtcNow;
        if (title != criterion.Title || acceptance != criterion.AcceptanceCriteria)
            criterion.DefinitionChangedAt = now;
        var deadline = input.DeadlineAt.HasValue ? EndOfDeadlineDay(input.DeadlineAt.Value) : (DateTimeOffset?)null;
        if (deadline != criterion.DeadlineAt && deadline.HasValue)
            Audit(db, Actor(user), "CRITERION_DEADLINE_UPDATED", "MilestoneCriterion", criterion.Id);
        criterion.Title = title;
        criterion.AcceptanceCriteria = acceptance;
        criterion.Points = input.Points;
        criterion.DeadlineAt = deadline;
        criterion.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        await MilestoneAutoAssessment.SyncAsync(db, id, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return Results.Ok(new { criterion.Id });
    }

    private static async Task<IResult> DeleteCriterion(Guid id, Guid criterionId, ClaimsPrincipal user,
        StudentProjectsDbContext db, CancellationToken ct)
    {
        var (milestone, project) = await CriterionScope(id, db, ct);
        if (milestone is null || project is null) return Results.NotFound();
        if (!CanWrite(project, user, "Lecturer")) return Results.Forbid();
        if (!Active(project))
            return Conflict("Không thể xóa đầu việc trong đồ án đã đóng.");
        await using var transaction = db.Database.IsSqlServer()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct) : null;
        if (transaction is not null)
        {
            var locked = await LockCriterionMilestone(db, id, ct);
            if (locked is null) return Results.NotFound();
        }
        var criterion = await db.MilestoneCriteria.SingleOrDefaultAsync(c => c.Id == criterionId && c.MilestoneId == id, ct);
        if (criterion is null) return Results.NotFound();
        if (!criterion.IsDeleted)
        {
            criterion.IsDeleted = true;
            criterion.UpdatedAt = DateTimeOffset.UtcNow;
            Audit(db, Actor(user), "CRITERION_REMOVED", "MilestoneCriterion", criterion.Id);
            await db.SaveChangesAsync(ct);
            await MilestoneAutoAssessment.SyncAsync(db, id, ct);
        }
        if (transaction is not null) await transaction.CommitAsync(ct);
        return Results.NoContent();
    }
}
