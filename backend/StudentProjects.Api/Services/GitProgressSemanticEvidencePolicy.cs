using System.Text.RegularExpressions;

namespace StudentProjects.Api.Services;

/// <summary>
/// A source-line citation must substantiate the *named action*, not merely be a
/// real line from the same file. Conservative rules only: unclear cases stay
/// unverified, and an unrelated line cannot establish a method/API/table.
/// </summary>
public static class GitProgressSemanticEvidencePolicy
{
    private static readonly Regex NamedSymbol = new(
        @"(?i)\b(?:phương\s+thức|hàm|method|function|interface|lớp|class|record|cấu\s+trúc\s+dữ\s+liệu)\s+[`'"" ]*(?<name>[A-Za-z_][A-Za-z0-9_]{1,95})\b",
        RegexOptions.Compiled);

    private static readonly Regex FrontendServerCreation = new(
        @"(?i)\b(?:bổ\s+sung|tạo|định\s+nghĩa|khai\s+báo|triển\s+khai)\s+(?:API|endpoint|route|tuyến\s+API)\b",
        RegexOptions.Compiled);

    private static readonly Regex ClientClaim = new(
        @"(?i)\b(?:http\s+client|github\s+client|client\s+github|client\s+HTTP)\b",
        RegexOptions.Compiled);

    /// <summary>True when one explicit, verifiable claim is missing from the cited added line.</summary>
    public static bool HasUnsupportedClaim(string path, string evidence, string description)
    {
        if (!evidence.StartsWith('+') || evidence.StartsWith("+++", StringComparison.Ordinal)) return true;
        var code = evidence[1..];
        var desc = description.Trim();
        foreach (Match match in NamedSymbol.Matches(desc))
        {
            var symbol = match.Groups["name"].Value;
            // Natural language following a noun isn't a symbol identifier.
            if (symbol.Equals("để", StringComparison.OrdinalIgnoreCase) ||
                symbol.Equals("cho", StringComparison.OrdinalIgnoreCase) ||
                symbol.Equals("mới", StringComparison.OrdinalIgnoreCase) ||
                symbol.Equals("và", StringComparison.OrdinalIgnoreCase) ||
                symbol.Equals("gồm", StringComparison.OrdinalIgnoreCase)) continue;
            if (!Regex.IsMatch(symbol, @"^[A-Za-z_][A-Za-z0-9_]*$")) continue;
            // Avoid mistaking ordinary English words ("function to ...")
            // for named symbols. We only enforce explicitly code-like names.
            if (!symbol.Any(char.IsUpper) && !symbol.Contains('_') &&
                !symbol.Any(char.IsDigit)) continue;
            if (!Regex.IsMatch(code, @"(?<![A-Za-z0-9_])" + Regex.Escape(symbol) + @"(?![A-Za-z0-9_])",
                    RegexOptions.IgnoreCase)) return true;
        }

        // Frontend metadata and HTTP calls are not backend route declarations.
        // 'Bổ sung lời gọi API ...' remains a valid description of a real call.
        if ((path.StartsWith("frontend/", StringComparison.OrdinalIgnoreCase) ||
             path.StartsWith("scripts/", StringComparison.OrdinalIgnoreCase)) &&
            FrontendServerCreation.IsMatch(desc) &&
            !code.Contains("MapGet(", StringComparison.Ordinal) &&
            !code.Contains("MapPost(", StringComparison.Ordinal)) return true;

        // Do not infer two service registrations from one AddMemoryCache line.
        if (ClientClaim.IsMatch(desc) &&
            !code.Contains("client", StringComparison.OrdinalIgnoreCase) &&
            !code.Contains("GitHub", StringComparison.OrdinalIgnoreCase)) return true;

        // A line that adds a memory cache does not independently establish a GitHub client.
        if (desc.Contains("client", StringComparison.OrdinalIgnoreCase) &&
            code.Contains("AddMemoryCache(", StringComparison.Ordinal)) return true;
        return false;
    }

    /// <summary>
    /// Structural scaffolding is still shown to Ollama as diff context, but it
    /// isn't offered as a stand-alone evidence ID or a student achievement.
    /// </summary>
    public static bool IsSupportingOnlyLine(string path, string raw)
    {
        if (!raw.StartsWith('+') || raw.StartsWith("+++", StringComparison.Ordinal)) return false;
        var code = raw[1..].Trim();
        if (path.Contains("IntegrationTests/", StringComparison.OrdinalIgnoreCase) &&
            Regex.IsMatch(code, @"^(?:(?:private|public|internal)\s+)?(?:static\s+)?(?:readonly\s+)?(?:const\s+)?(?:string|Guid)\s+(?:[A-Za-z_]\w*)\s*=", RegexOptions.IgnoreCase))
            return true;
        if (path.EndsWith(".vue", StringComparison.OrdinalIgnoreCase) &&
            (code.StartsWith("<script setup", StringComparison.OrdinalIgnoreCase) ||
             Regex.IsMatch(code, @"^(?:defineProps|defineEmits)\s*<") ||
             Regex.IsMatch(code, @"^const\s+links\s*=\s*ref\s*<") ||
             Regex.IsMatch(code, @"^const\s+(?:auth|router|route|store)\s*=\s*use[A-Za-z_]+\(") ))
            return true;
        if (path.StartsWith("scripts/", StringComparison.OrdinalIgnoreCase) &&
            (Regex.IsMatch(code, @"^(?:const|let|var)\s+\w+\s*=\s*require\s*\(") ||
             Regex.IsMatch(code, @"^(?:function\s+prepare|function\s+alter)\s*\(")))
            return true;
        if (path.EndsWith(".sql", StringComparison.OrdinalIgnoreCase) &&
            Regex.IsMatch(code, @"^(?:SET\s+(?:XACT_ABORT|NOCOUNT)|USE\s+\[|IF\s+EXISTS|AND\s+name\s*=)", RegexOptions.IgnoreCase))
            return true;
        if (Regex.IsMatch(code, @"^(?:private|public|internal)?\s*(?:static\s+)?readonly\s+(?:TimeSpan|TimeSpan\?)\s+\w+\s*="))
            return true;
        if (path.EndsWith("/modules.ts", StringComparison.OrdinalIgnoreCase) &&
            Regex.IsMatch(code, "^[\\\"'](?:GET|POST|PUT|PATCH|DELETE)\\s+/", RegexOptions.IgnoreCase))
            return true;
        return false;
    }
}
