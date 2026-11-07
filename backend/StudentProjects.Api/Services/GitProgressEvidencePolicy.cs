using System.Text.RegularExpressions;

namespace StudentProjects.Api.Services;

public sealed record ProgressFile(string Path, string Status, int Additions, int Deletions, string? Patch);
// Files are the safe-to-inspect subset. Raw GitHub counts are kept separately so
// screening backups/secrets cannot silently change repository-wide statistics.
// Optional totals keep existing tests and historical callers source-compatible.
public sealed record ProgressDiff(string BaseSha, string HeadSha, int AheadBy,
    IReadOnlyList<ProgressFile> Files, bool PartialCoverage,
    int? GitFileCount = null, int? GitAdditions = null, int? GitDeletions = null)
{
    public int RawFileCount => GitFileCount ?? Files.Count;
    public int RawAdditions => GitAdditions ?? Files.Sum(f => f.Additions);
    public int RawDeletions => GitDeletions ?? Files.Sum(f => f.Deletions);
}

public sealed record ProgressEvidence(string File, string Evidence, string Description);
public sealed record ProgressEvidenceReference(string Id, string File, string Evidence);
public sealed record ProgressEvidenceSelection(string EvidenceId, string Description);
public sealed record ProgressAiResult(IReadOnlyList<ProgressEvidence> Items, int SentFiles, int CandidateCount = 0,
    GitProgressMeaningPolicy.RejectionDiagnostics? Diagnostics = null,
    bool RuleBasedFallback = false, int RawItemCount = 0);

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

    // Backup and generated artifacts can be part of GitHub's compare statistics,
    // but must never be cited as changes to the active application source.
    private static readonly Regex VersionedBackupDirectory = new(
        @"^v[0-9][a-z0-9._-]*[-_]backups?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly string[] BackupDirectories = [
        "backup", "backups", "_backup", "_backups", "__backup__", ".backups",
        "archive", "archives", "__old__"
    ];

    public static bool IsSha(string? value) => value is not null && ShaPattern.IsMatch(value);

    public static bool SafePath(string path)
    {
        if (string.IsNullOrEmpty(path) || path.Length > 300 || path.Contains('\\') || path.Contains("..")) return false;
        var pieces = path.ToLowerInvariant().Split('/');
        if (pieces.Any(p => ExcludedSegments.Any(bad => p == bad || p.StartsWith(bad + ".", StringComparison.Ordinal))))
            return false;
        // Reject only DIRECTORY segments, never a real BackupService.cs source file.
        if (pieces.Take(pieces.Length - 1).Any(p =>
            BackupDirectories.Contains(p, StringComparer.Ordinal) ||
            VersionedBackupDirectory.IsMatch(p))) return false;
        if (path.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith("ModelSnapshot.cs", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".generated.ts", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".g.ts", StringComparison.OrdinalIgnoreCase) ||
            path.Contains(".min.", StringComparison.OrdinalIgnoreCase)) return false;
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

    /// <summary>Counts source/test paths; does not imply Ollama saw their complete contents.</summary>
    public static bool IsAnalysisSourcePath(string path)
    {
        if (!SafePath(path) || !(path.StartsWith("backend/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("frontend/src/", StringComparison.OrdinalIgnoreCase))) return false;
        return new[] { ".cs", ".ts", ".tsx", ".vue", ".js", ".jsx", ".py", ".go", ".java", ".sql" }
            .Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
    }

    public static string ComposeSummary(ProgressDiff diff, int sentFiles, int groundedFindings)
    {
        // Raw totals are read BEFORE security/backup filtering in GitHubProgressClient.
        // Never infer API truncation merely from the number of files screened out.
        var rawCount = diff.RawFileCount;
        var retainedCount = diff.Files.Count;
        var excludedCount = Math.Max(0, rawCount - retainedCount);
        var sourceCount = diff.Files.Count(f => IsAnalysisSourcePath(f.Path));
        var otherCount = retainedCount - sourceCount;
        var scope = diff.PartialCoverage ? "Danh sách file GitHub trả về có thể chưa đầy đủ do giới hạn API. " : "";
        // Breakdown applies ONLY to the filtered subset, not to the full Git diff.
        var groups = diff.Files.GroupBy(f =>
            f.Path.StartsWith("backend/StudentProjects.IntegrationTests/", StringComparison.OrdinalIgnoreCase) ? "kiểm thử" :
            f.Path.Contains("/Migrations/", StringComparison.OrdinalIgnoreCase) ? "migration" :
            f.Path.StartsWith("backend/", StringComparison.OrdinalIgnoreCase) ? "backend" :
            f.Path.StartsWith("frontend/", StringComparison.OrdinalIgnoreCase) ? "frontend" :
            f.Path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? "tài liệu" : "khác")
            .Select(g => $"{g.Key}: {g.Count()} file");
        var breakdown = string.Join(", ", groups);
        return $"GitHub trả về {diff.AheadBy} commit mới, {rawCount} file thay đổi " +
            $"(+{diff.RawAdditions}/-{diff.RawDeletions} dòng) trước khi lọc. " +
            $"Phạm vi danh sách chi tiết sau lọc: {retainedCount} file; " +
            $"loại {excludedCount} file theo chính sách đường dẫn/bản sao lưu; " +
            $"mã nguồn ứng dụng/kiểm thử: {sourceCount} file, file khác: {otherCount}. " +
            (breakdown.Length == 0 ? "" : $"Phân loại trong {retainedCount} file giữ lại: {breakdown}. ") +
            $"Đã gửi các đoạn diff của {sentFiles} file cho AI; {groundedFindings} nhận xét khớp dòng diff. " +
            scope + "Đây là bằng chứng thay đổi mã nguồn, không chứng minh chức năng đã chạy đúng hoặc hoàn thành đồ án.";
    }
}
