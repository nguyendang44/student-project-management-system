using StudentProjects.Domain.Entities;
using StudentProjects.Domain.Enums;
using Xunit;

namespace StudentProjects.IntegrationTests;

public sealed class ProgressDeadlinePolicyTests
{
    private static DateTimeOffset Day(int day, int hour = 0, int minute = 0) => new(2026, 11, day, hour, minute, 0, TimeSpan.FromHours(7));

    [Fact]
    public void Whole_project_compares_progress_with_elapsed_calendar_days()
    {
        var warning = ProgressDeadlinePolicy.Evaluate(Day(20), Day(30, 23, 59), 12.5m, Day(25))!;
        Assert.Equal("BEHIND_PLAN", warning.Code);
        Assert.Equal((decimal?)50m, warning.ExpectedPercent);
        Assert.Equal((decimal?)12.5m, warning.Percent);
        Assert.Equal(5, warning.DaysRemaining);
        Assert.Null(ProgressDeadlinePolicy.Evaluate(Day(20), Day(30), 50m, Day(25)));
    }

    [Fact]
    public void Date_only_deadline_does_not_become_overdue_during_the_last_day()
    {
        var morning = ProgressDeadlinePolicy.Evaluate(Day(20), Day(25), 12.5m, Day(25))!;
        var evening = ProgressDeadlinePolicy.Evaluate(Day(20), Day(25), 12.5m, Day(25, 23, 59))!;
        Assert.Equal("DUE_TODAY", morning.Code);
        Assert.Equal("DUE_TODAY", evening.Code);
        Assert.Equal("OVERDUE", ProgressDeadlinePolicy.Evaluate(Day(20), Day(25), 12.5m, Day(26))!.Code);
        Assert.Equal((decimal?)100m, morning.ExpectedPercent);
    }

    [Fact]
    public void Calendar_progress_uses_Vietnam_day_and_does_not_change_hour_by_hour()
    {
        var utcMidnightVietnam = new DateTimeOffset(2026, 11, 24, 17, 0, 0, TimeSpan.Zero);
        var early = ProgressDeadlinePolicy.Evaluate(Day(20), Day(30), 12.5m, utcMidnightVietnam)!;
        var late = ProgressDeadlinePolicy.Evaluate(Day(20), Day(30), 12.5m, Day(25, 23, 59))!;
        Assert.Equal(early.ExpectedPercent, late.ExpectedPercent);
        Assert.Equal(early.DaysRemaining, late.DaysRemaining);
    }

    [Fact]
    public void Completed_closed_future_and_unset_schedules_do_not_raise_incomplete_work_warnings()
    {
        Assert.Null(ProgressDeadlinePolicy.Evaluate(Day(20), Day(25), 100m, Day(26)));
        Assert.Null(ProgressDeadlinePolicy.Evaluate(Day(20), Day(25), 0m, Day(26), active: false));
        Assert.Null(ProgressDeadlinePolicy.Evaluate(Day(27), Day(30), 0m, Day(26)));
        Assert.Null(ProgressDeadlinePolicy.Evaluate(Day(20), null, 0m, Day(26)));
        Assert.Null(ProgressDeadlinePolicy.Evaluate(Day(30), Day(20), 0m, Day(26)));
    }

    [Fact]
    public void Criteria_without_a_start_date_use_near_deadline_thresholds()
    {
        Assert.Equal("RISK_7D", ProgressDeadlinePolicy.Evaluate(null, Day(30), 49m, Day(23))!.Code);
        Assert.Null(ProgressDeadlinePolicy.Evaluate(null, Day(30), 50m, Day(23)));
        Assert.Equal("RISK_3D", ProgressDeadlinePolicy.Evaluate(null, Day(30), 79m, Day(27))!.Code);
        Assert.Null(ProgressDeadlinePolicy.Evaluate(null, Day(30), 80m, Day(27)));
        Assert.Equal("RISK_1D", ProgressDeadlinePolicy.Evaluate(null, Day(30), 99m, Day(29))!.Code);
        Assert.Null(ProgressDeadlinePolicy.Evaluate(null, Day(30), 100m, Day(29)));
    }

    [Fact]
    public void Missing_assessed_plan_is_not_silently_reported_as_zero_percent()
    {
        var warning = ProgressDeadlinePolicy.Evaluate(Day(20), Day(30), null, Day(27))!;
        Assert.Equal("info", warning.Level);
        Assert.Equal((decimal?)null, warning.Percent);
        Assert.Equal("UNKNOWN", warning.Code);
    }

    [Fact]
    public void Notification_keys_deduplicate_refreshes_but_allow_a_new_daily_plan_warning()
    {
        var id = Guid.NewGuid();
        var warning = ProgressDeadlinePolicy.Evaluate(Day(20), Day(30), 12.5m, Day(25))!;
        var morning = ProgressDeadlinePolicy.NotificationKey("PRISK", id, warning, Day(25));
        Assert.Equal(morning, ProgressDeadlinePolicy.NotificationKey("PRISK", id, warning, Day(25, 23, 59)));
        Assert.Equal(false, morning == ProgressDeadlinePolicy.NotificationKey("PRISK", id, warning, Day(26)));
        Assert.Equal(true, morning.Length < 100);
    }

    [Fact]
    public void Overall_progress_uses_normalized_weights_and_latest_lecturer_override()
    {
        var first = new Milestone();
        var second = new Milestone();
        var pending = new Milestone { ProposalStatus = "PENDING_REVIEW" };
        var item = new MilestoneCriterion { MilestoneId = first.Id, Points = 30 };
        var remaining = new MilestoneCriterion { MilestoneId = first.Id, Points = 100 };
        var records = new List<MilestoneCriterionAssessment> { new() { CriterionId = item.Id, Source = "AI", Outcome = "full" } };
        var criteria = new[] { item, remaining };
        Assert.Equal((decimal?)11.5m, ProjectEvidenceProgress.Calculate([first, second, pending], criteria, records));
        records.Add(new() { CriterionId = item.Id, Source = "REVIEW", Outcome = "missing" });
        Assert.Equal((decimal?)0m, ProjectEvidenceProgress.Calculate([first, second], criteria, records));
        Assert.Equal((decimal?)null, ProjectEvidenceProgress.Calculate([pending], criteria, records));
        second.Status = MilestoneStatus.APPROVED;
        Assert.Equal((decimal?)100m, ProjectEvidenceProgress.Calculate([second], [], []));
    }
}
