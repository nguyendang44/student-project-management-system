using System.Text.RegularExpressions;

namespace StudentProjects.Api.Services;

/// <summary>
/// Conservative, deterministic facts extracted from validated ADDED diff lines.
/// Used only when Ollama produced no meaningful screened observations. No inference
/// about successful builds, working features, tests or security is permitted.
/// </summary>
public static class GitProgressGroundedFallback
{
    private static readonly Regex Route = new(
        @"\bMap(?<verb>Get|Post|Put|Delete|Patch)\s*\(\s*""(?<route>[^""]{1,96})""\s*,\s*(?<handler>[A-Za-z_]\w*)",
        RegexOptions.Compiled);
    private static readonly Regex Hosted = new(
        @"\bAddHostedService\s*<\s*(?<name>[A-Za-z_]\w*)\s*>", RegexOptions.Compiled);
    private static readonly Regex HttpClient = new(
        @"\bAddHttpClient\s*<\s*(?<name>[A-Za-z_]\w*)\s*>", RegexOptions.Compiled);
    private static readonly Regex DbSet = new(
        @"\bDbSet\s*<\s*(?<name>[A-Za-z_]\w*)\s*>", RegexOptions.Compiled);
    private static readonly Regex Table = new(
        @"\bCreateTable\s*\(\s*name:\s*""(?<name>[A-Za-z_]\w*)""", RegexOptions.Compiled);
    private static readonly Regex Test = new(
        @"\bAssert\.(?<method>[A-Za-z_]\w*)\s*\(", RegexOptions.Compiled);
    private static readonly Regex Click = new(
        @"@click=""(?<action>[A-Za-z_][A-Za-z_0-9.]*)(?<arguments>\([^""]{0,100}\))?""", RegexOptions.Compiled);
    private static readonly Regex Model = new(
        "v-model(?:\\.[a-z]+)*=\"(?<field>[A-Za-z_][A-Za-z_0-9.]*)\"", RegexOptions.Compiled);
    private static readonly Regex Router = new(
        "<RouterLink[^>]*\\bto=\"(?<route>/[^\"]{0,90})\"", RegexOptions.Compiled);
    private static readonly Regex Await = new(
        @"\bawait\s+(?:[A-Za-z_]\w*\.)+(?<method>[A-Za-z_]\w*Async)\s*\(", RegexOptions.Compiled);

    public static IReadOnlyList<ProgressEvidence> Extract(
        IReadOnlyList<ProgressEvidenceReference> references,
        IReadOnlyList<ProgressFile> sent)
    {
        // Restrict to EXACT lines that were sent to Ollama and that survived
        // path, secret and functional-line validation. References cannot be
        // forged by the model; they are assigned server-side.
        var allowed = sent.GroupBy(f => f.Path, StringComparer.Ordinal)
            .Where(g => g.Count() == 1)
            .ToDictionary(g => g.Key,
                g => GitProgressEvidencePolicy.EvidenceLines(g.Single()).ToHashSet(StringComparer.Ordinal),
                StringComparer.Ordinal);
        var candidates = new List<ProgressEvidence>();
        foreach (var reference in references)
        {
            if (!allowed.TryGetValue(reference.File, out var changed) ||
                !changed.Contains(reference.Evidence) ||
                !GitProgressMeaningPolicy.IsFunctionalLine(reference.Evidence) ||
                GitProgressMeaningPolicy.IsLowValueTestSetupLine(reference.File, reference.Evidence) ||
                !TryDescribeAddedLine(reference.File, reference.Evidence, out var description) ||
                !GitProgressMeaningPolicy.UsefulDescription(description)) continue;
            if (candidates.Any(e => e.File == reference.File && e.Evidence == reference.Evidence)) continue;
            candidates.Add(new ProgressEvidence(reference.File, reference.Evidence, description));
        }
        var areas = new[] { "API", "AI/Backend", "Frontend", "Kiểm thử", "CSDL", "Backend khác" };
        var result = new List<ProgressEvidence>();
        foreach (var area in areas)
        {
            var next = candidates.FirstOrDefault(e => GitProgressMeaningPolicy.AreaForPath(e.File) == area);
            if (next is not null && result.Count < 6) result.Add(next);
        }
        foreach (var entry in candidates)
        {
            if (result.Count >= 6) break;
            if (result.Contains(entry) || result.Count(e => e.File == entry.File) >= 2) continue;
            result.Add(entry);
        }
        return result;
    }

