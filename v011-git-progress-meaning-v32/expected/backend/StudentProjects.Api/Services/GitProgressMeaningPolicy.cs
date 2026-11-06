using System.Text.RegularExpressions;

namespace StudentProjects.Api.Services;

/// <summary>
/// Deterministic sampling and screening for student-work progress. A citation only
/// proves a changed source line was observed, never that the software works.
/// </summary>
public static class GitProgressMeaningPolicy
{
    private static readonly string[] AreaPriority = ["API", "AI/Backend", "Frontend", "Kiểm thử", "CSDL", "Backend khác"];
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex UnsupportedClaim = new(
        @"(?i)(đã hoàn thành|hoàn thiện toàn bộ|đã chạy thành công|đảm bảo (?:an toàn|bảo mật|đúng)|không còn lỗi|đã fix hoàn toàn|tests? (?:have )?passed|fully implemented|production.ready|security is proven)",
        RegexOptions.Compiled);
    private static readonly Regex EmptyObservation = new(
        @"(?i)(the (?:code )?snippet|the class .* is defined|(?:class|record|interface) .* is (?:defined|declared)|has a private|a sealed record|xml (?:comment|summary)|được (?:định nghĩa|khai báo)|khai báo (?:lớp|class)|chỉ là (?:comment|khai báo)|đoạn (?:mã|code) (?:này|trên)|phần (?:comment|chú thích)|tên (?:lớp|biến|class))",
        RegexOptions.Compiled);
    private static readonly string[] ActionWords = [
        "thêm", "bổ sung", "tạo ", "đăng ký", "xử lý", "ghi ", "lưu ", "gọi ",
        "kiểm tra", "tích hợp", "hiển thị", "cập nhật", "liên kết", "đọc ",
        "truy vấn", "giới hạn", "phân tích", "trả về", "xác thực", "kiểm thử",
        "lọc ", "chọn ", "so sánh", "định tuyến", "chuyển ", "cấu hình",
        "adds", "registers", "introduces", "checks", "validates", "renders",
        "returns", "calls", "stores", "filters", "routes", "updates", "compares"
    ];

