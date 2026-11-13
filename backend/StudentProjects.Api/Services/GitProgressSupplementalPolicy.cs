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
    /// Rank concrete behavior over declarations/metadata before the per-file cap.
    /// Rank never proves runtime success, only which independent diff detail is
    /// more informative when the report has limited space.
    /// </summary>
    public static int Priority(ProgressEvidence item)
    {
        var line = item.Evidence;
        if (line.Contains("MapGet(", StringComparison.Ordinal) ||
            line.Contains("MapPost(", StringComparison.Ordinal) ||
            line.Contains("MapPut(", StringComparison.Ordinal) ||
            line.Contains("MapDelete(", StringComparison.Ordinal) ||
            line.Contains("@click=", StringComparison.Ordinal) ||
            line.Contains("CreateTable", StringComparison.OrdinalIgnoreCase)) return 10;
        if (line.Contains("v-model=", StringComparison.Ordinal) ||
            line.Contains("GetAsync(", StringComparison.Ordinal) ||
            line.Contains("PostAsync(", StringComparison.Ordinal) ||
            line.Contains("await ", StringComparison.Ordinal)) return 8;
        if (line.Contains("request<", StringComparison.Ordinal) ||
            line.Contains("request(", StringComparison.Ordinal) ||
            line.Contains("Assert.", StringComparison.Ordinal) ||
            line.Contains("return ", StringComparison.Ordinal)) return 7;
        if (line.Contains("=>", StringComparison.Ordinal)) return 5;
        if (line.Contains("interface ", StringComparison.Ordinal) ||
            line.Contains("record ", StringComparison.Ordinal) ||
            line.Contains("const ", StringComparison.Ordinal)) return 2;
        return 3;
    }

    /// <summary>
    /// An already reported group covers its non-independent wiring details.
    /// This is intentionally narrow: an @click action not included in primary
    /// citations is kept, and unrelated service work is not hidden.
    /// </summary>
    public static (IReadOnlyList<ProgressEvidence> Kept, int GroupedCount) CollapseFeatureOverlap(
        IReadOnlyList<ProgressEvidence> candidates, IReadOnlyList<ProgressFeatureFact> features)
    {
        var cited = features.SelectMany(f => f.Citations)
            .Select(c => (c.File, c.Evidence))
            .ToHashSet();
        var frontendCitations = features.Where(f => f.Area == "Frontend")
            .SelectMany(f => f.Citations).ToArray();
        var hasApi = features.Any(f => f.Area == "API");
        var kept = new List<ProgressEvidence>();
        var grouped = 0;
        foreach (var item in candidates)
        {
            if (cited.Contains((item.File, item.Evidence)) ||
                IsSupportingWiring(item, frontendCitations, hasApi)) grouped++;
            else kept.Add(item);
        }
        return (kept, grouped);
    }

    private static bool IsSupportingWiring(ProgressEvidence item,
        IReadOnlyList<ProgressFeatureCitation> frontendCitations, bool hasApi)
    {
        var path = item.File;
        var line = item.Evidence;
        if (frontendCitations.Count > 0 &&
            path.StartsWith("frontend/src/", StringComparison.OrdinalIgnoreCase))
        {
            // A route import is related only when it actually names a cited view.
            if (path.EndsWith("/router.ts", StringComparison.OrdinalIgnoreCase) &&
                frontendCitations.Any(c => c.File.Contains("/views/", StringComparison.OrdinalIgnoreCase) &&
                    line.Contains(Path.GetFileName(c.File), StringComparison.OrdinalIgnoreCase)))
                return true;

            // Same feature module: API client and type wiring support the view/API
            // integration already recognized, rather than creating extra features.
            const string prefix = "frontend/src/features/";
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var rest = path[prefix.Length..];
                var slash = rest.IndexOf('/');
                if (slash > 0)
                {
                    var module = rest[..slash];
                    if (frontendCitations.Any(c =>
                        c.File.Contains("/features/" + module + "/", StringComparison.OrdinalIgnoreCase) ||
                        c.File.Contains(module, StringComparison.OrdinalIgnoreCase)))
                        return path.EndsWith(".types.ts", StringComparison.OrdinalIgnoreCase) ||
                            (path.EndsWith(".api.ts", StringComparison.OrdinalIgnoreCase) &&
                             (line.Contains("request<", StringComparison.Ordinal) ||
                              line.Contains("request(", StringComparison.Ordinal)));
                }
            }
        }
        if (hasApi && path.StartsWith("backend/", StringComparison.OrdinalIgnoreCase))
        {
            if (path.EndsWith("/Program.cs", StringComparison.OrdinalIgnoreCase) &&
                (line.Contains("builder.Services.Add", StringComparison.Ordinal) ||
                 line.Contains("AddMemoryCache(", StringComparison.Ordinal))) return true;
            if (path.Contains("/Endpoints/", StringComparison.OrdinalIgnoreCase) &&
                line.Contains("static void Map", StringComparison.Ordinal)) return true;
        }
        return false;
    }
}