    // Exposed for conservative English -> Vietnamese presentation normalization.
    // It never processes a removed line, foreign path or unvalidated model claim.
    public static bool TryDescribeAddedLine(string file, string evidence, out string description)
    {
        description = "";
        // Removed lines show code that no longer exists. Do not describe them
        // as implemented work without surrounding context.
        if (!evidence.StartsWith('+') || evidence.StartsWith("+++", StringComparison.Ordinal)) return false;
        var text = evidence[1..].Trim();
        Match match;
        if ((match = Route.Match(text)).Success)
            description = $"Bổ sung khai báo route {match.Groups["verb"].Value.ToUpperInvariant()} " +
                $"{match.Groups["route"].Value} liên kết handler {match.Groups["handler"].Value}.";
        else if ((match = Hosted.Match(text)).Success)
            description = $"Bổ sung đăng ký dịch vụ chạy nền {match.Groups["name"].Value} trong cấu hình API.";
        else if ((match = HttpClient.Match(text)).Success)
            description = $"Bổ sung đăng ký HTTP client {match.Groups["name"].Value} trong cấu hình dịch vụ.";
        else if ((match = DbSet.Match(text)).Success && file.Contains("Persistence/", StringComparison.OrdinalIgnoreCase))
            description = $"Bổ sung DbSet cho thực thể {match.Groups["name"].Value} trong cấu hình dữ liệu.";
        else if ((match = Table.Match(text)).Success && file.Contains("/Migrations/", StringComparison.OrdinalIgnoreCase))
            description = $"Bổ sung thao tác tạo bảng {match.Groups["name"].Value} trong migration.";
        else if (text.Contains("migrationBuilder.CreateTable(", StringComparison.Ordinal) &&
            file.Contains("/Migrations/", StringComparison.OrdinalIgnoreCase))
            description = "Bổ sung lệnh CreateTable trong migration, thể hiện thay đổi cấu trúc cơ sở dữ liệu.";
        else if ((match = Test.Match(text)).Success && file.Contains("IntegrationTests/", StringComparison.OrdinalIgnoreCase))
            description = $"Bổ sung phép kiểm tra Assert.{match.Groups["method"].Value} trong mã kiểm thử.";
        else if (file.StartsWith("frontend/", StringComparison.OrdinalIgnoreCase) && (match = Click.Match(text)).Success)
            description = text.Contains("Gỡ liên kết", StringComparison.OrdinalIgnoreCase)
                ? $"Bổ sung nút Gỡ liên kết gọi {match.Groups["action"].Value}{match.Groups["arguments"].Value} khi nhấn."
                : $"Bổ sung sự kiện nhấn gọi {match.Groups["action"].Value}{match.Groups["arguments"].Value} trên giao diện.";
        else if (file.StartsWith("frontend/", StringComparison.OrdinalIgnoreCase) && (match = Model.Match(text)).Success)
            description = $"Bổ sung liên kết biểu mẫu v-model với {match.Groups["field"].Value} trên giao diện.";
        else if (file.StartsWith("frontend/", StringComparison.OrdinalIgnoreCase) && (match = Router.Match(text)).Success)
            description = $"Bổ sung liên kết điều hướng đến {match.Groups["route"].Value} trên giao diện.";
        else if ((match = Await.Match(text)).Success)
            description = $"Bổ sung lời gọi bất đồng bộ {match.Groups["method"].Value} trong luồng xử lý.";
        return description.Length > 0;
    }
}
