namespace StudentProjects.Domain.Entities;

public static class MilestoneCriterionProgress
{
    public static (decimal Confirmed, decimal? Proposed, int Reviewed) Calculate(
        IEnumerable<MilestoneCriterion> criteria, IEnumerable<MilestoneCriterionAssessment> assessments)
    {
        var items = criteria.Where(x => !x.IsDeleted).ToArray();
        var definitions = items.ToDictionary(x => x.Id);
        var current = assessments.Where(x => !(x.Source == "AI" && x.Note?.EndsWith("-superseded", StringComparison.Ordinal) == true) && definitions.TryGetValue(x.CriterionId, out var criterion) &&
            (!criterion.DefinitionChangedAt.HasValue || x.CreatedAt >= criterion.DefinitionChangedAt.Value)).ToArray();
        var confirmed = 0;
        var proposed = 0;
        var reviewed = 0;
        var hasProposal = false;
        foreach (var criterion in items)
        {
            var records = current.Where(x => x.CriterionId == criterion.Id).ToArray();
            var review = records.Where(x => x.Source == "REVIEW")
                .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).FirstOrDefault();
            // Incremental weekly reports: a later diff without this feature does not revoke
            // earlier valid evidence. Only a lecturer decision or a definition edit does.
            var ai = records.Where(x => x.Source == "AI").ToArray();
            if (review is not null) reviewed++;
            if (ai.Length > 0) hasProposal = true;
            if (ai.Any(x => x.Outcome == "full")) proposed += criterion.Points;
            if (EffectiveOutcome(criterion, records) == "full") confirmed += criterion.Points;
        }
        var total = items.Sum(x => x.Points);
        decimal Percent(int value) => total == 0 ? 0 : Math.Round(value * 100m / total, 2);
        return (Percent(confirmed), hasProposal ? Percent(proposed) : null, reviewed);
    }
    public static string EffectiveOutcome(MilestoneCriterion criterion,
        IEnumerable<MilestoneCriterionAssessment> assessments)
    {
        var current = assessments.Where(x => !(x.Source == "AI" && x.Note?.EndsWith("-superseded", StringComparison.Ordinal) == true) && x.CriterionId == criterion.Id && !criterion.IsDeleted &&
            (!criterion.DefinitionChangedAt.HasValue || x.CreatedAt >= criterion.DefinitionChangedAt.Value)).ToArray();
        var review = current.Where(x => x.Source == "REVIEW")
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).FirstOrDefault();
        if (review is not null) return review.Outcome;
        if (current.Any(x => x.Source == "AI" && x.Outcome == "full")) return "full";
        return current.Any(x => x.Source == "AI" && x.Outcome == "partial") ? "partial" : "missing";
    }
}
