using System.Globalization;
using StudentProjects.Domain.Enums;

namespace StudentProjects.Domain.Entities;

public sealed record ProgressDeadlineWarning(string Level, string Code, string Message,
    decimal? Percent, decimal? ExpectedPercent, int DaysRemaining, DateTimeOffset DeadlineAt);

public static class ProgressDeadlinePolicy
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);
    public static ProgressDeadlineWarning? Evaluate(DateTimeOffset? startAt, DateTimeOffset? deadlineAt,
        decimal? percent, DateTimeOffset now, bool active = true)
    {
        if (!active || !deadlineAt.HasValue || percent >= 100m) return null;
        var today = now.ToOffset(VietnamOffset).Date;
        var end = deadlineAt.Value.ToOffset(VietnamOffset).Date;
        var start = startAt?.ToOffset(VietnamOffset).Date;
        if (start > end || (start.HasValue && today < start.Value)) return null;
        var days = (end - today).Days;
        var progress = percent.HasValue ? Math.Clamp(percent.Value, 0m, 100m) : (decimal?)null;
        string P(decimal value) => value.ToString("0.##", CultureInfo.GetCultureInfo("vi-VN"));
        ProgressDeadlineWarning Warning(string level, string code, decimal? expected, string message) =>
            new(level, code, message, progress, expected, days, deadlineAt.Value);
        if (!progress.HasValue)
            return days <= 7 ? Warning("info", "UNKNOWN", null,
                $"Chưa có % tiến độ được tính để đánh giá nguy cơ; ngày kết thúc {end:dd/MM/yyyy}.") : null;
        if (days < 0)
            return Warning("danger", "OVERDUE", 100m,
                $"Quá hạn chưa hoàn thành: tiến độ được tính {P(progress.Value)}%, còn {P(100m - progress.Value)}%; ngày kết thúc {end:dd/MM/yyyy}.");
        if (days == 0)
            return Warning("danger", "DUE_TODAY", 100m,
                $"Đến hạn hôm nay ({end:dd/MM/yyyy}): tiến độ được tính {P(progress.Value)}%, chưa đủ 100%.");
        if (start.HasValue)
        {
            var total = (end - start.Value).Days;
            var expected = total <= 0 ? 100m : Math.Round(Math.Clamp((today - start.Value).Days * 100m / total, 0m, 100m), 2);
            if (progress.Value >= expected) return null;
            return Warning(days <= 3 ? "danger" : "warning", "BEHIND_PLAN", expected,
                $"Nguy cơ trễ hạn: tiến độ được tính {P(progress.Value)}%, theo thời gian kế hoạch cần khoảng {P(expected)}% (chậm {P(expected - progress.Value)} điểm phần trăm); còn {days} ngày đến {end:dd/MM/yyyy}.");
        }
        // Individual criteria and old projects without a start date use the near-deadline thresholds.
        var target = days <= 1 ? 100m : days <= 3 ? 80m : days <= 7 ? 50m : 0m;
        if (target == 0 || progress.Value >= target) return null;
        var code = days <= 1 ? "RISK_1D" : days <= 3 ? "RISK_3D" : "RISK_7D";
        return Warning(days <= 3 ? "danger" : "warning", code, target,
            $"Nguy cơ trễ hạn: còn {days} ngày, tiến độ được tính {P(progress.Value)}%, dưới mức cảnh báo {P(target)}% (hạn {end:dd/MM/yyyy}).");
    }

    public static string NotificationKey(string kind, Guid id, ProgressDeadlineWarning warning, DateTimeOffset now) =>
        $"V08:{kind}:{id:N}:{warning.DeadlineAt.ToOffset(VietnamOffset):yyyyMMdd}:{warning.Code}" +
        (warning.Code == "BEHIND_PLAN" ? $":{now.ToOffset(VietnamOffset):yyyyMMdd}" : "");
}

public static class ProjectEvidenceProgress
{
    // Each confirmed Milestone has equal weight, matching the existing project progress summary.
    public static decimal? Calculate(IEnumerable<Milestone> milestones, IEnumerable<MilestoneCriterion> criteria,
        IEnumerable<MilestoneCriterionAssessment> assessments)
    {
        var planned = milestones.Where(m => m.ProposalStatus == "CONFIRMED").ToArray();
        if (planned.Length == 0) return null;
        var items = criteria.Where(c => !c.IsDeleted).ToLookup(c => c.MilestoneId);
        var records = assessments.ToArray();
        return Math.Round(planned.Average(m => items[m.Id].Any()
            ? MilestoneCriterionProgress.Calculate(items[m.Id], records).Confirmed
            : m.Status == MilestoneStatus.APPROVED ? 100m : 0m), 1);
    }
}
