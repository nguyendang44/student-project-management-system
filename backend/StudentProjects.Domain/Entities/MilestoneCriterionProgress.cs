namespace StudentProjects.Domain.Entities;

public static class MilestoneCriterionProgress
{
    public static (decimal Confirmed, decimal? Proposed, int Reviewed) Calculate(
        IEnumerable<MilestoneCriterion> criteria, IEnumerable<MilestoneCriterionAssessment> assessments)
    {
        var items = criteria.Where(x => !x.IsDeleted).ToArray();
        var definitions = items.ToDictionary(x => x.Id);
        var latest = assessments.Where(x => definitions.TryGetValue(x.CriterionId, out var criterion) &&
            (!criterion.DefinitionChangedAt.HasValue || x.CreatedAt >= criterion.DefinitionChangedAt.Value)).GroupBy(x => (x.CriterionId, x.Source)).ToDictionary(
            group => group.Key, group => group.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).First());
        var confirmed = 0;
        var proposed = 0;
        var reviewed = 0;
        var hasProposal = false;
        foreach (var criterion in items)
        {
            var review = latest.GetValueOrDefault((criterion.Id, "REVIEW"));
            var proposal = latest.GetValueOrDefault((criterion.Id, "AI"));
            if (review is not null) reviewed++;
            if (proposal is not null) hasProposal = true;
            if (review?.Outcome == "full")
            {
                confirmed += criterion.Points;
                proposed += criterion.Points;
            }
            else if (proposal?.Outcome == "full" && (review is null || proposal.CreatedAt > review.CreatedAt))
            {
                proposed += criterion.Points;
            }
        }
        var total = items.Sum(x => x.Points);
        decimal Percent(int value) => total == 0 ? 0 : Math.Round(value * 100m / total, 2);
        return (Percent(confirmed), hasProposal ? Percent(proposed) : null, reviewed);
    }
}
