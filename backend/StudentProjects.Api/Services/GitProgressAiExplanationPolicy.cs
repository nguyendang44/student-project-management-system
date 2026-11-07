using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace StudentProjects.Api.Services;

/// <summary>A model's phrasing is never allowed to create or amend a Git citation.</summary>
public sealed record ProgressFeatureExplanation(string FactId, string Explanation);

public static class GitProgressAiExplanationPolicy
{
    private static readonly Regex Tokens = new(@"[\p{L}\p{Nd}_]{4,}", RegexOptions.Compiled);
    // A useful explanation states a grounded purpose, not just a new phrasing of
    // a declaration. This deliberately prefers silence over ungrounded claims.
    private static readonly Regex Purpose = new(
        @"(?i)(?:\bđể\b|\bnhằm\b|\bgiúp\b|\bhỗ\s+trợ\b|\bphục\s+vụ\b|\bcho\s+phép\b|\btạo\s+điểm\b)",
        RegexOptions.Compiled);
    private static readonly Regex GenericPurpose = new(
        @"(?i)(?:nâng cao|cải thiện|tối ưu).{0,30}(?:hệ thống|trải nghiệm|hiệu quả|chất lượng)",
        RegexOptions.Compiled);
    // A statement such as "Để tạo các endpoint ..." only restates the edit.
    // If it starts with a construction verb, require an actual user/processing flow.
    private static readonly Regex ConstructionOnly = new(
        @"(?i)^\s*(?:để|nhằm)\s+(?:tạo|bổ\s+sung|thêm|khai\s+báo|thiết\s+lập|xây\s+dựng)\b",
        RegexOptions.Compiled);
    private static readonly Regex Actor = new(
        @"(?i)(?:frontend|giao\s+diện|người\s+dùng|sinh\s+viên|giảng\s+viên|client|worker|backend)",
        RegexOptions.Compiled);
    private static readonly Regex RealInteraction = new(
        @"(?i)(?:gửi\s+yêu\s+cầu|khởi\s+tạo\s+(?:lần|yêu\s+cầu)|xem\s+lịch\s+sử|tra\s+cứu|truy\s+vấn|so\s+sánh|đối\s+chiếu|hiển\s+thị|lựa\s+chọn|theo\s+dõi|lưu\s+kết\s+quả)",
        RegexOptions.Compiled);
    private static readonly Regex RequestFlow = new(
        @"(?i)(?:nhận|tiếp\s+nhận|gửi|khởi\s+tạo|xử\s+lý)\s+yêu\s+cầu\s+phân\s+tích",
        RegexOptions.Compiled);
    private static readonly Regex AnalysisRun = new(
        @"(?i)(?:lần|lượt|phiên|đợt)\s+(?:chạy\s+)?phân\s+tích|lần\s+chạy|yêu\s+cầu\s+phân\s+tích",
        RegexOptions.Compiled);
    private static readonly Regex Findings = new(@"(?i)phát\s+hiện|vấn\s+đề\s+(?:trong\s+)?mã", RegexOptions.Compiled);
    private static readonly Regex Reviews = new(@"(?i)nhận\s+xét|đánh\s+giá", RegexOptions.Compiled);
    private static readonly Regex DuplicateWord = new(
        @"(?i)\b(?<word>[\p{L}]{3,})\s+\k<word>\b", RegexOptions.Compiled);
    private static readonly Regex PurposeAction = new(
        @"(?i)(?:tiếp nhận|nhận yêu cầu|gửi yêu cầu|tra cứu|truy vấn|xem lịch sử|so sánh|đối chiếu|lưu|hiển thị|chọn|ghi nhận|xử lý|gọi|phân tích|kiểm tra|kết nối|điều hướng|truy cập)",
        RegexOptions.Compiled);
    private static readonly Regex Word = new(@"[a-z0-9]{3,}", RegexOptions.Compiled);
    private static readonly HashSet<string> Common = new(StringComparer.Ordinal)
    {
        "cho", "cac", "cua", "duoc", "voi", "mot", "nhung", "trong", "tren", "theo",
        "nay", "khi", "moi", "sau", "can", "den", "voi", "viec", "bo", "sung",
        "code", "file", "thay", "doi", "khai", "bao", "them", "tiep", "can",
        "tung", "nguoi", "dung", "bang", "chung", "thong", "qua", "tinh", "nang",
        "nham", "giup", "ho", "tro", "phuc", "vu", "cho", "phep", "tao", "diem"
    };
    private static readonly Regex Route = new(@"/[A-Za-z0-9_{}:/.-]{2,}", RegexOptions.Compiled);
    // Reject particularly consequential named technologies/capabilities unless
    // the verified feature or its Git citations actually mentions them.
    private static readonly Regex ExternalClaims = new(
        @"(?i)\b(?:Redis|Kafka|RabbitMQ|Docker|Kubernetes|MongoDB|PostgreSQL|MySQL|GraphQL|JWT|OAuth|Stripe|SMTP|WebSocket|AWS|Azure)\b|chấm\s+điểm|gửi\s+(?:email|thông\s+báo)",
        RegexOptions.Compiled);
    private static readonly Regex UnsupportedClaims = new(
        @"(?i)(đã\s+(?:chạy\s+thành\s+công|hoàn\s+thành|kiểm\s+thử\s+thành\s+công|triển\s+khai\s+thành\s+công)|(?:100\s*%|không\s+có\s+lỗi|an\s+toàn\s+tuyệt\s+đối|pass(?:ed)?\s+(?:all\s+)?tests?|chấm\s+điểm\s+tự\s+động|tự\s+động\s+chấm\s+điểm))",
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

    private static bool HasConcreteBusinessOutcome(string explanation, ProgressFeatureFact fact)
    {
        // An explanation must add a real user action or distinguishable data flow,
        // not simply say "create API" / "create database structure".
        var actorFlow = Actor.IsMatch(explanation) && RealInteraction.IsMatch(explanation);
        var requestFlow = RequestFlow.IsMatch(explanation);
        if (ConstructionOnly.IsMatch(explanation) && !actorFlow && !requestFlow) return false;

        if (string.Equals(fact.Area, "API", StringComparison.OrdinalIgnoreCase))
            return actorFlow || requestFlow;

        if (string.Equals(fact.Area, "CSDL", StringComparison.OrdinalIgnoreCase))
        {
            // Where the diff proves three separately named data sets, require
            // an explanation of at least two of their distinct responsibilities.
            // A generic "store reports" claim contributes nothing new.
            var grounded = fact.Description + " " + string.Join(" ", fact.Citations.Select(c => c.Evidence));
            if (grounded.Contains("CodeAnalysisRuns", StringComparison.OrdinalIgnoreCase) &&
                grounded.Contains("CodeAnalysisFindings", StringComparison.OrdinalIgnoreCase) &&
                grounded.Contains("CodeAnalysisReviews", StringComparison.OrdinalIgnoreCase))
            {
                var roles = (AnalysisRun.IsMatch(explanation) ? 1 : 0) +
                    (Findings.IsMatch(explanation) ? 1 : 0) +
                    (Reviews.IsMatch(explanation) ? 1 : 0);
                return roles >= 2;
            }
            return actorFlow;
        }
        return true;
    }

    public static bool HasAdditionalPurpose(string explanation, ProgressFeatureFact fact)
    {
        if (!Purpose.IsMatch(explanation) || !PurposeAction.IsMatch(explanation) ||
            GenericPurpose.IsMatch(explanation) || DuplicateWord.IsMatch(explanation) ||
            !HasConcreteBusinessOutcome(explanation, fact)) return false;
        var known = Word.Matches(Fold(fact.Title + " " + fact.Description))
            .Cast<Match>().Select(m => m.Value).Where(w => !Common.Contains(w))
            .ToHashSet(StringComparer.Ordinal);
        var additional = Word.Matches(Fold(explanation))
            .Cast<Match>().Select(m => m.Value).Where(w => !Common.Contains(w) && !known.Contains(w))
            .Distinct(StringComparer.Ordinal).Count();
        return additional >= 3;
    }

    private static string Fold(string text)
    {
        var input = text.Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(input.Length);
        foreach (var ch in input)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            result.Append(ch is 'đ' or 'Đ' ? 'd' : char.ToLowerInvariant(ch));
        }
        return result.ToString();
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
            if (ExternalClaims.Matches(explanation).Cast<Match>().Any(m =>
                !supported.Contains(m.Value, StringComparison.OrdinalIgnoreCase))) continue;
            // A polished restatement is not an explanation of business/technical
            // purpose. Require a concrete, relevant outcome and NEW content beyond
            // the deterministic feature description. New route/API names are still
            // forbidden by the citation guard above.
            if (!HasAdditionalPurpose(explanation, fact)) continue;
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
