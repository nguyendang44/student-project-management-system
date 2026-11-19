using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace StudentProjects.Api.Services;

internal static class GitProgressMilestoneEvidence
{
    private const string Login = @"login|sign[_-]?in|authenticate|passwordsignin|dangnhap|dang nhap";
    private const string Roles = @"requirerole|requireclaim|isinrole|authorize\s*\([^)]*roles|hasrole|forrole|role\s*[=!]=|\.roles?\.(?:includes|contains)";
    private const string Commit = @"/commits\b|\bcommits?\s*[:(]|listcommits|fetchcommits|getcommits";
    private const string Branch = @"/branches\b|\bbranches\s*[:(]|listbranches|fetchbranches|getbranches";

    public static WordComparisonRow Validate(WordComparisonRow assessment)
    {
        if (assessment.Status == "missing") return assessment;

        var claim = Normalize(assessment.Claim);
        var gates = new List<(string Pattern, bool? Frontend)>();
        if (Matches(claim, @"\bdang nhap\b|\blogin\b|\bsign[ -]?in\b"))
            gates.Add((Login, null));
        if (Matches(claim, @"\bphan quyen\b|\bkiem tra quyen\b|\brole.based\b"))
        {
            var frontend = Matches(claim, @"\bgiao dien\b|\bfrontend\b");
            var backend = Matches(claim, @"\bapi\b|\bbackend\b");
            if (frontend) gates.Add((Roles, true));
            if (backend || !frontend) gates.Add((Roles, backend ? false : null));
        }
        if (Matches(claim, @"\bcommit(s)?\b") && Matches(claim, @"\bnhanh\b|\bbranch(es)?\b"))
        {
            var frontend = Matches(claim, @"\bgiao dien\b|\bfrontend\b|\bhien thi\b");
            var backend = Matches(claim, @"\bbackend\b|\bapi\b");
            foreach (var pattern in new[] { Commit, Branch })
            {
                if (frontend) gates.Add((pattern, true));
                if (backend || !frontend) gates.Add((pattern, backend ? false : null));
            }
        }

        if (gates.Count == 0) return assessment;
        var eligible = gates.Select(gate => assessment.Evidence.Where(citation =>
            Matches(Normalize(citation.Evidence), gate.Pattern) &&
            (gate.Frontend is null || citation.File.StartsWith(
                gate.Frontend.Value ? "frontend/" : "backend/", StringComparison.OrdinalIgnoreCase))).ToArray()).ToArray();
        var citations = eligible.SelectMany(x => x).Distinct().ToArray();
        if (citations.Length == 0) return assessment with { Status = "missing", Evidence = [] };
        return assessment with
        {
            Status = eligible.Any(x => x.Length == 0) ? "partial" : assessment.Status,
            Evidence = citations
        };
    }

    private static bool Matches(string text, string pattern) => Regex.IsMatch(text, pattern,
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    private static string Normalize(string text) => string.Concat(text.Normalize(NormalizationForm.FormD)
        .Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
        .Select(ch => char.ToLowerInvariant(ch) == 'đ' ? 'd' : char.ToLowerInvariant(ch)));
}
