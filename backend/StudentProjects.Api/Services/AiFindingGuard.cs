namespace StudentProjects.Api.Services;

/// <summary>
/// Conservative, deterministic screening for unsupported AI security claims.
/// This is NOT proof that the underlying code is secure. All surviving findings
/// still require a lecturer's review.
/// </summary>
public static class AiFindingGuard
{
    private static bool Mentions(string source, params string[] keywords) =>
        keywords.Any(word => source.Contains(word, StringComparison.OrdinalIgnoreCase));

    public static AiFinding? Screen(AiFinding finding)
    {
        if (!string.Equals(finding.Category, "security", StringComparison.Ordinal))
            return finding;

        var file = (finding.File ?? "").Replace('\\', '/');
        var evidence = finding.Evidence ?? "";
        var claim = $"{finding.Explanation} {finding.Recommendation}";

        // A single guard for missing/inactive users cannot prove account
        // enumeration, a missing lock state, or the need to return 403.
        if (file.EndsWith("/AuthEndpoints.cs", StringComparison.OrdinalIgnoreCase)
            && evidence.Contains("user is null", StringComparison.OrdinalIgnoreCase)
            && evidence.Contains("IsActive", StringComparison.OrdinalIgnoreCase)
            && Mentions(claim, "401", "403", "bị khóa", "bị cấm", "khóa tài khoản",
                "lockout", "locked", "disabled", "tiết lộ", "leak", "enumeration"))
            return null;

        // A frontend login POST by itself is NOT evidence that the backend
        // uses ambient cookie authentication or is vulnerable to login CSRF.
        if (file.EndsWith("/auth.api.ts", StringComparison.OrdinalIgnoreCase)
            && Mentions(evidence, "/auth/login", "LoginRequest")
            && Mentions(claim, "csrf", "cross-site request forgery", "cross site request forgery"))
            return null;

        // Interface declarations describe a contract, not the call site or
        // implementation. Missing user checks cannot be inferred here.
        if (file.EndsWith("/Abstractions/Contracts.cs", StringComparison.OrdinalIgnoreCase)
            && evidence.Contains("ScanDueMilestonesAsync(", StringComparison.Ordinal)
            && Mentions(claim, "xác thực", "đăng nhập", "quyền", "authorization",
                "authentication", "unauthorized", "permission", "access control"))
            return null;

        // A typed frontend login request cannot by itself prove SQL injection,
        // XSS or missing server-side validation. Review the backend sink and
        // rendering context before making any vulnerability claim.
        if (file.EndsWith("/auth.api.ts", StringComparison.OrdinalIgnoreCase)
            && Mentions(evidence, "/auth/login", "LoginRequest")
            && Mentions(claim, "sql injection", "sql-injection", "xss", "cross-site scripting",
                "cross site scripting", "kiểm tra đầu vào", "validate input", "input validation"))
            return null;

        // Negative claims need cross-file / full-flow evidence. With only
        // excerpts, cap their urgency rather than pretending they are proven.
        var isAbsenceClaim = Mentions(finding.Explanation ?? "", "thiếu ", "không có ",
            "không kiểm tra", "chưa kiểm tra", "missing ", "lacks ", "no check ",
            "not checked", "without checking");
        if (finding.Severity == "HIGH" && isAbsenceClaim)
            return finding with { Severity = "MEDIUM" };

        return finding;
    }

    public static string CautiousNarrative(string? text, IReadOnlyList<AiFinding> retained)
    {
        var value = text ?? "";
        // Avoid echoing rejected, ungrounded security allegations in summaries.
        if (Mentions(value, "csrf", "cross-site request forgery") &&
            !retained.Any(f => f.Category == "security" &&
                Mentions($"{f.Explanation} {f.Recommendation}", "csrf", "cross-site request forgery")))
            return "Chưa đủ bằng chứng trong các đoạn mã đã đọc để kết luận về CSRF.";

        if (Mentions(value, "401", "403") &&
            !retained.Any(f => f.Category == "security" &&
                Mentions($"{f.Explanation} {f.Recommendation}", "401", "403")))
            return "Chưa đủ bằng chứng để kết luận phản hồi HTTP của chức năng đăng nhập là lỗ hổng.";

        return value;
    }
}