    public static string AreaForPath(string path)
    {
        if (path.Contains("IntegrationTests/", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/tests/", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".spec.ts", StringComparison.OrdinalIgnoreCase)) return "Kiểm thử";
        if (path.StartsWith("frontend/", StringComparison.OrdinalIgnoreCase)) return "Frontend";
        if (path.Contains("/Migrations/", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("Persistence/", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/Entities/", StringComparison.OrdinalIgnoreCase)) return "CSDL";
        if (path.Contains("/Endpoints/", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith("/Program.cs", StringComparison.OrdinalIgnoreCase)) return "API";
        if (path.Contains("/Services/", StringComparison.OrdinalIgnoreCase)) return "AI/Backend";
        return "Backend khác";
    }

    private static bool CodeFile(string path) =>
        new[] { ".cs", ".ts", ".tsx", ".vue", ".js", ".jsx", ".py", ".go", ".java", ".sql" }
            .Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));

    private static bool Generated(string path) =>
        path.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith("ModelSnapshot.cs", StringComparison.OrdinalIgnoreCase) ||
        path.Contains(".min.", StringComparison.OrdinalIgnoreCase);

    /// <summary>Reject comment-only lines, pure declarations, imports and punctuation.</summary>
    public static bool IsFunctionalLine(string raw)
    {
        if (raw.Length < 12 || raw.Length > 210 || (raw[0] != '+' && raw[0] != '-')) return false;
        var line = raw[1..].Trim();
        if (line.Length < 10 || line.StartsWith("//", StringComparison.Ordinal) ||
            line.StartsWith("/*", StringComparison.Ordinal) || line.StartsWith('*') ||
            line.StartsWith('#') || line.StartsWith("using ", StringComparison.Ordinal) ||
            line.StartsWith("import ", StringComparison.Ordinal) ||
            line.StartsWith("namespace ", StringComparison.Ordinal) ||
            line.StartsWith("export type ", StringComparison.Ordinal) ||
            line.StartsWith("<!--", StringComparison.Ordinal) ||
            line.StartsWith("public class ", StringComparison.Ordinal) ||
            line.StartsWith("public static class ", StringComparison.Ordinal) ||
            line.StartsWith("public sealed class ", StringComparison.Ordinal) ||
            line.StartsWith("public sealed record ", StringComparison.Ordinal) ||
            line.StartsWith("private static readonly string[] ", StringComparison.Ordinal) ||
            line is "{" or "}" or "};" or "</summary>" or "<summary>" ||
            line.StartsWith("[JsonProperty", StringComparison.Ordinal)) return false;
        if (Regex.IsMatch(line, @"^(?:public|internal|private)\s+(?:static\s+)?(?:sealed\s+)?(?:class|record|interface|enum)\b"))
            return false;
        return true;
    }

    private static int LineScore(string line)
    {
        var text = line[1..];
        if (text.Contains("MapGet(", StringComparison.Ordinal) ||
            text.Contains("MapPost(", StringComparison.Ordinal) ||
            text.Contains("MapPut(", StringComparison.Ordinal) ||
            text.Contains("AddHostedService", StringComparison.Ordinal) ||
            text.Contains("AddHttpClient", StringComparison.Ordinal)) return 9;
        if (text.Contains("await ", StringComparison.Ordinal) ||
            text.Contains("Assert.", StringComparison.Ordinal) ||
            text.Contains("v-model", StringComparison.Ordinal) ||
            text.Contains("@click", StringComparison.Ordinal) ||
            text.Contains("CreateTable", StringComparison.Ordinal)) return 8;
        if (text.Contains("return ", StringComparison.Ordinal) ||
            text.Contains("if (", StringComparison.Ordinal) ||
            text.Contains("=>", StringComparison.Ordinal) ||
            text.Contains("JsonSerializer", StringComparison.Ordinal) ||
            text.Contains("HasIndex", StringComparison.Ordinal)) return 6;
        if (text.Contains("async ", StringComparison.Ordinal) ||
            text.Contains("function ", StringComparison.Ordinal) ||
            text.Contains("<RouterLink", StringComparison.Ordinal) ||
            text.Contains("DbSet<", StringComparison.Ordinal)) return 5;
        if (text.Contains('=') || text.Contains('(')) return 3;
        return 1;
    }

    /// <summary>Important source lines from a diff, not just the first 24 comments/declarations.</summary>
    public static IReadOnlyList<string> MeaningfulLines(ProgressFile file, int limit = 14)
    {
        if (Generated(file.Path) || !CodeFile(file.Path) || !GitProgressEvidencePolicy.SafePath(file.Path)) return [];
        return GitProgressEvidencePolicy.EvidenceLines(file)
            .Select((line, index) => new { line, index, score = LineScore(line) })
            .Where(x => IsFunctionalLine(x.line))
            .OrderByDescending(x => x.score)
            .ThenBy(x => x.index)
            .Take(limit)
            .OrderBy(x => x.index)
            .Select(x => x.line)
            .ToArray();
    }

    /// <summary>Round-robin through work areas; never fill the model input from one large file.</summary>
    public static IReadOnlyList<ProgressFile> SelectFiles(IEnumerable<ProgressFile> files, int maxFiles = 10)
    {
        var candidates = files
            .Where(f => f.Patch is not null && CodeFile(f.Path) && !Generated(f.Path))
            .Select(f => new { file = f, lines = MeaningfulLines(f) })
            .Where(x => x.lines.Count > 0)
            .ToArray();
        var buckets = AreaPriority.ToDictionary(area => area, area =>
            candidates.Where(x => AreaForPath(x.file.Path) == area)
                .OrderByDescending(x => x.lines.Sum(l => LineScore(l)))
                .ThenBy(x => x.file.Path, StringComparer.Ordinal)
                .Select(x => x.file)
                .ToArray(), StringComparer.Ordinal);
        var output = new List<ProgressFile>();
        for (var round = 0; round < 3 && output.Count < maxFiles; round++)
            foreach (var area in AreaPriority)
            {
                if (output.Count == maxFiles) break;
                if (round < buckets[area].Length) output.Add(buckets[area][round]);
            }
        return output;
    }

    // Diagnostic counts only. Never include diff text, model descriptions or credentials in logs.
    public sealed record RejectionDiagnostics(
        int UnknownId, int UnsentFile, int UnsentLine, int NonFunctionalLine,
        int WeakDescription, int Duplicate, int Eligible);

    /// <summary>
    /// Count the FIRST reason each parsed AI candidate would be rejected.
    /// This mirrors the checks in ScreenMeaningfulById without weakening them.
    /// </summary>
    public static RejectionDiagnostics ExplainRejectionsById(
        IEnumerable<ProgressEvidenceSelection> selections,
        IReadOnlyList<ProgressEvidenceReference> references,
        IReadOnlyList<ProgressFile> sent)
    {
        var index = references.GroupBy(r => r.Id, StringComparer.Ordinal)
            .Where(g => g.Count() == 1)
            .ToDictionary(g => g.Key, g => g.Single(), StringComparer.Ordinal);
        var allowed = sent.GroupBy(f => f.Path, StringComparer.Ordinal)
            .Where(g => g.Count() == 1)
            .ToDictionary(g => g.Key,
                g => GitProgressEvidencePolicy.EvidenceLines(g.Single()).ToHashSet(StringComparer.Ordinal),
                StringComparer.Ordinal);
        var valid = new List<ProgressEvidence>();
        var unknownId = 0;
        var unsentFile = 0;
        var unsentLine = 0;
        var nonFunctionalLine = 0;
        var weakDescription = 0;
        var duplicate = 0;
        var eligible = 0;
        foreach (var candidate in selections.Take(18))
        {
            if (!index.TryGetValue(candidate.EvidenceId, out var reference))
            {
                unknownId++;
                continue;
            }
            if (!allowed.TryGetValue(reference.File, out var changed))
            {
                unsentFile++;
                continue;
            }
            if (!changed.Contains(reference.Evidence))
            {
                unsentLine++;
                continue;
            }
            if (!IsFunctionalLine(reference.Evidence))
            {
                nonFunctionalLine++;
                continue;
            }
            if (!UsefulDescription(candidate.Description))
            {
                weakDescription++;
                continue;
            }
            if (valid.Any(v => v.File == reference.File && (v.Evidence == reference.Evidence ||
                v.Description.Equals(candidate.Description, StringComparison.OrdinalIgnoreCase))))
            {
                duplicate++;
                continue;
            }
            valid.Add(new ProgressEvidence(reference.File, reference.Evidence, candidate.Description.Trim()));
            eligible++;
        }
        return new RejectionDiagnostics(unknownId, unsentFile, unsentLine,
            nonFunctionalLine, weakDescription, duplicate, eligible);
    }

    public static bool UsefulDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description) || description.Length < 28 || description.Length > 350)
            return false;
        var normalized = Whitespace.Replace(description.Trim(), " ");
        if (UnsupportedClaim.IsMatch(normalized) || EmptyObservation.IsMatch(normalized)) return false;
        return ActionWords.Any(x => normalized.Contains(x, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Re-check a server-issued evidence ID against the exact excerpt actually sent;
    /// filter low-value observations; distribute max six results across work areas.
    /// </summary>
    public static IReadOnlyList<ProgressEvidence> ScreenMeaningfulById(
        IEnumerable<ProgressEvidenceSelection> selections,
        IReadOnlyList<ProgressEvidenceReference> references,
        IReadOnlyList<ProgressFile> sent)
    {
        var index = references.GroupBy(r => r.Id, StringComparer.Ordinal)
            .Where(g => g.Count() == 1)
            .ToDictionary(g => g.Key, g => g.Single(), StringComparer.Ordinal);
        var allowed = sent.GroupBy(f => f.Path, StringComparer.Ordinal)
            .Where(g => g.Count() == 1)
            .ToDictionary(g => g.Key,
                g => GitProgressEvidencePolicy.EvidenceLines(g.Single()).ToHashSet(StringComparer.Ordinal),
                StringComparer.Ordinal);
        var valid = new List<ProgressEvidence>();
        foreach (var candidate in selections.Take(18))
        {
            if (!index.TryGetValue(candidate.EvidenceId, out var reference) ||
                !allowed.TryGetValue(reference.File, out var changed) ||
                !changed.Contains(reference.Evidence) ||
                !IsFunctionalLine(reference.Evidence) ||
                !UsefulDescription(candidate.Description)) continue;
            if (valid.Any(v => v.File == reference.File && (v.Evidence == reference.Evidence ||
                v.Description.Equals(candidate.Description, StringComparison.OrdinalIgnoreCase)))) continue;
            valid.Add(new ProgressEvidence(reference.File, reference.Evidence, candidate.Description.Trim()));
        }
        var selected = new List<ProgressEvidence>();
        // First pass: at most one finding from each present work area.
        foreach (var area in AreaPriority)
        {
            var item = valid.FirstOrDefault(v => AreaForPath(v.File) == area);
            if (item is not null && selected.Count < 6) selected.Add(item);
        }
        // Second pass: at most two per file, preserving model order within each area.
        foreach (var item in valid)
        {
            if (selected.Count == 6) break;
            if (selected.Contains(item) || selected.Count(v => v.File == item.File) >= 2) continue;
            selected.Add(item);
        }
        return selected;
    }
}
