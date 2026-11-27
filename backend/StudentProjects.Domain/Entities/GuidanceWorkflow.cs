namespace StudentProjects.Domain.Entities;

public static class GuidanceWorkflow
{
    // Older confirmed journals have no explicit ReviewStatus value.
    public static string Status(GuidanceSession entry) => entry.LecturerConfirmedAt.HasValue
        ? "CONFIRMED" : entry.ReviewStatus == "CHANGES_REQUESTED" ? "CHANGES_REQUESTED" : "PENDING";

    public static bool CanResubmit(GuidanceSession entry, Guid actor) =>
        entry.CreatedByUserId == actor && Status(entry) != "CONFIRMED";

    public static bool ValidReport(DateTimeOffset meetingAt, string? summary, string? questions, DateTimeOffset now) =>
        meetingAt.Year >= 2000 && meetingAt <= now.AddDays(1) &&
        !string.IsNullOrWhiteSpace(summary) && summary.Trim().Length <= 4000 && (questions?.Length ?? 0) <= 4000;

    public static void Review(GuidanceSession entry, bool confirm, string feedback, string nextActions, DateTimeOffset now)
    {
        if (Status(entry) == "CONFIRMED") throw new InvalidOperationException("Nhật ký đã được xác nhận.");
        if (feedback.Length > 4000 || nextActions.Length > 4000 || (!confirm && string.IsNullOrWhiteSpace(feedback)))
            throw new ArgumentException("Cần ghi lý do khi yêu cầu sửa; tối đa 4000 ký tự mỗi trường.");
        entry.Feedback = feedback.Trim();
        entry.NextActions = nextActions.Trim();
        entry.ReviewStatus = confirm ? "CONFIRMED" : "CHANGES_REQUESTED";
        entry.LecturerConfirmedAt = confirm ? now : null;
        entry.UpdatedAt = now;
    }

    public static void Resubmit(GuidanceSession entry, Guid actor, DateTimeOffset now)
    {
        if (!CanResubmit(entry, actor)) throw new InvalidOperationException("Không được sửa nhật ký này.");
        // Lecturer feedback remains visible when the student edits their own report.
        entry.ReviewStatus = "PENDING";
        entry.UpdatedAt = now;
    }
}
