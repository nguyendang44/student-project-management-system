using System.Text.RegularExpressions;

namespace StudentProjects.Api.Services;

public sealed record CodeContextExcerpt(string CommitSha, string Version, string File,
    int StartLine, int EndLine, string Code, bool IsChangedFile);

public sealed record RepositoryCodeContext(IReadOnlyList<CodeContextExcerpt> Excerpts, string Status)
{
    public static RepositoryCodeContext Unavailable => new([], "unavailable");
}

/// <summary>
/// Bounded context for a local 7B model. Filename affinity is a retrieval heuristic,
/// not a resolved call graph. Excerpts are context, never independent proof of a new edit.
/// </summary>
public static class RepositoryContextBuilder
{
    public const int CharacterBudget = 6_000;
    private static readonly Regex Hunk = new(@"^@@ -(\d+)(?:,\d+)? \+(\d+)(?:,\d+)? @@", RegexOptions.Multiline);
    private static readonly Regex Words = new(@"[A-Z]?[a-z]+|[A-Z]+(?=[A-Z][a-z]|$)");
    private static readonly HashSet<string> Generic = new(StringComparer.OrdinalIgnoreCase)
        { "api", "service", "controller", "endpoint", "endpoints", "view", "test", "tests", "client", "cs", "ts", "vue", "js" };

    public static IReadOnlyList<string> SelectPaths(IEnumerable<string> eligible, IReadOnlyList<string> changed)
    {
        var paths = eligible.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var seeds = changed.Where(p => paths.Contains(p, StringComparer.Ordinal)).Distinct().Take(3).ToArray();
        var words = seeds.SelectMany(Tokens).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var related = paths.Where(p => !seeds.Contains(p, StringComparer.Ordinal))
            .Select(p => (Path: p, Score: Tokens(p).Count(words.Contains)))
            .Where(x => x.Score > 0).OrderByDescending(x => x.Score).ThenBy(x => x.Path, StringComparer.Ordinal)
            .Take(1).Select(x => x.Path);
        return seeds.Concat(related).ToArray();
    }

    private static IEnumerable<string> Tokens(string path) => Words.Matches(Path.GetFileNameWithoutExtension(path))
        .Select(m => m.Value).Where(w => w.Length > 2 && !Generic.Contains(w));

    public static async Task<RepositoryCodeContext> ReadAsync(GitHubSnapshotClient reader,
        string owner, string repo, ProgressDiff diff, CancellationToken ct)
    {
        var changed = GitProgressMeaningPolicy.SelectFiles(diff.Files, 10).Select(f => f.Path).ToArray();
        if (changed.Length == 0) return new([], "empty");
        // A shared deadline bounds extra GitHub requests; external cancellation still propagates.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(45));
        RepositorySnapshot? before = null, after = null;
        try { before = await reader.ReadAtCommitAsync(owner, repo, diff.BaseSha, deadline.Token, changed); }
        catch (Exception e) when (!ct.IsCancellationRequested && e is AnalysisInputException or HttpRequestException or OperationCanceledException) { }
        try { after = await reader.ReadAtCommitAsync(owner, repo, diff.HeadSha, deadline.Token, changed); }
        catch (Exception e) when (!ct.IsCancellationRequested && e is AnalysisInputException or HttpRequestException or OperationCanceledException) { }
        ct.ThrowIfCancellationRequested();
        return Build(diff, before, after);
    }

    public static RepositoryCodeContext Build(ProgressDiff diff, RepositorySnapshot? before, RepositorySnapshot? after)
    {
        var excerpts = new List<CodeContextExcerpt>();
        var remaining = CharacterBudget;
        var patches = diff.Files.ToDictionary(f => f.Path, StringComparer.Ordinal);
        var paths = (before?.Files ?? []).Concat(after?.Files ?? []).Select(f => f.Path).Distinct()
            .OrderByDescending(patches.ContainsKey).ToArray();
        foreach (var path in paths)
        {
            patches.TryGetValue(path, out var change);
            // Pair BEFORE/AFTER across several changed regions, within the global budget.
            var hunks = Hunk.Matches(change?.Patch ?? "").Take(3).ToArray();
            for (var region = 0; region < Math.Max(1, hunks.Length); region++)
            foreach (var (snapshot, version, side) in new[] { (before, "before", 1), (after, "after", 2) })
            {
                var source = snapshot?.Files.FirstOrDefault(f => f.Path == path);
                if (source is null || remaining < 80) continue;
                var lines = source.Lines;
                var match = hunks.ElementAtOrDefault(region);
                var anchor = match is not null && int.TryParse(match.Groups[side].Value, out var line) ? Math.Max(1, line) : 1;
                var start = source.Content.Length <= 750 ? 0
                    : Math.Clamp(anchor - 4, 0, Math.Max(0, lines.Length - 1));
                if (excerpts.Any(x => x.File == path && x.Version == version &&
                    x.StartLine <= start + 1 && x.EndLine >= anchor)) continue;
                var selected = new List<string>();
                var budget = Math.Min(750, remaining);
                foreach (var value in lines.Skip(start).Take(45))
                {
                    // Preserve complete lines and honest line ranges, never splice long source lines.
                    if (value.Length + 1 > budget) break;
                    selected.Add(value);
                    budget -= value.Length + 1;
                }
                if (selected.Count == 0) continue;
                var code = string.Join('\n', selected);
                remaining -= code.Length + 1;
                excerpts.Add(new(snapshot!.CommitSha, version, path, start + 1, start + selected.Count,
                    code, change is not null));
            }
        }
        return new(excerpts, excerpts.Count == 0 ? "unavailable" : before is null || after is null ? "partial" : "sampled");
    }
}
