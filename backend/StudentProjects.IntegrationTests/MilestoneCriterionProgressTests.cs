using StudentProjects.Domain.Entities;
using Xunit;

namespace StudentProjects.IntegrationTests;

public sealed class MilestoneCriterionProgressTests
{
    [Fact]
    public void New_weeks_automatically_add_points_and_lecturer_decisions_override_ai()
    {
        var login = new MilestoneCriterion { Points = 30 };
        var topic = new MilestoneCriterion { Points = 25 };
        var test = new MilestoneCriterion { Points = 45 };
        var criteria = new[] { login, topic, test };
        var week1 = new DateTimeOffset(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);
        var week2 = week1.AddDays(7);
        var history = new List<MilestoneCriterionAssessment>
        {
            Assessment(login, "AI", "full", week1),
            Assessment(login, "REVIEW", "full", week1.AddHours(1)),
            Assessment(login, "AI", "missing", week2),
            Assessment(topic, "AI", "full", week2),
            Assessment(test, "AI", "partial", week2)
        };
        var proposed = MilestoneCriterionProgress.Calculate(criteria, history);
        Assert.Equal((55m, (decimal?)55m, 1), proposed);

        history.Add(Assessment(topic, "REVIEW", "full", week2.AddHours(1)));
        history.Add(Assessment(topic, "REVIEW", "full", week2.AddHours(2)));
        Assert.Equal((55m, (decimal?)55m, 2), MilestoneCriterionProgress.Calculate(criteria, history));

        history.Add(Assessment(topic, "REVIEW", "missing", week2.AddHours(3)));
        Assert.Equal((30m, (decimal?)55m, 2), MilestoneCriterionProgress.Calculate(criteria, history));
    }

    [Fact]
    public void Manual_evidence_can_be_confirmed_without_an_ai_run()
    {
        var design = new MilestoneCriterion { Points = 40 };
        var review = Assessment(design, "REVIEW", "full", DateTimeOffset.UtcNow);
        Assert.Equal((100m, (decimal?)null, 1), MilestoneCriterionProgress.Calculate([design], [review]));
        Assert.Equal((0m, (decimal?)null, 0), MilestoneCriterionProgress.Calculate([design], []));
    }

    [Fact]
    public void Total_130_is_normalized_and_deleted_work_is_excluded()
    {
        var first = new MilestoneCriterion { Points = 30 };
        var second = new MilestoneCriterion { Points = 100 };
        var history = new[] { Assessment(first, "REVIEW", "full", DateTimeOffset.UtcNow) };
        var progress = MilestoneCriterionProgress.Calculate([first, second], history);
        Assert.Equal(23.08m, progress.Confirmed);
        second.IsDeleted = true;
        Assert.Equal(100m, MilestoneCriterionProgress.Calculate([first, second], history).Confirmed);
        first.IsDeleted = true;
        Assert.Equal(0m, MilestoneCriterionProgress.Calculate([first, second], history).Confirmed);
    }

    [Fact]
    public void Editing_the_definition_requires_a_new_assessment_but_weight_changes_reuse_it()
    {
        var definition = new MilestoneCriterion { Points = 30 };
        var other = new MilestoneCriterion { Points = 100 };
        var previous = DateTimeOffset.UtcNow.AddDays(-1);
        var history = new List<MilestoneCriterionAssessment>
        {
            Assessment(definition, "AI", "full", previous),
            Assessment(definition, "REVIEW", "full", previous.AddMinutes(1))
        };
        definition.Points = 100;
        Assert.Equal(50m, MilestoneCriterionProgress.Calculate([definition, other], history).Confirmed);
        definition.DefinitionChangedAt = DateTimeOffset.UtcNow;
        Assert.Equal((0m, (decimal?)null, 0), MilestoneCriterionProgress.Calculate([definition, other], history));
        history.Add(Assessment(definition, "REVIEW", "full", definition.DefinitionChangedAt.Value.AddMinutes(1)));
        Assert.Equal(50m, MilestoneCriterionProgress.Calculate([definition, other], history).Confirmed);
    }

    [Fact]
    public void Ai_full_counts_once_and_survives_a_later_unrelated_week()
    {
        var item = new MilestoneCriterion { Points = 30 };
        var other = new MilestoneCriterion { Points = 100 };
        var now = DateTimeOffset.UtcNow;
        var history = new[] { Assessment(item, "AI", "full", now),
            Assessment(item, "AI", "full", now.AddDays(1)), Assessment(item, "AI", "missing", now.AddDays(7)) };
        Assert.Equal((23.08m, (decimal?)23.08m, 0), MilestoneCriterionProgress.Calculate([item, other], history));
        Assert.Equal("full", MilestoneCriterionProgress.EffectiveOutcome(item, history));
    }

    [Fact]
    public void Lecturer_rejection_wins_over_newer_ai_until_lecturer_changes_it()
    {
        var item = new MilestoneCriterion { Points = 100 };
        var now = DateTimeOffset.UtcNow;
        var history = new List<MilestoneCriterionAssessment> { Assessment(item, "AI", "full", now),
            Assessment(item, "REVIEW", "missing", now.AddMinutes(1)), Assessment(item, "AI", "full", now.AddDays(7)) };
        Assert.Equal((0m, (decimal?)100m, 1), MilestoneCriterionProgress.Calculate([item], history));
        history.Add(Assessment(item, "REVIEW", "full", now.AddDays(8)));
        Assert.Equal((100m, (decimal?)100m, 1), MilestoneCriterionProgress.Calculate([item], history));
    }

    [Fact]
    public void Partial_or_missing_never_earns_weight_and_ai_for_old_definition_is_ignored()
    {
        var item = new MilestoneCriterion { Points = 100 };
        var now = DateTimeOffset.UtcNow;
        var history = new List<MilestoneCriterionAssessment> { Assessment(item, "AI", "partial", now) };
        Assert.Equal((0m, (decimal?)0m, 0), MilestoneCriterionProgress.Calculate([item], history));
        history.Add(Assessment(item, "AI", "full", now.AddMinutes(1)));
        Assert.Equal(100m, MilestoneCriterionProgress.Calculate([item], history).Confirmed);
        item.DefinitionChangedAt = now.AddDays(1);
        Assert.Equal((0m, (decimal?)null, 0), MilestoneCriterionProgress.Calculate([item], history));
        Assert.Equal("missing", MilestoneCriterionProgress.EffectiveOutcome(item, history));
    }

    private static MilestoneCriterionAssessment Assessment(MilestoneCriterion criterion, string source,
        string outcome, DateTimeOffset time) => new()
        { CriterionId = criterion.Id, Source = source, Outcome = outcome, CreatedAt = time };
}
