using System.Text.RegularExpressions;

namespace StudentProjects.Api.Services;

/// <summary>
/// Recognizes deliberately public credential entry points using the actual
/// route-to-handler binding, not a bare AllowAnonymous token. This is a
/// conservative false-positive rule; it does not certify the endpoint secure.
/// </summary>
public static class AiPublicAuthRoutePolicy
{
    private static readonly Regex PublicAuthPost = new(
        "\\bMapPost\\s*\\(\\s*\"(?<route>/(?:login|register))\"\\s*,\\s*(?<handler>[A-Za-z_]\\w*)\\s*\\)\\s*\\.AllowAnonymous\\s*\\(\\s*\\)",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);

    private static readonly string[] AbsenceOfAuthentication =
    [
        "không yêu cầu xác thực", "chưa yêu cầu xác thực", "thiếu xác thực",
        "yêu cầu xác thực", "không cần xác thực", "không cần đăng nhập",
        "thiếu phân quyền", "không có phân quyền", "require authentication",
        "requires authentication", "missing authentication", "unauthenticated",
        "authentication required", "allowanonymous", "requireauthorization"
    ];

    private static readonly string[] ConcreteBehaviorClaims =
    [
        "bỏ qua kiểm tra mật khẩu", "không kiểm tra mật khẩu", "không kiểm tra credentials",
        "token được cấp không cần", "cấp token trước", "token leak", "token leakage",
        "password bypass", "bypass password", "sql injection", "xss", "csrf"
    ];

    public static bool IsUnsupportedLoginGateClaim(SourceFile source, AiFinding finding)
    {
        if (!source.Path.EndsWith("/AuthEndpoints.cs", StringComparison.OrdinalIgnoreCase) &&
            !source.Path.EndsWith("\\AuthEndpoints.cs", StringComparison.OrdinalIgnoreCase))
            return false;
        if (finding.Line < 1 || finding.Line > source.Lines.Length ||
            !source.Lines[finding.Line - 1].Contains("AllowAnonymous", StringComparison.OrdinalIgnoreCase))
            return false;
        var allegation = $"{finding.Explanation} {finding.Recommendation}";
        if (!AbsenceOfAuthentication.Any(x => allegation.Contains(x, StringComparison.OrdinalIgnoreCase)) ||
            ConcreteBehaviorClaims.Any(x => allegation.Contains(x, StringComparison.OrdinalIgnoreCase)))
            return false;

        var entireFile = string.Join("\n", source.Lines);
        foreach (Match match in PublicAuthPost.Matches(entireFile))
        {
            // Enforce that the evidence points to the exact route declaration,
            // not a different AllowAnonymous endpoint in the same file.
            var start = 1 + entireFile[..match.Index].Count(c => c == '\n');
            var end = start + match.Value.Count(c => c == '\n');
            if (finding.Line < start || finding.Line > end) continue;
            if (match.Groups["route"].Value.Equals("/register", StringComparison.OrdinalIgnoreCase))
                return true; // A public registration route alone is not a vulnerability.

            // For /login, look for the bound handler's credential-validation flow.
            // Do not suppress the allegation if the handler is absent/truncated.
            var handlerName = match.Groups["handler"].Value;
            var method = AiSourceContextBuilder.FindCSharpMethods(source)
                .FirstOrDefault(m => string.Equals(m.Name, handlerName, StringComparison.Ordinal));
            if (method is null) return false;
            var handlerText = string.Join("\n", source.Lines.Skip(method.StartLine - 1)
                .Take(method.EndLine - method.StartLine + 1));
            if (handlerText.Contains("VerifyHashedPassword", StringComparison.Ordinal) &&
                handlerText.Contains("PasswordVerificationResult.Failed", StringComparison.Ordinal) &&
                handlerText.Contains("INVALID_CREDENTIALS", StringComparison.Ordinal) &&
                handlerText.Contains("Results.Ok", StringComparison.Ordinal))
                return true;
        }
        return false;
    }
}
