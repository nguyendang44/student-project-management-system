using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using StudentProjects.Domain.Entities;
using StudentProjects.Infrastructure.Persistence;

namespace StudentProjects.Api.Services;

public sealed record MilestoneCheckStatus(Guid? RequestId, string State, string Message,
    DateTimeOffset? RequestedAt, DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt);

public static class MilestoneAssessmentStatus
{
    public const string Started = "MILESTONE_CHECK_STARTED";
    public const string Failed = "MILESTONE_CHECK_FAILED";
    public const string Skipped = "MILESTONE_CHECK_SKIPPED";
    public const string ItemDone = "MILESTONE_CHECK_ITEM_DONE";
    public const string ResponseLimit = Failed + "_RESPONSE_LIMIT";
    public const string ContextLimit = Failed + "_CONTEXT_LIMIT";
    public const string InvalidResponse = Failed + "_INVALID_RESPONSE";
    public const string Connection = Failed + "_CONNECTION";
    public const string Timeout = Failed + "_TIMEOUT";

    public static string FailureAction(Exception error) => error switch
    {
        InvalidDataException when error.Message is GitProgressComparisonResponse.EnvelopeTooLarge or GitProgressComparisonResponse.GeneratedTooLarge => ResponseLimit,
        InvalidDataException when error.Message.Contains("ngữ cảnh", StringComparison.OrdinalIgnoreCase) => ContextLimit,
        HttpRequestException => Connection,
        OperationCanceledException => Timeout,
        JsonException or InvalidDataException => InvalidResponse,
        _ => Failed
    };

    private static bool IsFailure(string action) => action == Failed || action.StartsWith(Failed + "_", StringComparison.Ordinal);
    private static string FailureMessage(string action) => action switch
    {
        ResponseLimit => "Phản hồi AI vượt giới hạn nhận dữ liệu hoặc độ dài kết quả. Chưa cập nhật điểm; xem log Backend rồi thử lại.",
        ContextLimit => "Dữ liệu báo cáo vượt ngữ cảnh đối chiếu của AI. Chưa cập nhật điểm; cần kiểm tra cách chia dữ liệu báo cáo.",
        InvalidResponse => "Ollama đã trả lời nhưng kết quả chưa đủ, sai cấu trúc hoặc có bằng chứng không hợp lệ. Kết quả gần nhất được giữ; có thể kiểm tra lại.",
        Connection => "Không hoàn tất yêu cầu tới Ollama. Kiểm tra Ollama đang chạy và model có sẵn rồi thử lại.",
        Timeout => "AI chưa hoàn tất trong thời gian chờ. Kết quả gần nhất được giữ; có thể thử lại khi Ollama sẵn sàng.",
        _ => "Chưa kiểm tra xong. Kết quả gần nhất được giữ lại; xem log Backend để xác định lỗi rồi thử lại."
    };

    private const string EventName = "MilestoneAssessmentRequest";

    public static async Task<MilestoneCheckStatus> ReadAsync(StudentProjectsDbContext db,
        Guid milestoneId, Guid projectId, CancellationToken ct)
    {
        // Called only after the endpoint has checked access to the project.
        var request = await db.AuditEntries.AsNoTracking().Where(a =>
            (a.Action == MilestoneAssessmentRequests.Manual && a.EntityId == milestoneId) ||
            (a.Action == MilestoneAssessmentRequests.Automatic && db.GitProgressRuns.Any(r =>
                r.Id == a.EntityId && r.ProjectId == projectId)))
            .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id).FirstOrDefaultAsync(ct);
        if (request is null) return Describe(null, [], milestoneId);
        var itemName = $"MilestoneAssessmentItem:{milestoneId:N}";
        var events = await db.AuditEntries.AsNoTracking().Where(a => a.EntityId == request.Id &&
            (a.EntityName == EventName || a.EntityName == itemName)).ToListAsync(ct);
        return Describe(request, events, milestoneId);
    }

    public static MilestoneCheckStatus Describe(AuditEntry? request, IEnumerable<AuditEntry> events, Guid milestoneId)
    {
        if (request is null) return new(null, "idle", "Chưa có yêu cầu kiểm tra tiến độ.", null, null, null);
        var marks = events.Where(a => a.EntityId == request.Id).ToArray();
        var started = marks.Where(a => a.Action == Started).OrderByDescending(a => a.CreatedAt).FirstOrDefault();
        var itemName = $"MilestoneAssessmentItem:{milestoneId:N}";
        var itemDone = marks.Where(a => a.Action == ItemDone && a.EntityName == itemName)
            .OrderByDescending(a => a.CreatedAt).FirstOrDefault();
        if (itemDone is not null)
            return new(request.Id, "completed", "Đã kiểm tra xong và cập nhật kết quả của Milestone này.",
                request.CreatedAt, started?.CreatedAt, itemDone.CreatedAt);
        int Priority(string action) => IsFailure(action) || action == Skipped ? 3 : action == MilestoneAssessmentRequests.Done ? 2 : 1;
        var latest = marks.Where(a => a.Action == Started || IsFailure(a.Action) || a.Action == Skipped ||
                a.Action == MilestoneAssessmentRequests.Done)
            .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => Priority(a.Action)).FirstOrDefault();
        var state = latest?.Action switch
        {
            Started => "running", Skipped => "cancelled",
            _ when latest is not null && IsFailure(latest.Action) => "failed",
            MilestoneAssessmentRequests.Done => "completed", _ => "queued"
        };
        var message = state switch
        {
            "running" => "AI đang đọc các báo cáo Git và kiểm tra điều kiện đạt của đầu việc…",
            "completed" => "Đã kiểm tra xong và cập nhật kết quả.",
            "failed" => FailureMessage(latest!.Action),
            "cancelled" => "Không thể tiếp tục: báo cáo Git không còn sẵn sàng hoặc đồ án đã đóng.",
            _ => "Đã nhận yêu cầu. Đang chờ báo cáo Git hoàn tất hoặc chờ tới lượt kiểm tra."
        };
        return new(request.Id, state, message, request.CreatedAt, started?.CreatedAt,
            state is "completed" or "failed" or "cancelled" ? latest?.CreatedAt : null);
    }

    public static async Task MarkAsync(StudentProjectsDbContext db, Guid requestId,
        string action, CancellationToken ct, Guid? milestoneId = null)
    {
        db.AuditEntries.Add(new AuditEntry { Action = action, EntityId = requestId,
            EntityName = milestoneId.HasValue ? $"MilestoneAssessmentItem:{milestoneId.Value:N}" : EventName });
        await db.SaveChangesAsync(ct);
    }
}
