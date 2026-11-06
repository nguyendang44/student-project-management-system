using System.Text.RegularExpressions;

namespace StudentProjects.Api.Services;

public sealed record ProgressFile(string Path, string Status, int Additions, int Deletions, string? Patch);
public sealed record ProgressDiff(string BaseSha, string HeadSha, int AheadBy,
    IReadOnlyList<ProgressFile> Files, bool PartialCoverage);
public sealed record ProgressEvidence(string File, string Evidence, string Description);
public sealed record ProgressEvidenceReference(string Id, string File, string Evidence);
public sealed record ProgressEvidenceSelection(string EvidenceId, string Description);
public sealed record ProgressAiResult(IReadOnlyList<ProgressEvidence> Items, int SentFiles, int CandidateCount = 0,
    GitProgressMeaningPolicy.RejectionDiagnostics? Diagnostics = null,
    bool RuleBasedFallback = false);

/// <summary>Only exact code changes provided to the model can support a progress claim.</summary>
public static class GitProgressEvidencePolicy
{
    private static readonly Regex ShaPattern = new("^[0-9a-fA-F]{40}$", RegexOptions.Compiled);
    private static readonly Regex SensitiveValue = new(
        @"(?i)(-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----|gh[pousr]_[a-z0-9_]{20,}|sk-[a-z0-9_-]{20,}|(?:api[_-]?key|secret|password|access[_-]?token|authorization|connectionstring)\s*[:=]\s*\S{6,})",
        RegexOptions.Compiled);
    private static readonly string[] ExcludedSegments = [
        ".env", "secrets", "credentials", "private", "node_modules", "vendor", "dist", "bin", "obj", ".git"
    ];

    public static bool IsSha(string? value) => value is not null && ShaPattern.IsMatch(value);

    public static bool SafePath(string path)
    {
        if (string.IsNullOrEmpty(path) || path.Length > 300 || path.Contains('\\') || path.Contains("..")) return false;
        var pieces = path.ToLowerInvariant().Split('/');
        if (pieces.Any(p => ExcludedSegments.Any(bad => p == bad || p.StartsWith(bad + ".", StringComparison.Ordinal))))
            return false;
        if (path.EndsWith(".pem", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".p12", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".pfx", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".key", StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    public static IReadOnlyList<string> EvidenceLines(ProgressFile file, int maxLines = 160)
    {
        if (!SafePath(file.Path) || string.IsNullOrEmpty(file.Patch)) return [];
        var items = new List<string>();
        foreach (var line in file.Patch.Replace("\r\n", "\n").Split('\n'))
        {
            if (line.Length < 8 || line.Length > 210 ||
                !(line.StartsWith('+') || line.StartsWith('-')) ||
                line.StartsWith("+++") || line.StartsWith("---") || SensitiveValue.IsMatch(line)) continue;
            if (items.Count < Math.Clamp(maxLines, 1, 500)) items.Add(line);
        }
        return items;
    }

    public static IReadOnlyList<ProgressEvidence> Screen(
        IEnumerable<ProgressEvidence> suggested, IReadOnlyList<ProgressFile> sent)
    {
        var lines = sent.ToDictionary(f => f.Path, f => EvidenceLines(f).ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);
        var kept = new List<ProgressEvidence>();
        foreach (var item in suggested.Take(12))
        {
            if (string.IsNullOrWhiteSpace(item.Description) || item.Description.Length > 350 ||
                string.IsNullOrWhiteSpace(item.Evidence) || item.Evidence.Length > 210 ||
                !lines.TryGetValue(item.File, out var accepted) || !accepted.Contains(item.Evidence)) continue;
            if (kept.Any(x => x.File == item.File && x.Evidence == item.Evidence)) continue;
            kept.Add(item);
            if (kept.Count == 6) break;
        }
        return kept;
    }

    /// <summary>
    /// Resolve ONLY IDs generated for excerpts actually sent to Ollama, then enforce the
    /// original exact-line check. An invented ID, secret or unsent line cannot pass.
    /// </summary>
    public static IReadOnlyList<ProgressEvidence> ScreenById(
        IEnumerable<ProgressEvidenceSelection> suggested,
        IReadOnlyList<ProgressEvidenceReference> references,
        IReadOnlyList<ProgressFile> sent)
    {
        var index = references.GroupBy(r => r.Id, StringComparer.Ordinal)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);
        var mapped = new List<ProgressEvidence>();
        foreach (var suggestion in suggested.Take(12))
        {
            if (!index.TryGetValue(suggestion.EvidenceId, out var reference)) continue;
            mapped.Add(new ProgressEvidence(reference.File, reference.Evidence, suggestion.Description));
        }
        return Screen(mapped, sent);
    }

    public static string ComposeSummary(ProgressDiff diff, int sentFiles, int groundedFindings)
    {
        var additions = diff.Files.Sum(f => f.Additions);
        var deletions = diff.Files.Sum(f => f.Deletions);
        var scope = diff.PartialCoverage ? "Danh sách file GitHub trả về có thể chưa đầy đủ. " : "";
        // These categories are computed from GitHub metadata, not generated by AI.
        var groups = diff.Files.GroupBy(f =>
            f.Path.StartsWith("backend/StudentProjects.IntegrationTests/", StringComparison.OrdinalIgnoreCase) ? "kiểm thử" :
            f.Path.Contains("/Migrations/", StringComparison.OrdinalIgnoreCase) ? "migration" :
            f.Path.StartsWith("backend/", StringComparison.OrdinalIgnoreCase) ? "backend" :
            f.Path.StartsWith("frontend/", StringComparison.OrdinalIgnoreCase) ? "frontend" :
            f.Path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? "tài liệu" : "khác")
            .Select(g => $"{g.Key}: {g.Count()} file");
        var breakdown = string.Join(", ", groups);
        return $"GitHub xác nhận {diff.AheadBy} commit mới so với mốc trước; dữ liệu trả về có " +
            $"{diff.Files.Count} file thay đổi (+{additions}/-{deletions} dòng). " +
            (breakdown.Length == 0 ? "" : $"Theo đường dẫn: {breakdown}. ") +
            $"Đã gửi các đoạn diff của {sentFiles} file cho AI; {groundedFindings} nhận xét khớp dòng diff. " +
            scope + "Đây là bằng chứng thay đổi mã nguồn, không chứng minh chức năng đã chạy đúng hoặc hoàn thành đồ án.";
    }
}
