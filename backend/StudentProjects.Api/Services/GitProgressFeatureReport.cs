namespace StudentProjects.Api.Services;

/// <summary>
/// Persist each Git-confirmed feature only once; an optional AI explanation
/// is its child, never a distinct task or an AI-created evidence line.
/// Keeps the existing JSON shape and uses no database migration.
/// </summary>
public static class GitProgressFeatureReport
{
    public static IReadOnlyList<object> Build(
        IReadOnlyList<ProgressFeatureFact> features,
        IReadOnlyList<ProgressFeatureExplanation> explanations)
    {
        var byId = explanations.GroupBy(x => x.FactId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Explanation, StringComparer.Ordinal);
        return features.Select((f, i) => (object)new
        {
            file = f.Citations[0].File,
            area = f.Area,
            title = f.Title,
            evidence = f.Citations[0].Evidence,
            description = f.Description,
            factId = $"F{i + 1}",
            aiExplanation = byId.GetValueOrDefault($"F{i + 1}"),
            citations = f.Citations.Select(c => new { file = c.File, evidence = c.Evidence }).ToArray(),
            source = "feature-rule"
        }).ToArray();
    }
}
