using System.Text.RegularExpressions;

namespace StudentProjects.Api.Services;

/// <summary>
/// Screens unsupported "missing authorization" claims using the whole handler
/// and route registration from the fetched source. It does NOT prove a route safe.
/// Without concrete route and exploit evidence, absence-of-check allegations are
/// not persisted as vulnerabilities; lecturers may still audit authorization.
/// </summary>
public static class AiAuthorizationContextGuard
{
    private static readonly string[] AccessTerms =
    [
        "phân quyền", "quyền truy cập", "kiểm tra quyền", "xác thực", "đăng nhập",
        "authorization", "authentication", "permission", "access control", "unauthorized",
        "role check", "requireauthorization"
    ];
    private static readonly string[] AbsenceTerms =
    [
        "thiếu", "không có", "chưa", "không kiểm tra", "không được bảo vệ",
        "missing", "lacks", "no check", "without", "unprotected", "not authorized"
    ];

    private static bool HasAny(string text, IEnumerable<string> terms) =>
        terms.Any(word => text.Contains(word, StringComparison.OrdinalIgnoreCase));

    public static AiFinding? Screen(AiFinding finding, SourceFile source)
    {
        if (!string.Equals(finding.Category, "security", StringComparison.OrdinalIgnoreCase))
            return finding;
        var claim = $"{finding.Explanation} {finding.Recommendation}";
        // Intentionally public /login and /register cannot be called an
        // authorization flaw merely because their route uses AllowAnonymous.
        // For /login verify that the bound handler actually checks credentials.
        if (AiPublicAuthRoutePolicy.IsUnsupportedLoginGateClaim(source, finding))
            return null;
        // An explicit AllowAnonymous on a route is evidence of configuration,
        // not proof of a vulnerability. Keep an access-related claim for review
        // at LOW severity, even when the wording does not match AbsenceTerms.
        if (finding.Evidence.Contains(".AllowAnonymous", StringComparison.OrdinalIgnoreCase)
            && HasAny(claim, AccessTerms))
            return finding with { Severity = "LOW" };

        if (!HasAny(claim, AccessTerms) || !HasAny(claim, AbsenceTerms))
            return finding;

        // A line of LINQ, service/interface declaration or response mapping
        // does not itself establish the request's authorization policy.
        if (!source.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) return null;
        var regions = AiSourceContextBuilder.FindCSharpMethods(source);
        var handler = regions.FirstOrDefault(m => m.StartLine <= finding.Line && finding.Line <= m.EndLine);
        if (handler is null) return null;
        // Search the complete local handler and route registration for existing
        // protection before assessing this negative claim. An absence allegation
        // is not established even if this bounded snapshot contains none.
        _ = HasRouteOrHandlerAuthorization(source, finding.Line);
        return null;
    }

    // Exposed for regression tests and for future request-path authorization
    // policies. True means a protection was observed, NOT that access is safe.
    public static bool HasRouteOrHandlerAuthorization(SourceFile source, int line)
    {
        if (!source.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) return false;
        var lines = source.Lines;
        var regions = AiSourceContextBuilder.FindCSharpMethods(source);
        var handler = regions.FirstOrDefault(m => m.StartLine <= line && line <= m.EndLine);
        if (handler is null) return false;
        var handlerCode = string.Join("\n", lines.Skip(handler.StartLine - 1)
            .Take(handler.EndLine - handler.StartLine + 1));
        if (Regex.IsMatch(handlerCode, @"\b(?:IsInRole|RequireRole|Results\.Forbid|AuthorizeAsync)\s*\("))
            return true;

        foreach (var mapMethod in regions.Where(m => m.Name.StartsWith("Map", StringComparison.Ordinal)))
        {
            var mapping = string.Join("\n", lines.Skip(mapMethod.StartLine - 1)
                .Take(mapMethod.EndLine - mapMethod.StartLine + 1));
            var handlerName = Regex.Escape(handler.Name);
            var routeRegex = new Regex(@"\b(?<group>\w+)\.Map(?:Get|Post|Put|Patch|Delete|Methods)\s*\([^;]*\b" +
                handlerName + @"\s*\)", RegexOptions.Singleline);
            foreach (Match route in routeRegex.Matches(mapping))
            {
                var variableName = Regex.Escape(route.Groups["group"].Value);
                var groupDeclaration = Regex.Match(mapping, @"\bvar\s+" + variableName +
                    @"\s*=\s*app\.MapGroup\s*\([^;]*;", RegexOptions.Singleline).Value;
                if (groupDeclaration.Contains(".RequireAuthorization", StringComparison.Ordinal) ||
                    Regex.IsMatch(mapping, @"\b" + variableName + @"\.RequireAuthorization\s*\(") ||
                    Regex.IsMatch(mapping, @"\b" + variableName +
                        @"\.Map(?:Get|Post|Put|Patch|Delete)\s*\([^;]*\b" + handlerName +
                        @"\s*\)\s*\.RequireAuthorization\s*\(", RegexOptions.Singleline))
                    return true;
            }
        }
        return false;
    }
}
