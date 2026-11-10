namespace StudentProjects.Api.Services;

/// <summary>
/// A code line about a test request/assertion can substantiate the grouped test,
/// but should not become an independent student achievement. No line/claim is
/// synthesized here: this only hides duplicates already covered by a Git fact.
/// </summary>
public static class GitProgressSupplementalPolicy
{
    public static (IReadOnlyList<ProgressEvidence> Kept, int GroupedCount) CollapseTestDetails(
        IReadOnlyList<ProgressEvidence> candidates, IReadOnlyList<ProgressFeatureFact> features)
    {
        var groupedTestFiles = features.Where(f => f.Area == "Kiểm thử")
            .SelectMany(f => f.Citations.Select(c => c.File))
            .ToHashSet(StringComparer.Ordinal);
        var kept = new List<ProgressEvidence>();
        var collapsed = 0;
        foreach (var candidate in candidates)
        {
            var technical = GitProgressMeaningPolicy.AreaForPath(candidate.File) == "Kiểm thử" &&
                (candidate.Evidence.Contains("Assert.", StringComparison.Ordinal) ||
                 candidate.Evidence.Contains("PostAsJsonAsync(", StringComparison.Ordinal) ||
                 candidate.Evidence.Contains("PutAsJsonAsync(", StringComparison.Ordinal) ||
                 candidate.Evidence.Contains("GetAsync(", StringComparison.Ordinal) ||
                 candidate.Evidence.Contains("DeleteAsync(", StringComparison.Ordinal));
            if (technical && groupedTestFiles.Contains(candidate.File))
                collapsed++;
            else
                kept.Add(candidate);
        }
        return (kept, collapsed);
    }
    /// <summary>
    /// Hide one-line supplements which cite exactly the same changed line as
    /// a primary feature fact. A different added UI action remains independent.
    /// This does not compare broad keywords and never fabricates work items.
    /// </summary>
    public static (IReadOnlyList<ProgressEvidence> Kept, int GroupedCount) CollapseFeatureOverlap(
        IReadOnlyList<ProgressEvidence> candidates, IReadOnlyList<ProgressFeatureFact> features)
    {
        var cited = features.SelectMany(f => f.Citations)
            .Select(c => (c.File, c.Evidence))
            .ToHashSet();
        var kept = new List<ProgressEvidence>();
        var grouped = 0;
        foreach (var item in candidates)
        {
            if (cited.Contains((item.File, item.Evidence))) grouped++;
            else kept.Add(item);
        }
        return (kept, grouped);
    }
}
