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
    private sealed record Gate(string Code, string? Path = null);

    private static List<Gate> Gates(string claim)
    {
        var gates = new List<Gate>();
        void Add(string code, string? path = null) => gates.Add(new(code, path));
        if (Matches(claim, @"\bdang nhap\b|\blogin\b|\bsign[ -]?in\b")) Add(Login);
        if (Matches(claim, @"\bjwt\b")) Add(@"addjwtbearer|jwtsecuritytoken|tokenvalidationparameters|jwt\.sign|jsonwebtoken|signingcredentials");
        if (Matches(claim, @"\bphan quyen\b|\bkiem tra quyen\b|\brole.based\b"))
        {
            var frontend = Matches(claim, @"\bgiao dien\b|\bfrontend\b");
            var backend = Matches(claim, @"\bapi\b|\bbackend\b");
            if (frontend) Add(Roles, @"^frontend/");
            if (backend || !frontend) Add(Roles, backend ? @"^backend/" : null);
            // A generic CanRead(project,user) call does not establish all three roles.
            if (Matches(claim, "quan tri vien") && Matches(claim, "giang vien") && Matches(claim, "sinh vien"))
                foreach (var role in new[] { "admin", "lecturer", "student" }) Add($"[\\\"']{role}[\\\"']");
        }
        if (Matches(claim, @"\bcau truc\b|\bkhoi tao du an\b|\bscaffold\b"))
        {
            if (Matches(claim, @"\bbackend\b|asp.net|minimal api"))
            {
                Add(@"microsoft\.net\.sdk\.web", @"\.csproj$");
                Add(@"webapplication\.createbuilder|webapplication\.create\(", @"program\.cs$");
                if (Matches(claim, "entity framework")) Add(@"adddbcontext|usesqlserver|usesqlite|dbcontextoptions", @"^backend/");
            }
            if (Matches(claim, @"\bfrontend\b|\bvue\b"))
            {
                Add("[\\\"']vue[\\\"']\\s*:\\s*[\\\"'][~^]?3", @"package\.json$");
                if (Matches(claim, "typescript")) Add("[\\\"']typescript[\\\"']\\s*:", @"package\.json$");
                if (Matches(claim, "vite")) Add("[\\\"']vite[\\\"']\\s*:|defineconfig\\(", @"package\.json$|vite\.config\.");
                Add(@"createapp\(", @"^frontend/.*\.(ts|js)$");
            }
        }
        if (Matches(claim, @"\bquan ly de tai\b")) Add(@"db\.topics\b|(?:mapget|mappost|mapput|mapdelete)\([^\n]*[/\""']topics\b|topics?api\.\w+\(");
        if (Matches(claim, @"\bquan ly de tai\b")) Add(@"db\.topics\.(?:add|addasync|remove|update)\(|mappost\([^\n]*/topics\b|mapput\([^\n]*/topics\b|topics?api\.(?:create|update|remove)\(");
        if (Matches(claim, @"\bdot dang ky\b")) Add(@"db\.registrationperiods\b|registrationperiods?api\.|/registration-periods\b");
        if (Matches(claim, @"\bde xuat cua sinh vien\b|\bde xuat de tai\b")) Add(@"db\.(studentproposals|topicproposals)\b|/[^\""']*proposals\b|proposalapi\.");
        if (Matches(claim, @"\byeu cau (huong dan|giang vien)\b"))
        {
            Add(@"db\.lecturerrequests\b|/lecturer-requests\b|lecturerrequests?api\.");
            if (Matches(claim, @"\btao\b")) Add(@"lecturerrequests\.(?:add|addasync)\(", @"lecturerrequest");
            if (Matches(claim, @"\bchap nhan\b")) Add(@"(?:status\s*=.*accepted|/accept\b)", @"lecturerrequest");
            if (Matches(claim, @"\btu choi\b")) Add(@"(?:status\s*=.*rejected|/reject\b)", @"lecturerrequest");
        }
        if (Matches(claim, @"\bquan ly do an\b")) Add(@"db\.projects\b|(?:mapget|mappost|mapput|mapdelete)\([^\n]*[/\""']projects\b", @"project(?!.*github)|projectprogress");
        if (Matches(claim, @"\bke hoach milestone\b")) Add(@"db\.milestones\b|/milestones\b|milestoneplan", @"milestone|projectprogress");
        if (Matches(claim, @"\bdau viec\b") && Matches(claim, @"\btrong so\b|\bdieu kien dat\b"))
            Add(@"milestonecriteria\b|acceptancecriteria\b|criterioninput", @"milestone|projectprogress");
        if (Matches(claim, @"\b(lien ket|gan)\b.*\b(repository|repo|github)\b"))
        {
            Add(@"mappost\([^\n]*/repositories\b", @"^backend/");
            Add(@"(?:db\.)?repositories\.(?:add|addasync)\(", @"^backend/");
            Add(@"githubapi\.link\(|\blink\s*:\s*\(", @"^frontend/");
        }
        if (Matches(claim, @"\bgo lien ket\b|\bxoa lien ket\b"))
        { Add(@"mapdelete\([^\n]*/repositories\b|repositories\.remove\(", @"^backend/"); Add(@"githubapi\.unlink\(", @"^frontend/"); }
        if (Matches(claim, @"\bcommit(s)?\b|\bnhanh\b|\bbranch(es)?\b"))
        {
            var frontend = Matches(claim, @"\bgiao dien\b|\bfrontend\b|\bhien thi\b");
            var backend = Matches(claim, @"\bbackend\b|\bapi\b");
            var patterns = new List<string>();
            if (Matches(claim, @"\bcommit(s)?\b")) patterns.Add(Commit);
            if (Matches(claim, @"\bnhanh\b|\bbranch(es)?\b")) patterns.Add(Branch);
            foreach (var pattern in patterns)
            {
                if (frontend) Add(pattern, @"^frontend/");
                if (backend || !frontend) Add(pattern, backend ? @"^backend/" : null);
            }
        }
        if (Matches(claim, @"\bkiem thu\b|\btest case\b")) Add(@"\[fact\]|\[test\]|\bit\(|\btest\(|\bassert\.|public\s+async\s+task\s+\w+", @"test|spec");
        return gates;
    }

    private static bool Supports(WordEvidence evidence, Gate gate)
    {
        if (Matches(Normalize(evidence.File), @"(^|[/._-])(tests?|specs?)([/._-]|$)|integrationtests|unittests") && gate.Path != "test|spec") return false;
        var code = Normalize(evidence.Evidence.TrimStart('+', ' ', '\t'));
        if (code.StartsWith("//") || code.StartsWith("/*") || code.StartsWith("<!--")) return false;
        return (gate.Path is null || Matches(Normalize(evidence.File), gate.Path)) && Matches(code, gate.Code);
    }

    public static IReadOnlyList<WordEvidence> SelectSupporting(string text, IReadOnlyList<WordEvidence> evidence)
    {
        var gates = Gates(Normalize(text));
        // Include a representative of every required action before other candidates.
        return gates.Select(gate => evidence.FirstOrDefault(e => Supports(e, gate))).OfType<WordEvidence>()
            .Concat(evidence.Where(e => gates.Count == 0 || gates.Any(g => Supports(e, g)))).DistinctBy(e => e.Id).Take(24).ToArray();
    }

    public static WordComparisonRow Validate(WordComparisonRow assessment)
    {
        if (assessment.Status == "missing") return assessment;
        var claim = Normalize(assessment.Claim);
        var gates = Gates(claim);
        // Unknown criteria need a lecturer decision until a semantic check supports them.
        // A model's unvalidated "full" must never automatically award progress.
        if (gates.Count == 0) return assessment with { Status = "partial" };
        var eligible = gates.Select(gate => assessment.Evidence.Where(e => Supports(e, gate)).ToArray()).ToArray();
        var citations = eligible.SelectMany(x => x).DistinctBy(e => e.Id).ToArray();
        if (citations.Length == 0) return assessment with { Status = "missing", Evidence = [] };
        var full = eligible.All(x => x.Length > 0);
        // Source test declarations cannot prove execution, universal test coverage or runtime success.
        if (Matches(claim, @"hoat dong dung|chay (dung|thanh cong)|kiem thu.*tat ca|tat ca.*kiem thu|dam bao hoat dong")) full = false;
        return assessment with { Status = full ? assessment.Status : "partial", Evidence = citations };
    }

    private static bool Matches(string text, string pattern) => Regex.IsMatch(text, pattern,
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static string Normalize(string text) => string.Concat(text.Normalize(NormalizationForm.FormD)
        .Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
        .Select(ch => char.ToLowerInvariant(ch) == 'đ' ? 'd' : char.ToLowerInvariant(ch)));
}
