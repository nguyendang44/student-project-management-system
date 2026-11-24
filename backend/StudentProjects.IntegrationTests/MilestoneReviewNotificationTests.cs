using StudentProjects.Api.Services;
using StudentProjects.Domain.Entities;
using Xunit;

namespace StudentProjects.IntegrationTests;

public sealed class MilestoneReviewNotificationTests
{
    [Fact]
    public void Lecturer_change_addresses_the_student_and_retains_the_reason()
    {
        var project = new Project { StudentUserId = Guid.NewGuid(), LecturerUserId = Guid.NewGuid() };
        var milestone = new Milestone { Title = "Backend" };
        var criterion = new MilestoneCriterion { Title = "Liên kết GitHub", Points = 30 };
        var ai = new MilestoneCriterionAssessment { CriterionId = criterion.Id, Source = "AI", Outcome = "full" };
        var review = new MilestoneCriterionAssessment
        { CriterionId = criterion.Id, Source = "REVIEW", Outcome = "missing", Note = "Cần bổ sung kiểm thử" };
        var notification = MilestoneAutoAssessment.ReviewNotification(project, milestone, criterion, review, [ai])!;
        Assert.Equal(project.StudentUserId, notification.RecipientUserId);
        Assert.Equal($"V08:CREVIEW:{criterion.Id:N}:{review.Id:N}", notification.Type);
        Assert.Contains(review.Note, notification.Message);
    }

    [Fact]
    public void Repeated_decision_is_silent_but_a_changed_note_notifies()
    {
        var project = new Project();
        var milestone = new Milestone();
        var criterion = new MilestoneCriterion();
        var first = new MilestoneCriterionAssessment
        { CriterionId = criterion.Id, Source = "REVIEW", Outcome = "missing", Note = "Cần kiểm thử" };
        var next = new MilestoneCriterionAssessment
        { CriterionId = criterion.Id, Source = "REVIEW", Outcome = "missing", Note = first.Note };
        Assert.Null(MilestoneAutoAssessment.ReviewNotification(project, milestone, criterion, next, [first]));
        next.Note = "Kiểm tra quyền sinh viên khác";
        Assert.Equal(project.StudentUserId,
            MilestoneAutoAssessment.ReviewNotification(project, milestone, criterion, next, [first])!.RecipientUserId);
    }

    [Fact]
    public void Confirming_an_unchanged_ai_result_does_not_send_a_correction_notice()
    {
        var criterion = new MilestoneCriterion();
        var ai = new MilestoneCriterionAssessment { CriterionId = criterion.Id, Source = "AI", Outcome = "full" };
        var review = new MilestoneCriterionAssessment { CriterionId = criterion.Id, Source = "REVIEW", Outcome = "full" };
        Assert.Null(MilestoneAutoAssessment.ReviewNotification(new Project(), new Milestone(), criterion, review, [ai]));
    }
}
