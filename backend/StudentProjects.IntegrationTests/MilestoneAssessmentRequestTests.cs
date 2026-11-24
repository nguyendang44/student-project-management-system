using StudentProjects.Api.Services;
using StudentProjects.Domain.Entities;
using Xunit;

namespace StudentProjects.IntegrationTests;

public sealed class MilestoneAssessmentRequestTests
{
    [Fact]
    public void Manual_request_pins_a_report_and_a_single_milestone()
    {
        var milestone = Guid.NewGuid();
        var run = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var entry = MilestoneAssessmentRequests.CreateRecheck(new() { MilestoneId = milestone, RunId = run }, actor);
        Assert.Equal((Guid?)milestone, entry.EntityId);
        Assert.Equal((Guid?)actor, entry.ActorUserId);
        Assert.Equal((Guid?)run, MilestoneAssessmentRequests.ManualRun(entry));
        Assert.Equal(MilestoneAssessmentRequests.Manual, entry.Action);
    }

    [Fact]
    public void Compare_request_is_explicit_and_scoped_to_the_requested_report()
    {
        var run = Guid.NewGuid();
        var entry = MilestoneAssessmentRequests.CreateComparison(run, Guid.NewGuid());
        Assert.Equal((Guid?)run, entry.EntityId);
        Assert.Equal("GitProgressRun", entry.EntityName);
        Assert.Equal(MilestoneAssessmentRequests.Automatic, entry.Action);
        Assert.Equal((Guid?)null, MilestoneAssessmentRequests.ManualRun(entry));
    }

    [Fact]
    public void Superseded_ai_results_no_longer_contribute_to_progress()
    {
        var criterion = new MilestoneCriterion { Points = 100 };
        var old = new MilestoneCriterionAssessment { CriterionId = criterion.Id, Source = "AI", Outcome = "full",
            Note = MilestoneAutoAssessment.EvidenceVersion + "-superseded" };
        var current = new MilestoneCriterionAssessment { CriterionId = criterion.Id, Source = "AI", Outcome = "missing",
            Note = MilestoneAutoAssessment.EvidenceVersion };
        Assert.Equal((0m, (decimal?)0m, 0), MilestoneCriterionProgress.Calculate([criterion], [old, current]));
        var review = new MilestoneCriterionAssessment { CriterionId = criterion.Id, Source = "REVIEW", Outcome = "full" };
        Assert.Equal(100m, MilestoneCriterionProgress.Calculate([criterion], [old, current, review]).Confirmed);
    }
    [Fact]
    public void Status_follows_real_worker_events_and_persists_after_reload()
    {
        var milestone = Guid.NewGuid();
        var request = MilestoneAssessmentRequests.CreateRecheck(new() { MilestoneId = milestone, RunId = Guid.NewGuid() }, Guid.NewGuid());
        var now = request.CreatedAt;
        var marks = new List<AuditEntry>();
        Assert.Equal("idle", MilestoneAssessmentStatus.Describe(null, marks, milestone).State);
        Assert.Equal("queued", MilestoneAssessmentStatus.Describe(request, marks, milestone).State);
        marks.Add(new AuditEntry { EntityId = request.Id, Action = MilestoneAssessmentStatus.Started, CreatedAt = now.AddSeconds(1) });
        Assert.Equal("running", MilestoneAssessmentStatus.Describe(request, marks, milestone).State);
        marks.Add(new AuditEntry { EntityId = request.Id, Action = MilestoneAssessmentRequests.Done, CreatedAt = now.AddSeconds(2) });
        var completed = MilestoneAssessmentStatus.Describe(request, marks, milestone);
        Assert.Equal("completed", completed.State);
        Assert.Equal((DateTimeOffset?)now.AddSeconds(2), completed.CompletedAt);
        var newer = MilestoneAssessmentRequests.CreateRecheck(new() { MilestoneId = milestone, RunId = Guid.NewGuid() }, Guid.NewGuid());
        Assert.Equal("queued", MilestoneAssessmentStatus.Describe(newer, marks, milestone).State);
    }

    [Fact]
    public void A_failed_request_is_not_presented_as_success_even_when_terminal_markers_share_a_timestamp()
    {
        var milestone = Guid.NewGuid();
        var request = MilestoneAssessmentRequests.CreateComparison(Guid.NewGuid(), Guid.NewGuid());
        var time = request.CreatedAt.AddSeconds(1);
        var marks = new[] {
            new AuditEntry { EntityId = request.Id, Action = MilestoneAssessmentRequests.Done, CreatedAt = time },
            new AuditEntry { EntityId = request.Id, Action = MilestoneAssessmentStatus.Failed, CreatedAt = time }
        };
        Assert.Equal("failed", MilestoneAssessmentStatus.Describe(request, marks, milestone).State);
    }

    [Fact]
    public void A_completed_milestone_stays_complete_if_another_milestone_in_the_request_fails()
    {
        var milestone = Guid.NewGuid();
        var other = Guid.NewGuid();
        var request = MilestoneAssessmentRequests.CreateComparison(Guid.NewGuid(), Guid.NewGuid());
        var marks = new[] {
            new AuditEntry { EntityId = request.Id, Action = MilestoneAssessmentStatus.ItemDone,
                EntityName = $"MilestoneAssessmentItem:{milestone:N}", CreatedAt = request.CreatedAt.AddSeconds(1) },
            new AuditEntry { EntityId = request.Id, Action = MilestoneAssessmentStatus.Failed, CreatedAt = request.CreatedAt.AddSeconds(2) }
        };
        Assert.Equal("completed", MilestoneAssessmentStatus.Describe(request, marks, milestone).State);
        Assert.Equal("failed", MilestoneAssessmentStatus.Describe(request, marks, other).State);
    }

    [Fact]
    public void Response_size_failures_are_reported_as_size_errors_instead_of_connection_errors()
    {
        var action = MilestoneAssessmentStatus.FailureAction(new InvalidDataException(GitProgressComparisonResponse.GeneratedTooLarge));
        Assert.Equal(MilestoneAssessmentStatus.ResponseLimit, action);
        var request = MilestoneAssessmentRequests.CreateComparison(Guid.NewGuid(), Guid.NewGuid());
        var marker = new AuditEntry { EntityId = request.Id, Action = action };
        var status = MilestoneAssessmentStatus.Describe(request, [marker], Guid.NewGuid());
        Assert.Equal("failed", status.State);
        Assert.Equal(true, status.Message.Contains("giới hạn"));
        Assert.Equal(false, status.Message.Contains("kết nối"));
        Assert.Equal(MilestoneAssessmentStatus.Connection, MilestoneAssessmentStatus.FailureAction(new HttpRequestException()));
        Assert.Equal(MilestoneAssessmentStatus.InvalidResponse, MilestoneAssessmentStatus.FailureAction(new InvalidDataException("Sai ID bằng chứng")));
    }

}
