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
    // Reject statements that *only* describe declarations/snippets. The old
    // unanchored regex also rejected useful sentences such as "Bổ sung route ...
    // được khai báo ..." simply because they contained "được khai báo".
    private static readonly Regex EmptyObservation = new(
        @"(?i)^\s*(?:(?:the (?:code )?snippet\b(?:\s+is\b|\s+shows?\s+(?:only|a summary)\b))|" +
        @"(?:the )?(?:class|record|interface)\s+.+?\s+is\s+(?:defined|declared)\b|" +
        @"(?:has a private|a sealed record|xml (?:comment|summary))\b|" +
        @"(?:được (?:định nghĩa|khai báo)|khai báo (?:lớp|class)|chỉ là (?:comment|khai báo)|" +
        @"phần (?:comment|chú thích)|tên (?:lớp|biến|class))\b|" +
        @"đoạn (?:mã|code) (?:này|trên)\s+(?:chỉ\s+|là\s+))",
        RegexOptions.Compiled);
    // A named HTTP route is a specific claim. It must be present VERBATIM in
    // the line cited by evidenceId; a real ID alone must not justify a fake API.
    private static readonly Regex RouteMention = new(@"/[A-Za-z0-9_{}:/.-]{2,}", RegexOptions.Compiled);
    public static bool HasUncitedRoute(string description, string citedLine) =>
        RouteMention.Matches(description).Cast<Match>().Any(route =>
            !citedLine.Contains(route.Value, StringComparison.OrdinalIgnoreCase));

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
            line.StartsWith("--", StringComparison.Ordinal) ||
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

    // These exact calls in integration-test setup only prove fixture/database
    // preparation, not a student-facing behavior. Never apply this filter to
    // production code; SaveChangesAsync there may be a real feature change.
    // Match only standalone awaited calls, so assertions/behavior on the same
    // line never disappear. The restriction to test file paths is essential.
    private static readonly Regex TestDbSetupCall = new(
        @"(?i)^\s*await\s+(?:[A-Za-z_]\w*\.)*(?:EnsureCreatedAsync|EnsureDeletedAsync|MigrateAsync|SaveChangesAsync)\s*\([^;]*\)\s*;?$",
        RegexOptions.Compiled);

    // An OK login assertion in an unrelated integration-test setup only checks
    // whether the test client was authenticated. Keep authorization/negative
    // behavior assertions (Unauthorized, Forbidden, Created) and production code.
    private static readonly Regex TestLoginSetupAssertion = new(
        @"(?i)^\s*Assert\.Equal\s*\(\s*HttpStatusCode\.OK\s*,\s*login\.StatusCode\s*\)\s*;?$",
        RegexOptions.Compiled);

    public static bool IsLowValueTestSetupLine(string path, string raw) =>
        AreaForPath(path) == "Kiểm thử" && raw.Length >= 2 &&
        (raw[0] == '+' || raw[0] == '-') &&
        (TestDbSetupCall.IsMatch(raw[1..].Trim()) ||
         TestLoginSetupAssertion.IsMatch(raw[1..].Trim()));

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
        // Only active source and test trees participate in code-change analysis.
        // Tooling/archived scripts still count in raw GitHub diff statistics.
        if (!(file.Path.StartsWith("backend/", StringComparison.OrdinalIgnoreCase) ||
              file.Path.StartsWith("frontend/src/", StringComparison.OrdinalIgnoreCase)) ||
            Generated(file.Path) || !CodeFile(file.Path) ||
            !GitProgressEvidencePolicy.SafePath(file.Path)) return [];
        return GitProgressEvidencePolicy.EvidenceLines(file)
            .Select((line, index) => new { line, index, score = LineScore(line) })
            // A removed line is not evidence of a newly implemented feature.
            // Git totals still count deletions; AI samples only additions.
            .Where(x => x.line.StartsWith('+') && IsFunctionalLine(x.line) &&
                !IsLowValueTestSetupLine(file.Path, x.line))
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
        int WeakDescription, int Duplicate, int Eligible,
        int InvalidLength = 0, int UnsupportedClaim = 0, int Boilerplate = 0,
        int MissingAction = 0, int TestSetupOnly = 0, int GroupedTestDetail = 0,
        int GroupedFeatureDetail = 0, int SelectionLimit = 0,
        int SemanticMismatch = 0, int SupportingDetail = 0);

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
                g => GitProgressEvidencePolicy.AllEvidenceLines(g.Single()).ToHashSet(StringComparer.Ordinal),
                StringComparer.Ordinal);
        var valid = new List<ProgressEvidence>();
        var unknownId = 0;
        var unsentFile = 0;
        var unsentLine = 0;
        var nonFunctionalLine = 0;
        var weakDescription = 0;
        var duplicate = 0;
        var eligible = 0;
        var invalidLength = 0;
        var unsupportedClaim = 0;
        var boilerplate = 0;
        var missingAction = 0;
        var testSetupOnly = 0;
        var semanticMismatch = 0;
        var supportingDetail = 0;
        foreach (var candidate in selections)
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
            if (!reference.Evidence.StartsWith('+') || !IsFunctionalLine(reference.Evidence))
            {
                // Including old removed code as newly added work is not a valid citation.
                nonFunctionalLine++;
                continue;
            }
            if (IsLowValueTestSetupLine(reference.File, reference.Evidence))
            {
                testSetupOnly++;
                continue;
            }
            if (GitProgressSemanticEvidencePolicy.IsSupportingOnlyLine(reference.File, reference.Evidence))
            {
                supportingDetail++;
                continue;
            }
            var issue = CheckEvidenceDescription(reference.File, reference.Evidence,
                candidate.Description, out var vietnameseDescription);
            if (issue != DescriptionIssue.None)
            {
                weakDescription++;
                switch (issue)
                {
                    case DescriptionIssue.InvalidLength: invalidLength++; break;
                    case DescriptionIssue.UnsupportedClaim: unsupportedClaim++; break;
                    case DescriptionIssue.Boilerplate: boilerplate++; break;
                    case DescriptionIssue.MissingAction: missingAction++; break;
                    case DescriptionIssue.SemanticMismatch: semanticMismatch++; break;
                }
                continue;
            }
            if (valid.Any(v => v.File == reference.File && (v.Evidence == reference.Evidence ||
                v.Description.Equals(vietnameseDescription, StringComparison.OrdinalIgnoreCase))))
            {
                duplicate++;
                continue;
            }
            valid.Add(new ProgressEvidence(reference.File, reference.Evidence, vietnameseDescription));
            eligible++;
        }
        return new RejectionDiagnostics(unknownId, unsentFile, unsentLine,
            nonFunctionalLine, weakDescription, duplicate, eligible,
            invalidLength, unsupportedClaim, boilerplate, missingAction, testSetupOnly,
            SemanticMismatch: semanticMismatch, SupportingDetail: supportingDetail);
    }

    // The old v3 keyword list rejected every observation from some valid Ollama
    // runs merely because descriptions used different verbs (e.g. configures,
    // exposes, thực hiện). Keep negative guards and an action requirement, but
    // cover common *behavioral* paraphrases in both English and Vietnamese.
    // A matching verb is NOT itself proof of behavior; evidence IDs still must
    // resolve to a real, non-comment line from the exact excerpt sent to AI.
    private static readonly Regex AdditionalAction = new(
        @"(?i)\b(?:implement(?:s|ed|ing)?|expos(?:e|es|ed|ing)|configur(?:e|es|ed|ing)|" +
        @"handl(?:e|es|ed|ing)|creat(?:e|es|ed|ing)|enabl(?:e|es|ed|ing)|" +
        @"persist(?:s|ed|ing)?|fetch(?:es|ed|ing)?|send(?:s|ing)?|" +
        @"receiv(?:e|es|ed|ing)|invok(?:e|es|ed|ing)|bind(?:s|ing)?|" +
        @"writ(?:e|es|ing)|read(?:s|ing)?|provid(?:e|es|ed|ing)|" +
        @"connect(?:s|ed|ing)?|track(?:s|ed|ing)?|sav(?:e|es|ed|ing)|" +
        @"test(?:s|ed|ing)?|verif(?:y|ies|ied|ying)|initializ(?:e|es|ed|ing)|" +
        @"secur(?:e|es|ed|ing)|authoriz(?:e|es|ed|ing)|" +
        @"trigger(?:s|ed|ing)?|calculat(?:e|es|ed|ing))\b",
        RegexOptions.Compiled);
    private static readonly string[] AdditionalVietnameseAction = [
        "triển khai", "thiết lập", "cung cấp", "thực hiện", "kết nối", "sử dụng",
        "khởi tạo", "gửi ", "nhận ", "điều phối", "ghi nhận", "tính toán",
        "theo dõi", "truy xuất", "phục vụ", "cho phép", "hỗ trợ",
        "ràng buộc", "đồng bộ", "kích hoạt", "đối chiếu"
    ];

    // Ollama sometimes produces a noun phrase with an exact, verifiable route,
    // handler or Vue binding but without a verb from our vocabulary. Only in
    // these narrow cases do we let a REAL added line supply the action. The
    // returned wording is deterministic and derived from that line, not AI prose.
    // Never apply this shortcut to test fixtures, arbitrary async calls or
    // technical assertions, which should belong to the test feature group.
    private static readonly Regex AnchoredRoute = new(
        @"\bMap(?<verb>Get|Post|Put|Delete|Patch)\s*\(\s*""(?<path>[^""]{1,100})""\s*,\s*(?<handler>[A-Za-z_]\w*)",
        RegexOptions.Compiled);
    private static readonly Regex AnchoredClick = new(
        "@click=\"(?<handler>[A-Za-z_][A-Za-z_0-9.]*)(?:\\([^\"]{0,100}\\))?\"",
        RegexOptions.Compiled);
    private static readonly Regex AnchoredModel = new(
        "v-model(?:\\.[a-z]+)*=\"(?<field>[A-Za-z_][A-Za-z_0-9.]*)\"",
        RegexOptions.Compiled);
    private static readonly Regex HttpVerbInDescription = new(
        @"(?i)\b(?:GET|POST|PUT|PATCH|DELETE)\b", RegexOptions.Compiled);
    private static readonly Regex ClaimingOtherRole = new(
        @"(?i)\b(?:admin|administrator|lecturer|teacher|giảng\s+viên|quản\s+trị\s+viên)\b",
        RegexOptions.Compiled);
    private static readonly Regex StatusInDescription = new(
        @"(?i)\b(?:Unauthorized|Forbidden|Created|NotFound|BadRequest|NoContent|HttpStatusCode\.OK)\b",
        RegexOptions.Compiled);

    private static bool TryDescribeMissingActionFromExactEvidence(
        string path, string evidence, string description, out string vietnamese)
    {
        vietnamese = "";
        if (CheckDescription(description) != DescriptionIssue.MissingAction ||
            !evidence.StartsWith('+') ||
            evidence.StartsWith("+++", StringComparison.Ordinal) ||
            !IsFunctionalLine(evidence) || IsLowValueTestSetupLine(path, evidence) ||
            AreaForPath(path) == "Kiểm thử" ||
            HasUncitedRoute(description, evidence) ||
            StatusInDescription.IsMatch(description) ||
            ClaimingOtherRole.IsMatch(description)) return false;

        var source = evidence[1..];
        var route = AnchoredRoute.Match(source);
        var click = AnchoredClick.Match(source);
        var model = AnchoredModel.Match(source);
        bool anchored = false;
        if (route.Success && path.Contains("/Endpoints/", StringComparison.OrdinalIgnoreCase))
        {
            var verb = route.Groups["verb"].Value.ToUpperInvariant();
            // The exact route MUST be written by the model. Merely repeating
            // 'repository' or 'handler' is not an evidence-grounded action.
            anchored = description.Contains(route.Groups["path"].Value, StringComparison.OrdinalIgnoreCase) &&
                (!HttpVerbInDescription.IsMatch(description) ||
                 HttpVerbInDescription.Matches(description).Cast<Match>()
                    .All(m => m.Value.Equals(verb, StringComparison.OrdinalIgnoreCase)));
        }
        else if (path.StartsWith("frontend/src/", StringComparison.OrdinalIgnoreCase))
        {
            if (click.Success)
                anchored = description.Contains(click.Groups["handler"].Value, StringComparison.OrdinalIgnoreCase);
            else if (model.Success)
                anchored = description.Contains(model.Groups["field"].Value, StringComparison.OrdinalIgnoreCase);
        }
        return anchored && GitProgressGroundedFallback.TryDescribeAddedLine(path, evidence, out vietnamese) &&
            UsefulDescription(vietnamese);
    }

    // Shared by the actual screen and diagnostics; otherwise counts would
    // disagree about whether a model-selected line qualified.
    private static DescriptionIssue CheckEvidenceDescription(
        string path, string evidence, string description, out string vietnamese)
    {
        vietnamese = "";
        if (HasUncitedRoute(description, evidence))
            return DescriptionIssue.UnsupportedClaim;

        // Classify basic description quality before comparing named symbols with
        // the cited source line. Otherwise generic prose such as "The class
        // Analyzer is defined" is incorrectly counted as SemanticMismatch just
        // because the word Analyzer does not occur in an unrelated route line.
        // Keep semantic verification for descriptions that are otherwise
        // meaningful; real claims such as "Bổ sung phương thức Actor..." must
        // still be rejected when Actor is absent from the cited code.
        var issue = CheckDescription(description);
        if (issue is DescriptionIssue.InvalidLength or DescriptionIssue.UnsupportedClaim or
            DescriptionIssue.Boilerplate)
            return issue;

        if (GitProgressSemanticEvidencePolicy.HasUnsupportedClaim(path, evidence, description))
            return DescriptionIssue.SemanticMismatch;
        if (issue == DescriptionIssue.None)
        {
            if (TryNormalizeToVietnamese(path, evidence, description, out vietnamese))
                return DescriptionIssue.None;
            return DescriptionIssue.Boilerplate;
        }
        if (issue == DescriptionIssue.MissingAction &&
            TryDescribeMissingActionFromExactEvidence(path, evidence, description, out vietnamese))
            return DescriptionIssue.None;
        return issue;
    }

    // Diagnostic taxonomy only. The screening policy below stays identical to v3.2:
    // no widening of allowed evidence, wording, or unsupported claims.
    public enum DescriptionIssue { None, InvalidLength, UnsupportedClaim, Boilerplate, MissingAction, SemanticMismatch }

    public static DescriptionIssue CheckDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description) || description.Length < 28 || description.Length > 350)
            return DescriptionIssue.InvalidLength;
        var normalized = Whitespace.Replace(description.Trim(), " ");
        if (UnsupportedClaim.IsMatch(normalized)) return DescriptionIssue.UnsupportedClaim;
        if (EmptyObservation.IsMatch(normalized)) return DescriptionIssue.Boilerplate;
        if (!ActionWords.Any(x => normalized.Contains(x, StringComparison.OrdinalIgnoreCase)) &&
            !AdditionalVietnameseAction.Any(x => normalized.Contains(x, StringComparison.OrdinalIgnoreCase)) &&
            !AdditionalAction.IsMatch(normalized))
            return DescriptionIssue.MissingAction;
        return DescriptionIssue.None;
    }

    public static bool UsefulDescription(string description) =>
        CheckDescription(description) == DescriptionIssue.None;

    // Do not show an English Ollama narrative verbatim in a Vietnamese report.
    // If it passed the semantic checks, normalize only by re-describing the exact
    // cited ADDED line using deterministic rules. Never freely translate claims
    // that might include information absent from that evidence.
    private static readonly Regex VietnameseProgressStart = new(
        @"(?i)^\s*(?:Bổ\s+sung|Thêm|Cập\s+nhật|Kiểm\s+tra|Kiểm\s+thử|" +
        @"Xác\s+minh|Hiển\s+thị|Gọi|Đăng\s+ký|Tích\s+hợp|Liên\s+kết|" +
        @"Giới\s+hạn|Đọc|Ghi\s+nhận|Tạo|Thiết\s+lập|Thực\s+hiện|" +
        @"Truy\s+vấn|Xử\s+lý|Chọn|Đối\s+chiếu|Thay\s+đổi)\b",
        RegexOptions.Compiled);

    public static bool TryNormalizeToVietnamese(string path, string line,
        string description, out string vietnamese)
    {
        vietnamese = description.Trim();
        if (VietnameseProgressStart.IsMatch(vietnamese)) return true;
        if (line.StartsWith('+') &&
            GitProgressGroundedFallback.TryDescribeAddedLine(path, line, out var ruleDescription) &&
            UsefulDescription(ruleDescription))
        {
            vietnamese = ruleDescription;
            return true;
        }
        return false;
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
                g => GitProgressEvidencePolicy.AllEvidenceLines(g.Single()).ToHashSet(StringComparer.Ordinal),
                StringComparer.Ordinal);
        var valid = new List<ProgressEvidence>();
        foreach (var candidate in selections.Take(18))
        {
            if (!index.TryGetValue(candidate.EvidenceId, out var reference) ||
                !allowed.TryGetValue(reference.File, out var changed) ||
                !changed.Contains(reference.Evidence) ||
                !reference.Evidence.StartsWith('+') ||
                !IsFunctionalLine(reference.Evidence) ||
                IsLowValueTestSetupLine(reference.File, reference.Evidence) ||
                GitProgressSemanticEvidencePolicy.IsSupportingOnlyLine(reference.File, reference.Evidence) ||
                CheckEvidenceDescription(reference.File, reference.Evidence,
                    candidate.Description, out _) != DescriptionIssue.None) continue;
            CheckEvidenceDescription(reference.File, reference.Evidence,
                candidate.Description, out var vietnameseDescription);
            if (valid.Any(v => v.File == reference.File && (v.Evidence == reference.Evidence ||
                v.Description.Equals(vietnameseDescription, StringComparison.OrdinalIgnoreCase)))) continue;
            valid.Add(new ProgressEvidence(reference.File, reference.Evidence, vietnameseDescription));
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
