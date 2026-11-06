using System.Text.RegularExpressions;

namespace StudentProjects.Api.Services;

/// <summary>A model's phrasing is never allowed to create or amend a Git citation.</summary>
public sealed record ProgressFeatureExplanation(string FactId, string Explanation);

public static class GitProgressAiExplanationPolicy
{
    private static readonly Regex Tokens = new(@"[\p{L}\p{Nd}_]{4,}", RegexOptions.Compiled);
    private static readonly Regex Route = new(@"/[A-Za-z0-9_{}:/.-]{2,}", RegexOptions.Compiled);
    private static readonly Regex UnsupportedClaims = new(
        @"(?i)(đã\s+(?:chạy\s+thành\s+công|hoàn\s+thành|kiểm\s+thử\s+thành\s+công|triển\s+khai\s+thành\s+công)|(?:100\s*%|không\s+có\s+lỗi|an\s+toàn\s+tuyệt\s+đối|pass(?:ed)?\s+(?:all\s+)?tests?))",
        RegexOptions.Compiled);
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "bổ", "sung", "những", "thông", "trong", "được", "thêm", "cũng", "trình", "thực",
        "hiện", "thay", "đổi", "chức", "năng", "phần", "mềm", "giúp", "người", "dùng",
        "đoạn", "điều", "này", "theo", "các", "việc", "bằng", "chứng", "từng", "trên",
        "thành", "công", "kiểm", "tra", "chưa", "nhận", "diện", "thêm", "không"
    };

    public static bool TryResolve(string factId, IReadOnlyList<ProgressFeatureFact> facts,
        out ProgressFeatureFact fact)
    {
        fact = default!;
        if (factId.Length < 2 || factId[0] != 'F' ||
            !int.TryParse(factId.AsSpan(1), out var index) ||
            index < 1 || index > Math.Min(7, facts.Count)) return false;
        var candidate = facts[index - 1];
        if (candidate.Citations.Count == 0) return false;
        fact = candidate;
        return true;
    }

    public static IReadOnlyList<ProgressFeatureExplanation> Screen(
        IEnumerable<ProgressFeatureExplanation> candidates,
        IReadOnlyList<ProgressFeatureFact> facts)
    {
        var found = new List<ProgressFeatureExplanation>();
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var candidate in candidates.Take(14))
        {
            if (!TryResolve(candidate.FactId, facts, out var fact) ||
                used.Contains(candidate.FactId)) continue;
            var explanation = candidate.Explanation.Trim();
            if (explanation.Length < 35 || explanation.Length > 260 ||
                explanation.Contains('<') || explanation.Contains('>') ||
                explanation.Contains("http://", StringComparison.OrdinalIgnoreCase) ||
                explanation.Contains("https://", StringComparison.OrdinalIgnoreCase) ||
                UnsupportedClaims.IsMatch(explanation)) continue;
            var supported = fact.Title + " " + fact.Description + " " +
                string.Join(" ", fact.Citations.Select(c => c.Evidence));
            // A route written by the AI must have appeared in the real cited edit.
            if (Route.Matches(explanation).Cast<Match>().Any(r =>
                !supported.Contains(r.Value, StringComparison.OrdinalIgnoreCase))) continue;
            // Explanations must still discuss the specific work item, not generic praise.
            var terms = Tokens.Matches(supported).Cast<Match>().Select(m => m.Value)
                .Where(t => !StopWords.Contains(t)).Distinct(StringComparer.OrdinalIgnoreCase);
            if (!terms.Any(t => explanation.Contains(t, StringComparison.OrdinalIgnoreCase))) continue;
            used.Add(candidate.FactId);
            found.Add(new ProgressFeatureExplanation(candidate.FactId, explanation));
            if (found.Count >= 7) break;
        }
        return found;
    }
}
