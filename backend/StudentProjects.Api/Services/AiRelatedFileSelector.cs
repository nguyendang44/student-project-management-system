namespace StudentProjects.Api.Services;

/// <summary>
/// Selects a small, feature-coherent GitHub source sample. This is a filename-based
/// dependency heuristic, NOT a call graph or a claim of full repository coverage.
/// The caller must apply file-type, size and secret filtering before using it.
/// </summary>
public static class AiRelatedFileSelector
{
    private static readonly string[] Features = ["Auth", "GitHub", "Project", "Milestone", "Lecturer"];

    public sealed record ReviewGroup(string Name, IReadOnlyList<SourceFile> Files);

    public static string Feature(string path)
    {
        var p = path.Replace('\\', '/').ToLowerInvariant();
        var name = p.Split('/').Last();
        if (name is "program.cs" or "studentprojectsdbcontext.cs" or "client.ts" or "client.js")
            return "Shared";
        // Inspect filename and meaningful feature folders, not namespace folders:
        // "backend/StudentProjects.Api" itself must NOT classify every file as Project.
        if (name.Contains("github") || p.Contains("/features/github/")) return "GitHub";
        if (name.Contains("auth") || name.Contains("login") || name.Contains("jwt")
            || p.Contains("/features/auth/")) return "Auth";
        if (name.Contains("milestone") || p.Contains("/features/milestone/")) return "Milestone";
        if (name.Contains("lecturer") || name.Contains("capacity") || p.Contains("/features/lecturer/"))
            return "Lecturer";
        if (name.Contains("project") || name.Contains("topic") || name.Contains("registration")
            || p.Contains("/features/project/")) return "Project";
        return "Other";
    }

    private static bool IsTest(string p) =>
        p.Contains("test", StringComparison.OrdinalIgnoreCase) ||
        p.Contains("spec", StringComparison.OrdinalIgnoreCase);

    private static string Layer(string path)
    {
        var p = path.Replace('\\', '/').ToLowerInvariant();
        if (IsTest(p)) return "test";
        if (p.Contains("/endpoints/") || p.Contains("/controllers/")) return "endpoint";
        if (p.StartsWith("backend/") && p.Contains("/services/")) return "service";
        if (p.StartsWith("frontend/") && (p.Contains(".api.") || p.Contains("/api/"))) return "frontend-api";
        if (p.Contains("/contracts/") || p.Contains("/abstractions/")) return "contract";
        if (p.StartsWith("frontend/") && (p.Contains("/views/") || p.EndsWith(".vue"))) return "view";
        return "other";
    }

    private static int LayerPriority(string layer) => layer switch
    {
        "endpoint" => 0,
        "service" => 1,
        "frontend-api" => 2,
        "contract" => 3,
        "test" => 4,
        "view" => 5,
        _ => 6
    };

    /// <summary>
    /// Chooses up to two workflows with both API and companion files when possible.
    /// A workflow receives one file per source role first, then related extras.
    /// Returns only paths already supplied by the caller, in stable order.
    /// </summary>
    public static IReadOnlyList<string> SelectPaths(IEnumerable<string> eligiblePaths, int maxFiles = 12)
    {
        if (maxFiles < 1) return [];
        var paths = eligiblePaths.Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal).ToArray();
        var featureGroups = paths.Where(p => Features.Contains(Feature(p), StringComparer.Ordinal))
            .GroupBy(Feature)
            .Select(g => new
            {
                Name = g.Key,
                Paths = g.ToArray(),
                Roles = g.Select(Layer).Distinct(StringComparer.Ordinal).Count(),
                HasEndpoint = g.Any(p => Layer(p) == "endpoint")
            })
            .OrderByDescending(g => g.HasEndpoint)
            .ThenByDescending(g => g.Roles)
            .ThenBy(g => Array.IndexOf(Features, g.Name))
            .Take(2).ToArray();

        var chosen = new List<string>(maxFiles);
        void Add(string p)
        {
            if (chosen.Count < maxFiles && !chosen.Contains(p, StringComparer.Ordinal)) chosen.Add(p);
        }
        // A route and its siblings must be reviewed together. First pick one
        // candidate per role to prevent many endpoint files dominating the sample.
        foreach (var group in featureGroups)
        {
            foreach (var role in new[] { "endpoint", "service", "frontend-api", "contract", "test", "view" })
            {
                var p = group.Paths.FirstOrDefault(p => Layer(p) == role);
                if (p is not null && chosen.Count < maxFiles) Add(p);
                if (chosen.Count >= maxFiles) break;
            }
        }

        // Shared security/data-flow context is useful across feature boundaries.
        // It must still exist in the Git tree and pass the caller's safety checks.
        string[] sharedPreference = featureGroups.Any(g => g.Name is "Auth" or "GitHub")
            ? ["/program.cs", "/api/client.ts"]
            : ["/studentprojectsdbcontext.cs", "/program.cs"];
        foreach (var suffix in sharedPreference)
        {
            var shared = paths.FirstOrDefault(p => Feature(p) == "Shared" &&
                p.Replace('\\', '/').EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
            if (shared is not null) Add(shared);
        }
        // Fill remaining capacity from selected workflows, not unrelated random files.
        foreach (var group in featureGroups)
            foreach (var path in group.Paths.OrderBy(p => LayerPriority(Layer(p)))
                         .ThenBy(p => p, StringComparer.Ordinal)) Add(path);
        // Small repositories may not have named workflows. Keep bounded fallback.
        if (chosen.Count == 0)
            foreach (var p in paths.Where(p => Feature(p) != "Shared")) Add(p);
        return chosen;
    }

    /// <summary>Groups relevant backend, contract, frontend and test excerpts together.
    /// Shared files are attached only to workflows that need them, not to every call.</summary>
    public static IReadOnlyList<ReviewGroup> BuildReviewGroups(IReadOnlyList<SourceFile> files, int maxGroups)
    {
        if (maxGroups < 1) return [];
        var shared = files.Where(f => Feature(f.Path) == "Shared").ToArray();
        var groups = files.Where(f => Feature(f.Path) is not "Shared" and not "Other")
            .GroupBy(f => Feature(f.Path))
            .OrderBy(g => Array.IndexOf(Features, g.Key))
            .Take(maxGroups)
            .Select(g =>
            {
                var selected = g.ToList();
                var additions = shared.Where(f => UsefulSharedFor(g.Key, f.Path)).Take(2);
                selected.AddRange(additions);
                return new ReviewGroup(g.Key, selected.DistinctBy(f => f.Path).ToArray());
            }).ToList();
        if (groups.Count == 0)
        {
            var fallback = files.Take(6).ToArray();
            if (fallback.Length > 0) groups.Add(new ReviewGroup("General", fallback));
        }
        return groups;
    }

    private static bool UsefulSharedFor(string feature, string path)
    {
        var p = path.Replace('\\', '/').ToLowerInvariant();
        if (p.EndsWith("/program.cs")) return feature is "Auth" or "GitHub" or "Lecturer";
        if (p.EndsWith("/client.ts")) return feature is "Auth" or "GitHub";
        if (p.EndsWith("/studentprojectsdbcontext.cs")) return feature is "Project" or "Milestone" or "Lecturer";
        return false;
    }
}
