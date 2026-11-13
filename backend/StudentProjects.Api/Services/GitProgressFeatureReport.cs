using System.Text.RegularExpressions;

namespace StudentProjects.Api.Services;

public sealed record ProgressFeatureReportStepCandidate(string Detail, IReadOnlyList<string> EvidenceIds);
public sealed record ProgressFeatureReportCandidate(string Name, string Summary,
    IReadOnlyList<ProgressFeatureReportStepCandidate> Steps);
public sealed record ProgressFeatureReportStep(string Detail, IReadOnlyList<ProgressFeatureCitation> Citations);
public sealed record ProgressAiFeature(string Name, string Summary,
    IReadOnlyList<ProgressFeatureReportStep> Steps,
    IReadOnlyList<ProgressFeatureCitation> Citations,
    IReadOnlyList<string> Components, IReadOnlyList<string> Technologies,
    string VerificationStatus, string EvidenceCoverage);
public sealed record ProgressAiFeatureReportResult(IReadOnlyList<ProgressAiFeature> Items,
    int RawItems, int ParsedItems, int RejectedEvidence, string Status);
public sealed record ProgressFeatureEvidenceSet(IReadOnlyList<ProgressEvidenceReference> Items,
    int Total, int Omitted);

/// <summary>
/// Feature-oriented report builder. Ollama may group verified source lines into
/// business features, but every displayed step must resolve to exact added Git lines.
/// Completion and test-pass claims are never inferred from a diff.
/// </summary>
public static class GitProgressFeatureReport
{
    private static readonly Regex CompletionClaim = new(
        @"(?i)(?:đã\s+(?:hoàn\s+thành|hoàn\s+thiện|hoàn\s+tất|triển\s+khai(?:\s+thành\s+công)?|chạy\s+thành\s+công|kiểm\s+thử\s+thành\s+công|pass\s+(?:test|kiểm\s+thử))|hoàn\s+thành\s+(?:chức\s+năng|toàn\s+bộ)|hoàn\s+thành\s+100\s*%|\btests?\s+(?:đã\s+)?(?:all\s+)?pass(?:ed)?\b|\b100\s*%\s*(?:hoàn\s+thành|complete))",
        RegexOptions.Compiled);
    private static readonly Regex BacktickSymbol = new("`(?<name>[A-Za-z_][A-Za-z0-9_]{1,90})`", RegexOptions.Compiled);
    private static readonly Regex Route = new(@"/(?:api|v[0-9]+)/[A-Za-z0-9_{}:/.-]{2,}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly (Regex Claim, Regex Evidence)[] Anchors =
    [
        (new Regex(@"(?i)\bJWT\b", RegexOptions.Compiled), new Regex(@"(?i)\bJWT\b|Jwt[A-Za-z0-9_]*|Bearer", RegexOptions.Compiled)),
        (new Regex(@"(?i)\btoken\b", RegexOptions.Compiled), new Regex(@"(?i)token|Bearer|Jwt[A-Za-z0-9_]*", RegexOptions.Compiled)),
        (new Regex(@"(?i)xác thực|authenticate|verify (?:the )?(?:user|credential|password)", RegexOptions.Compiled), new Regex(@"(?i)Authenticate|Verify|PasswordHasher|ValidateCredentials|CheckPassword|IsValid", RegexOptions.Compiled)),
        (new Regex(@"(?i)email|e-mail|thư điện tử", RegexOptions.Compiled), new Regex(@"(?i)email|e-mail|mailaddress", RegexOptions.Compiled)),
        (new Regex(@"(?i)mật khẩu|password|passwd", RegexOptions.Compiled), new Regex(@"(?i)password|passwd|credential|hasher", RegexOptions.Compiled)),
        (new Regex(@"(?i)OTP|mã xác thực một lần", RegexOptions.Compiled), new Regex(@"(?i)OTP|one.?time.?password|verification.?code", RegexOptions.Compiled)),
        (new Regex(@"(?i)OAuth|Google Login|đăng nhập Google", RegexOptions.Compiled), new Regex(@"(?i)OAuth|Google|ExternalLogin", RegexOptions.Compiled)),
        (new Regex(@"(?i)đăng nhập|login|sign.?in", RegexOptions.Compiled), new Regex(@"(?i)login|sign.?in|authenticate|authentication|password|credential|ClaimTypes\.NameIdentifier", RegexOptions.Compiled)),
        (new Regex(@"(?i)đăng ký|register|sign.?up", RegexOptions.Compiled), new Regex(@"(?i)register|sign.?up|registration|CreateUser|UserManager", RegexOptions.Compiled)),
        (new Regex(@"(?i)phân quyền|phân vai trò|role.?based|authorization", RegexOptions.Compiled), new Regex(@"(?i)RequireAuthorization|RequireRole|Authorize|Roles|ClaimTypes|permission", RegexOptions.Compiled)),
        (new Regex(@"(?i)gửi email|email thông báo|send email", RegexOptions.Compiled), new Regex(@"(?i)SendEmail|Smtp|MailMessage|IEmailSender|EmailService", RegexOptions.Compiled)),
        (new Regex(@"(?i)tải lên|upload file|đính kèm tệp", RegexOptions.Compiled), new Regex(@"(?i)IFormFile|Upload|Multipart|FileStream|OpenReadStream", RegexOptions.Compiled)),
        (new Regex(@"(?i)thanh toán|payment|checkout", RegexOptions.Compiled), new Regex(@"(?i)Payment|Checkout|Stripe|Transaction", RegexOptions.Compiled)),
        (new Regex(@"(?i)lưu vào (?:cơ sở dữ liệu|database)|ghi dữ liệu|persist", RegexOptions.Compiled), new Regex(@"(?i)DbContext|SaveChanges|INSERT|CREATE TABLE|migrationBuilder", RegexOptions.Compiled))
    ];

    /// <summary>Legacy deterministic groups retained as a fallback for old reports.</summary>
    public static IReadOnlyList<object> Build(
        IReadOnlyList<ProgressFeatureFact> features,
        IReadOnlyList<ProgressFeatureExplanation> explanations)
    {
        var byId = explanations.GroupBy(x => x.FactId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Explanation, StringComparer.Ordinal);
        return features.Select((f, i) => (object)new
        {
            file = f.Citations[0].File,
            area = f.Area,
            title = f.Title,
            evidence = f.Citations[0].Evidence,
            description = f.Description,
            factId = $"F{i + 1}",
            aiExplanation = byId.GetValueOrDefault($"F{i + 1}"),
            citations = f.Citations.Select(c => new { file = c.File, evidence = c.Evidence }).ToArray(),
            source = "feature-rule"
        }).ToArray();
    }

    public static IReadOnlyList<object> BuildAi(IReadOnlyList<ProgressAiFeature> features,
        string source = "ai-feature-report") =>
        features.Select((f, i) => (object)new
        {
            file = f.Citations[0].File,
            area = "Chức năng",
            title = f.Name,
            evidence = f.Citations[0].Evidence,
            description = f.Summary,
            factId = $"AF{i + 1}",
            featureId = $"AF{i + 1}",
            steps = f.Steps.Select(s => new
            {
                detail = s.Detail,
                citations = s.Citations.Select(c => new { file = c.File, evidence = c.Evidence }).ToArray()
            }).ToArray(),
            citations = f.Citations.Select(c => new { file = c.File, evidence = c.Evidence }).ToArray(),
            components = f.Components,
            technologies = f.Technologies,
            verificationStatus = f.VerificationStatus,
            evidenceCoverage = f.EvidenceCoverage,
            source
        }).ToArray();

    // Conservative safety net: only concrete new production endpoint mappings.
    // Not an Ollama-generated feature, and never claims the handler/test succeeded.
    private static readonly Regex AddedEndpoint = new(
        @"^\s*(?:[A-Za-z_]\w*\.)+Map(?<verb>Get|Post|Put|Patch|Delete)\s*\(\s*""(?<route>[^""\r\n]{1,120})""\s*,\s*(?<handler>[A-Za-z_]\w*)",
        RegexOptions.Compiled);

    public static IReadOnlyList<ProgressAiFeature> BuildRouteFallback(IReadOnlyList<ProgressFile> files)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<ProgressAiFeature>();
        foreach (var file in files.OrderBy(x => x.Path, StringComparer.Ordinal))
        {
            if (!file.Path.StartsWith("backend/", StringComparison.OrdinalIgnoreCase) ||
                !file.Path.Contains("/Endpoints/", StringComparison.OrdinalIgnoreCase) ||
                !file.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                !GitProgressEvidencePolicy.SafePath(file.Path)) continue;
            foreach (var line in GitProgressEvidencePolicy.EvidenceLines(file, 100_000))
            {
                if (!IsSafeAddedEvidence(new ProgressEvidenceReference("", file.Path, line))) continue;
                var match = AddedEndpoint.Match(line[1..]);
                if (!match.Success) continue;
                var verb = match.Groups["verb"].Value.ToUpperInvariant();
                var route = match.Groups["route"].Value;
                var handler = match.Groups["handler"].Value;
                if (!seen.Add(verb + " " + route)) continue;
                var name = EndpointName(verb, route);
                var summary = $"Bổ sung khai báo tuyến {verb} {route} tại backend; " +
                    $"chuyển yêu cầu đến handler {handler}. Chưa có bằng chứng kiểm thử chạy đạt.";
                var step = new ProgressFeatureReportStep(
                    $"Khai báo tuyến API {verb} {route}, dùng handler {handler}.",
                    [new ProgressFeatureCitation(file.Path, line)]);
                result.Add(CreateFeature(name, summary, [step]));
                if (result.Count >= 25) return result;
            }
        }
        return result;
    }

    // A model summary backed ONLY by MapGet/MapPost/MapDelete declarations
    // cannot prove complete business features. Normalize those cases to one
    // verified operation per route instead of retaining a mixed or misnamed claim.
    public static bool IsRouteOnlyReport(IReadOnlyList<ProgressAiFeature> items) =>
        items.Count > 0 && items.All(feature => feature.Citations.Count > 0 &&
            feature.Citations.All(c => c.Evidence.StartsWith('+') &&
                AddedEndpoint.IsMatch(c.Evidence[1..])));

    // Add directly matching changed source lines to the route-level fallback.
    // Only describe the exact operation visible in that line; do not infer
    // that handlers, UI wiring, persistence or tests passed at runtime.
    public static IReadOnlyList<ProgressAiFeature> BuildRouteEvidenceReport(IReadOnlyList<ProgressFile> files)
    {
        var routes = BuildRouteFallback(files);
        var lines = files.Where(f => GitProgressEvidencePolicy.SafePath(f.Path))
            .SelectMany(f => GitProgressEvidencePolicy.EvidenceLines(f, 100_000)
                .Select(line => new ProgressEvidenceReference("", f.Path, line)))
            .Where(IsSafeAddedEvidence).ToArray();
        var result = new List<ProgressAiFeature>();
        foreach (var routeFeature in routes)
        {
            var mapped = routeFeature.Citations[0];
            var mapping = AddedEndpoint.Match(mapped.Evidence[1..]);
            var handler = mapping.Groups["handler"].Value;
            var route = mapping.Groups["route"].Value;
            var verb = mapping.Groups["verb"].Value.ToUpperInvariant();
            var steps = routeFeature.Steps.ToList();

            void Add(string file, string exactPrefix, string description)
            {
                var source = lines.FirstOrDefault(c =>
                    c.File.Equals(file, StringComparison.OrdinalIgnoreCase) &&
                    c.Evidence[1..].TrimStart().StartsWith(exactPrefix, StringComparison.Ordinal));
                if (source is null) return;
                steps.Add(new ProgressFeatureReportStep(description,
                    [new ProgressFeatureCitation(source.File, source.Evidence)]));
            }

            // A handler declaration proves its presence, not that its body ran.
            Add(mapped.File, "private static async Task<IResult> " + handler + "(",
                $"Backend khai báo handler {handler} cho tuyến {verb} {route}.");

            // Scope action evidence to the added handler body in the *actual patch*,
            // including unchanged context and braces. Do not borrow SaveChanges,
            // permission checks or audit calls from another method or diff hunk.
            var filePatch = files.FirstOrDefault(f =>
                f.Path.Equals(mapped.File, StringComparison.OrdinalIgnoreCase))?.Patch ?? "";
            var patchLines = filePatch.Replace("\r\n", "\n").Split('\n');
            var declaration = "private static async Task<IResult> " + handler + "(";
            var start = Array.FindIndex(patchLines, line =>
                line.StartsWith('+') && !line.StartsWith("+++") &&
                line[1..].TrimStart().StartsWith(declaration, StringComparison.Ordinal));
            var handlerEvidence = new List<ProgressEvidenceReference>();
            if (start >= 0)
            {
                var depth = 0;
                var opened = false;
                for (var i = start + 1; i < patchLines.Length; i++)
                {
                    var diffLine = patchLines[i];
                    if (diffLine.StartsWith("@@", StringComparison.Ordinal) ||
                        diffLine.StartsWith("+++", StringComparison.Ordinal) ||
                        diffLine.StartsWith("---", StringComparison.Ordinal)) break;
                    if (diffLine.Length == 0 ||
                        diffLine[0] is not ('+' or '-' or ' ')) continue;
                    var code = diffLine[1..];
                    if (!opened && Regex.IsMatch(code,
                        @"^\s*(?:private|public|internal|protected)\s+(?:(?:static|async|sealed|readonly|partial)\s+)*[A-Za-z_]\w*"))
                        break;
                    if (diffLine[0] == '+' &&
                        GitProgressEvidencePolicy.IsAiSafeDiffLine(diffLine))
                    {
                        var evidence = new ProgressEvidenceReference("", mapped.File, diffLine);
                        if (IsSafeAddedEvidence(evidence) &&
                            lines.Any(c => c.File.Equals(mapped.File, StringComparison.OrdinalIgnoreCase) &&
                                c.Evidence == diffLine))
                            handlerEvidence.Add(evidence);
                    }
                    // Braces are used only as boundaries; never as evidence.
                    // A conservative stop at the first closed body prevents claims
                    // borrowed from later handlers in the same added file.
                    depth += code.Count(ch => ch == '{') - code.Count(ch => ch == '}');
                    if (code.Contains('{')) opened = true;
                    if (opened && depth <= 0) break;
                }
            }
            void AddHandler(string token, string description)
            {
                var source = handlerEvidence.FirstOrDefault(c =>
                    c.Evidence.Contains(token, StringComparison.Ordinal));
                if (source is null) return;
                steps.Add(new ProgressFeatureReportStep(description,
                    [new ProgressFeatureCitation(source.File, source.Evidence)]));
            }

            void AddContaining(string file, string token, string description)
            {
                var source = lines.FirstOrDefault(c =>
                    c.File.Equals(file, StringComparison.OrdinalIgnoreCase) &&
                    c.Evidence.Contains(token, StringComparison.Ordinal));
                if (source is null) return;
                steps.Add(new ProgressFeatureReportStep(description,
                    [new ProgressFeatureCitation(source.File, source.Evidence)]));
            }

            if (mapped.File.EndsWith("/GitHubV10Endpoints.cs", StringComparison.OrdinalIgnoreCase))
            {
                var api = "frontend/src/features/github/github.api.ts";
                var view = "frontend/src/views/GitHubV10View.vue";
                if (verb == "GET" && route.EndsWith("/projects/{projectId:guid}/repositories", StringComparison.Ordinal))
                {
                    AddHandler("!CanRead(project, user)", "Backend kiểm tra quyền xem repository của dự án.");
                    AddHandler("db.Repositories.AsNoTracking().Where(",
                        "Backend truy vấn các repository liên kết theo projectId.");
                    AddHandler("return Results.Ok(rows)", "Backend có lệnh trả về danh sách repository.");
                    Add(api, "list:", "Frontend khai báo hàm list để gọi API liệt kê repository theo dự án.");
                    AddContaining(view, "await githubApi.list(", "Giao diện có lời gọi tải danh sách repository.");
                }
                else if (verb == "POST" && route.EndsWith("/repositories", StringComparison.Ordinal))
                {
                    AddHandler("!CanManage(project, user)", "Backend kiểm tra quyền quản lý dự án trước khi liên kết.");
                    AddHandler("project.Status == ProjectStatus.CANCELLED", "Backend từ chối liên kết nếu dự án đã hủy.");
                    AddHandler("!GitHubUrl.TryParse(input?.RepositoryUrl", "Backend kiểm tra URL repository được nhập.");
                    AddHandler("github.GetRepositoryAsync(owner, repo", "Backend yêu cầu metadata repository từ GitHub.");
                    AddHandler("db.Repositories.AsNoTracking().AnyAsync(",
                        "Backend kiểm tra repository đã liên kết với dự án hay chưa.");
                    AddHandler("db.Repositories.Add(link)", "Backend thêm bản ghi liên kết vào DbContext.");
                    AddHandler("db.SaveChangesAsync(ct)", "Backend có lệnh ghi các thay đổi liên kết vào cơ sở dữ liệu.");
                    AddHandler("Results.Created(", "Backend có lệnh trả về HTTP Created.");
                    Add(api, "link:", "Frontend khai báo hàm link nhận projectId và repositoryUrl.");
                    AddContaining(view, "await githubApi.link(", "Giao diện có lời gọi API liên kết repository.");
                    AddContaining(view, "@submit.prevent=\"link\"", "Biểu mẫu có sự kiện gửi yêu cầu liên kết.");
                }
                else if (verb == "GET" && route.EndsWith("/commits", StringComparison.Ordinal))
                {
                    AddHandler("!CanRead(project, user)", "Backend kiểm tra quyền xem dữ liệu dự án.");
                    AddHandler("github.GetCommitsAsync(owner, repo", "Backend gọi client lấy commit từ GitHub.");
                    AddHandler("Items = commits", "Backend có lệnh trả về danh sách commit nhận được.");
                    Add(api, "commits:", "Frontend khai báo hàm commits để gọi API danh sách commit.");
                    AddContaining(view, "githubApi.commits(link.id)", "Giao diện có lời gọi lấy commit của repository được chọn.");
                    AddContaining(view, "commits.value = recent.items", "Giao diện gán kết quả commit vào danh sách hiển thị.");
                }
                else if (verb == "GET" && route.EndsWith("/branches", StringComparison.Ordinal))
                {
                    AddHandler("!CanRead(project, user)", "Backend kiểm tra quyền xem dữ liệu dự án.");
                    AddHandler("github.GetBranchesAsync(owner, repo", "Backend gọi client lấy branch từ GitHub.");
                    Add(api, "branches:", "Frontend khai báo hàm branches để gọi API danh sách nhánh Git.");
                    AddContaining(view, "githubApi.branches(link.id)", "Giao diện có lời gọi lấy branch của repository được chọn.");
                    AddContaining(view, "branches.value = foundBranches.items", "Giao diện gán kết quả branch vào danh sách hiển thị.");
                }
                else if (verb == "DELETE" && handler == "Unlink")
                {
                    AddHandler("!CanManage(project, user)", "Backend kiểm tra quyền quản lý trước khi gỡ liên kết.");
                    AddHandler("db.Repositories.Remove(link)",
                        "Backend xóa bản ghi liên kết khỏi DbContext, không xóa repository trên GitHub.");
                    AddHandler("db.SaveChangesAsync(ct)", "Backend có lệnh lưu thao tác gỡ liên kết vào cơ sở dữ liệu.");
                    AddHandler("Results.NoContent()", "Backend có lệnh trả về HTTP No Content.");
                    Add(api, "unlink:", "Frontend khai báo hàm unlink để gọi API gỡ liên kết repository.");
                    AddContaining(view, "await githubApi.unlink(", "Giao diện có lời gọi API gỡ liên kết repository.");
                    AddContaining(view, "@click=\"removeLink(", "Giao diện có nút gỡ liên kết.");
                }
            }
            if (mapped.File.EndsWith("/ProjectProgressV07Endpoints.cs", StringComparison.OrdinalIgnoreCase) &&
                handler == "UpdateStudentDeadline" && verb == "PUT" &&
                route.EndsWith("/milestones/{id:guid}/deadline", StringComparison.Ordinal))
            {
                AddHandler("db.Milestones.FirstOrDefaultAsync(", "Backend tìm Milestone cần thay đổi theo ID.");
                AddHandler("!CanWrite(project, principal, \"Student\")",
                    "Backend kiểm tra quyền sửa của sinh viên đối với dự án.");
                AddHandler("item.Status is MilestoneStatus.SUBMITTED",
                    "Backend không cho sửa ngày khi Milestone đang chờ đánh giá hoặc đã duyệt.");
                AddHandler("req.DeadlineAt == default", "Backend kiểm tra yêu cầu ngày nộp hợp lệ.");
                AddHandler("deadline < item.StartAt", "Backend từ chối ngày nộp trước ngày bắt đầu Milestone.");
                AddHandler("deadline == EndOfDeadlineDay(item.DeadlineAt)",
                    "Backend tránh tạo yêu cầu xét duyệt mới khi ngày không đổi.");
                AddHandler("item.DeadlineAt = deadline", "Backend gán ngày nộp mới vào Milestone.");
                AddHandler("item.ProposalStatus = \"PENDING_REVIEW\"",
                    "Backend chuyển trạng thái đề xuất về chờ duyệt khi cần xác nhận lại.");
                AddHandler("Audit(db, Actor(principal)", "Backend ghi nhận sự kiện thay đổi ngày nộp trong audit.");
                AddHandler("await db.SaveChangesAsync(ct)",
                    "Backend có lệnh lưu thay đổi Milestone vào cơ sở dữ liệu.");
                var apiFile = "frontend/src/features/progress/projectProgress.api.ts";
                var viewFile = "frontend/src/views/ProjectProgressV07View.vue";
                AddContaining(apiFile, "updateDeadline:",
                    "Frontend khai báo hàm gọi API cập nhật ngày nộp.");
                AddContaining(viewFile, "await projectProgressApi.updateDeadline(",
                    "Giao diện có lời gọi API lưu ngày nộp mới.");
                AddContaining(viewFile, "@click=\"saveDeadline(selected)\"",
                    "Giao diện có nút lưu ngày nộp mới.");
            }
            result.Add(CreateFeature(routeFeature.Name, routeFeature.Summary, steps));
        }
        return result;
    }

    // A generic model label can mix distinct GitHub operations and cite unrelated
    // cache/config/type lines. For GitHub V10, direct Map* route evidence takes
    // precedence EVEN IF the model also cited non-route lines. This avoids relying
    // on the former IsRouteOnlyReport() all-or-nothing condition.
    public static IReadOnlyList<ProgressAiFeature> BuildGitHubRouteReport(
        IReadOnlyList<ProgressFile> files) => BuildRouteEvidenceReport(files)
        .Where(f => f.Citations.Any(c =>
            c.File.EndsWith("/GitHubV10Endpoints.cs", StringComparison.OrdinalIgnoreCase)))
        .ToArray();

    public static bool IsGitHubFeatureClaim(ProgressAiFeature feature) =>
        feature.Citations.Any(c =>
            c.File.EndsWith("/GitHubV10Endpoints.cs", StringComparison.OrdinalIgnoreCase) ||
            c.File.EndsWith("/GitHubPublicClient.cs", StringComparison.OrdinalIgnoreCase) ||
            c.File.Contains("/features/github/", StringComparison.OrdinalIgnoreCase) ||
            c.File.EndsWith("/GitHubV10View.vue", StringComparison.OrdinalIgnoreCase)) ||
        Regex.IsMatch(feature.Name + " " + feature.Summary,
            @"(?i)github|\brepositor(?:y|ies)\b|\brepo\b|\bcommits?\b|\bbranches\b|kho\s+mã\s+nguồn|liên\s+kết\s+(?:git|repo)|gỡ\s+liên\s+kết\s+(?:git|repo)");

    private static string EndpointName(string verb, string route)
    {
        var path = route.ToLowerInvariant();
        if (path.EndsWith("/commits", StringComparison.Ordinal))
            return "Khai báo API xem commit";
        if (path.EndsWith("/branches", StringComparison.Ordinal))
            return "Khai báo API xem nhánh repository";
        if (path.EndsWith("/repositories", StringComparison.Ordinal))
            return verb switch
            {
                "GET" => "Khai báo API xem danh sách repository",
                "POST" => "Khai báo API liên kết repository",
                _ => $"Khai báo API {verb} {route}"
            };
        if (path.Contains("/repositories/", StringComparison.Ordinal) && verb == "DELETE")
            return "Khai báo API gỡ liên kết repository";
        return $"Khai báo API {verb} {route}";
    }

    public static ProgressAiFeatureReportResult Screen(
        IEnumerable<ProgressFeatureReportCandidate> candidates,
        IReadOnlyList<ProgressEvidenceReference> evidence,
        int rawItems)
    {
        var candidateList = candidates.Take(20).ToArray();
        var index = evidence.GroupBy(x => x.Id, StringComparer.Ordinal)
            .Where(g => g.Count() == 1)
            .ToDictionary(g => g.Key, g => g.Single(), StringComparer.Ordinal);
        var accepted = new List<ProgressAiFeature>();
        var rejectedEvidence = 0;
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in candidateList)
        {
            var name = Clean(candidate.Name, 100);
            var summary = Clean(candidate.Summary, 320);
            if (name.Length < 4 || summary.Length < 25 || CompletionClaim.IsMatch(name + " " + summary))
                continue;

            var steps = new List<ProgressFeatureReportStep>();
            foreach (var step in candidate.Steps.Take(8))
            {
                var detail = Clean(step.Detail, 260);
                if (detail.Length < 18 || CompletionClaim.IsMatch(detail)) continue;
                var citations = new List<ProgressFeatureCitation>();
                foreach (var id in step.EvidenceIds.Distinct(StringComparer.Ordinal).Take(8))
                {
                    if (!index.TryGetValue(id, out var source)) { rejectedEvidence++; continue; }
                    if (!IsSafeAddedEvidence(source)) { rejectedEvidence++; continue; }
                    citations.Add(new ProgressFeatureCitation(source.File, source.Evidence));
                }
                var distinct = citations.DistinctBy(x => (x.File, x.Evidence)).ToArray();
                citations = distinct.Where(c => SupportsStepEvidence(detail, c)).ToList();
                rejectedEvidence += distinct.Length - citations.Count;
                if (citations.Count == 0) continue;
                steps.Add(new ProgressFeatureReportStep(detail, citations));
            }

            var allCitations = steps.SelectMany(s => s.Citations)
                .DistinctBy(x => (x.File, x.Evidence)).ToArray();
            if (steps.Count == 0 || allCitations.Length == 0 ||
                !SupportsText(name + ". " + summary, allCitations) ||
                !SupportsFeatureOperation(name, summary, allCitations))
                continue;

            var normalized = Regex.Replace(name, @"[^\p{L}\p{N}]+", " ").Trim();
            if (!names.Add(normalized)) continue;
            accepted.Add(CreateFeature(name, summary, steps));
        }

        var status = accepted.Count > 0 ? "accepted" : rawItems == 0 ? "empty" : "filtered";
        return new ProgressAiFeatureReportResult(accepted, rawItems, candidateList.Length, rejectedEvidence, status);
    }

    // Multiple model batches may refer to the same feature. Recombine the
    // verified steps, not the unchecked model text or evidence IDs.
    public static IReadOnlyList<ProgressAiFeature> Merge(IReadOnlyList<ProgressAiFeature> features) =>
        features.GroupBy(x => Regex.Replace(x.Name, @"[^\p{L}\p{N}]+", " ").Trim(),
                StringComparer.OrdinalIgnoreCase)
            .Select(g => CreateFeature(g.First().Name, g.First().Summary,
                g.SelectMany(x => x.Steps).DistinctBy(x => x.Detail).Take(16).ToArray()))
            .ToArray();

    private static ProgressAiFeature CreateFeature(string name, string summary,
        IReadOnlyList<ProgressFeatureReportStep> steps)
    {
        var citations = steps.SelectMany(x => x.Citations)
            .DistinctBy(x => (x.File, x.Evidence)).ToArray();
        var components = Components(citations);
        var technologies = Technologies(citations);
        var production = citations.Any(c => IsProductionPath(c.File));
        var tests = citations.Any(c => IsTestPath(c.File));
        var layers = components.Count(x => x is "Frontend" or "Backend/API" or "Cơ sở dữ liệu");
        var verification = production && tests && layers >= 2
            ? "Có mã triển khai ở nhiều lớp và có mã kiểm thử trong diff; chưa có kết quả chạy test để xác nhận pass."
            : production && tests
                ? "Có thay đổi ở một lớp triển khai và có mã kiểm thử; chưa đủ chứng minh toàn bộ luồng hoặc test đã pass."
                : production && layers >= 2
                    ? "Có bằng chứng mã ở nhiều lớp triển khai; chưa xác minh chức năng chạy thực tế."
                    : production
                        ? "Bằng chứng mới ở một lớp triển khai; chưa đủ xác nhận toàn bộ chức năng hoặc hành vi chạy thực tế."
                        : "Chỉ có bằng chứng kiểm thử/cấu hình/tài liệu; chưa chứng minh chức năng production mới.";
        return new ProgressAiFeature(name, summary, steps, citations, components, technologies,
            verification, citations.Select(c => c.File).Distinct(StringComparer.Ordinal).Count() > 1
                ? "Đối chiếu nhiều file" : "Bằng chứng một file");
    }

    // Restrict feature claims to their actual domain. Merely referencing any
    // valid added line is insufficient (e.g. a notification helper cannot prove
    // UpdateMilestone; a repository URL cannot prove a branch-list feature).
    private static readonly (Regex Claim, Regex Evidence)[] FeatureDomains =
    [
        (new(@"(?i)milestone|mốc\s+tiến\s+độ", RegexOptions.Compiled), new(@"(?i)milestone|mốc\s+tiến\s+độ", RegexOptions.Compiled)),
        (new(@"(?i)\baudit\b|nhật\s+ký\s+(?:hoạt\s+động|kiểm\s+toán)", RegexOptions.Compiled), new(@"(?i)\baudit\b|AuditLog", RegexOptions.Compiled)),
        (new(@"(?i)\bbranch(?:es)?\b|nhánh\s+git", RegexOptions.Compiled), new(@"(?i)\bbranch(?:es)?\b|GetBranches", RegexOptions.Compiled)),
        (new(@"(?i)\bcommit(?:s)?\b|lịch\s+sử\s+commit", RegexOptions.Compiled), new(@"(?i)\bcommit(?:s)?\b", RegexOptions.Compiled)),
        (new(@"(?i)\brepositor(?:y|ies)\b|\brepo\b|kho\s+mã\s+nguồn", RegexOptions.Compiled), new(@"(?i)\brepositor(?:y|ies)\b|\brepo\b", RegexOptions.Compiled)),
        (new(@"(?i)\bproposal\b|đề\s+xuất\s+đề\s+tài", RegexOptions.Compiled), new(@"(?i)\bproposal\b|PENDING_REVIEW", RegexOptions.Compiled)),
        (new(@"(?i)\bnotification\b|thông\s+báo", RegexOptions.Compiled), new(@"(?i)\bnotification\b|Notify|SendNotice", RegexOptions.Compiled))
    ];

    private static bool SupportsFeatureOperation(string name, string summary,
        IReadOnlyList<ProgressFeatureCitation> citations)
    {
        var code = string.Join("\n", citations.Select(x => x.Evidence[1..]));
        var claimText = name + " " + summary;

        // Reject multi-action feature labels when each action is not supported.
        // A date editor or a lecturer selector cannot establish topic creation,
        // and a form field cannot by itself establish persistence/management.
        var topic = Regex.IsMatch(claimText, @"(?i)chủ\s+đề|đề\s+tài|\btopics?\b|\bsubjects?\b");
        var schedule = Regex.IsMatch(claimText, @"(?i)lịch\s+trình|\bschedules?\b");
        var period = Regex.IsMatch(claimText, @"(?i)kỳ\s+hạn|đợt\s+nộp|\bperiods?\b");
        var creation = Regex.IsMatch(claimText, @"(?i)\btạo\b|\bcreate\b");
        var update = Regex.IsMatch(claimText, @"(?i)cập\s+nhật|\bupdate\b|chỉnh\s+sửa");
        if (topic && creation &&
            !Regex.IsMatch(code, @"(?i)Create(?:New)?Topic|AddTopic|createTopic|MapPost\s*\([^\r\n]*topics|Topics\.Add\s*\("))
            return false;
        if (topic && update &&
            !Regex.IsMatch(code, @"(?i)UpdateTopic|EditTopic|updateTopic|Map(?:Put|Patch)\s*\([^\r\n]*topics"))
            return false;
        if (schedule && creation &&
            !Regex.IsMatch(code, @"(?i)CreateSchedule|createSchedule|CreatePeriod|createPeriod|MapPost\s*\([^\r\n]*(?:schedule|period)"))
            return false;
        if (schedule && update &&
            !Regex.IsMatch(code, @"(?i)UpdateSchedule|updateSchedule|UpdatePeriod|updatePeriod|Map(?:Put|Patch)\s*\([^\r\n]*(?:schedule|period)"))
            return false;
        if (period && creation && Regex.IsMatch(claimText, @"(?i)quản\s+lý|manage") &&
            !Regex.IsMatch(code, @"(?i)(?:MapPost\s*\([^\r\n]*period|Periods\.Add\s*\(|CreatePeriodAsync|SaveChangesAsync\s*\()"))
            return false;
        if (Regex.IsMatch(claimText, @"(?i)(?:ngày\s+bắt\s+đầu|start).*(?:so\s+với|sau|after|lớn\s+hơn).*(?:ngày\s+kết\s+thúc|end)") &&
            !Regex.IsMatch(code, @"(?i)(?:start(?:\.getTime\(\))?\s*(?:>|>=)\s*end|end(?:\.getTime\(\))?\s*<\s*start|isAfter\s*\()"))
            return false;
        // Do not infer which user role performs a change from a route alone.
        if (Regex.IsMatch(claimText, @"(?i)cho\s+giảng\s+viên|\bfor\s+lecturers?\b") &&
            !Regex.IsMatch(code, @"(?i)\bLecturer\b|RoleIds\.Lecturer|RequireRole\([^\r\n]*Lecturer"))
            return false;

        // A release can change the way a request row is highlighted without
        // implementing four different request-status workflows. Require actual
        // status-specific code when the model calls a status a new feature.
        foreach (var status in new[] { "SUPERVISION", "REVISION_REQUIRED", "REJECTED", "CANCELLED" })
            if (claimText.Contains(status, StringComparison.OrdinalIgnoreCase) &&
                !code.Contains(status, StringComparison.OrdinalIgnoreCase)) return false;

        // The model must not promote a button to edit a date or a plain view
        // heading to a distinct deadline-checking feature. Preserve such UI
        // changes as details only when accurately described as UI changes.
        if (Regex.IsMatch(claimText, @"(?i)(?:kiểm\s+tra|xác\s+minh|check|validate).{0,80}(?:hạn\s+nộp|ngày\s+nộp|deadline|milestone)") &&
            !Regex.IsMatch(code, @"(?i)\bif\s*\(|\bswitch\s*\(|TryParse|getTime\s*\(|Date\.parse|CompareTo|IsOverdue|isDeadlineValid|ValidateDeadline"))
            return false;
        if (Regex.IsMatch(claimText, @"(?i)người\s+dùng\s+khác|other\s+users?") &&
            !Regex.IsMatch(code, @"(?i)otherUser|targetUser|UserId\s*[!=]=|actor\s*[!=]=|RoleIds\.|RequireRole|IsInRole|canViewOther"))
            return false;

        // Deadline/period mutations cannot be proven by a date-calculation
        // helper, a form option, notification refresh, or status display alone.
        var deadline = Regex.IsMatch(claimText,
            @"(?i)hạn\s+nộp|kỳ\s+hạn|deadline|due\s*date|submission\s+period");
        if (deadline && Regex.IsMatch(claimText, @"(?i)cập\s+nhật|thay\s+đổi|gia\s+hạn|update|extend") &&
            !Regex.IsMatch(code, @"(?i)Update(?:Deadline|DueDate|Period)|Map(?:Put|Patch)\s*\(|(?:DueDate|Deadline|EndDate|dueDate|deadline)\s*=(?!=)|SaveChanges(?:Async)?\s*\("))
            return false;
        if (deadline && Regex.IsMatch(claimText, @"(?i)tạo\s+(?:và\s+quản\s+lý\s+)?(?:kỳ|đợt|hạn)|create\s+(?:a\s+)?(?:submission\s+)?period") &&
            !Regex.IsMatch(code, @"(?i)Create(?:Submission)?Period|MapPost\s*\(|@submit|submitPeriod|savePeriod|SaveChanges(?:Async)?\s*\("))
            return false;
        if (Regex.IsMatch(claimText, @"(?i)xử\s+lý\s+yêu\s+cầu|process\s+(?:a\s+)?request") &&
            Regex.IsMatch(claimText, @"(?i)SUPERVISION|REVISION_REQUIRED|REJECTED|CANCELLED") &&
            !Regex.IsMatch(code, @"(?i)Map(?:Post|Put|Patch|Delete)\s*\(|(?:Status|status)\s*=(?!=)|(?:Process|Handle|Approve|Reject|Cancel|Review)\w*\s*\("))
            return false;
        foreach (var (claim, expected) in FeatureDomains)
            if (claim.IsMatch(name + " " + summary) && !expected.IsMatch(code)) return false;
        return true;
    }

    public static ProgressFeatureEvidenceSet BuildEvidence(
        IReadOnlyList<ProgressEvidenceReference> modelCandidates,
        IReadOnlyList<ProgressFeatureFact> ruleFacts)
    {
        var ruleEvidence = ruleFacts.SelectMany(f => f.Citations)
            .Select((c, i) => new ProgressEvidenceReference($"R{i + 1}", c.File, c.Evidence));
        var perFile = ruleEvidence.Concat(modelCandidates)
            .Where(IsSafeAddedEvidence)
            .GroupBy(x => x.File, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => g.DistinctBy(x => x.Evidence).ToArray())
            .ToArray();
        // Round-robin across files so an early, large file cannot crowd later
        // changed files out of the feature-grouping prompt.
        var all = perFile.SelectMany(x => x).ToArray();
        var result = new List<ProgressEvidenceReference>();
        // Preserve substantially more evidence; the model receives it in small
        // batches, never one giant prompt. Any remaining cap is reported.
        const int MaxEvidence = 1000;
        for (var offset = 0; result.Count < MaxEvidence; offset++)
        {
            var added = false;
            foreach (var group in perFile)
            {
                if (offset >= group.Length || result.Count >= MaxEvidence) continue;
                result.Add(group[offset]);
                added = true;
            }
            if (!added) break;
        }
        // IDs from the diff batcher are not trusted as report IDs; issue a compact,
        // unique set only after safe paths/added lines have been screened.
        var indexed = result.Select((x, i) => x with { Id = $"E{i + 1}" }).ToArray();
        return new ProgressFeatureEvidenceSet(indexed, all.Length, Math.Max(0, all.Length - indexed.Length));
    }

    private static bool IsSafeAddedEvidence(ProgressEvidenceReference source) =>
        GitProgressEvidencePolicy.SafePath(source.File) &&
        source.Evidence.StartsWith('+') && !source.Evidence.StartsWith("+++", StringComparison.Ordinal) &&
        source.Evidence.Length <= 210 && !GitProgressSemanticEvidencePolicy.IsSupportingOnlyLine(source.File, source.Evidence);

    private static bool SupportsStepEvidence(string detail, ProgressFeatureCitation citation)
    {
        if (!SupportsText(detail, [citation])) return false;
        var path = citation.File;
        var code = citation.Evidence[1..].Trim();
        var text = detail;
        // A deadline claim cannot cite an unrelated DTO property, status, cache
        // configuration or view title. Require this precise line to identify the
        // deadline/milestone/period operation that it purports to describe.
        if (Regex.IsMatch(text, @"(?i)hạn\s+nộp|ngày\s+nộp|deadline|due\s*date|kỳ\s+hạn|milestone") &&
            !Regex.IsMatch(code, @"(?i)deadline|dueDate|due_date|ngày\s+nộp|milestone|periodForm|CreatePeriod|UpdatePeriod|endsDate|startDate"))
            return false;
        // A button labelled 'Sửa ngày' or an <h4> heading does not perform a
        // deadline check. Only a real validation/conditional expression can
        // establish the behavior described by 'kiểm tra hạn nộp'.
        if (Regex.IsMatch(text, @"(?i)(?:kiểm\s+tra|xác\s+minh|check|validate).{0,80}(?:hạn\s+nộp|ngày\s+nộp|deadline|milestone)") &&
            !Regex.IsMatch(code, @"(?i)\bif\s*\(|\bswitch\s*\(|TryParse|getTime\s*\(|Date\.parse|CompareTo|IsOverdue|isDeadlineValid|ValidateDeadline"))
            return false;
        // Checking whether a date parses is not the same as checking start <= end.
        if (Regex.IsMatch(text, @"(?i)(?:ngày\s+bắt\s+đầu|start).*(?:sau|lớn\s+hơn|after|so\s+với).*(?:ngày\s+kết\s+thúc|end)") &&
            !Regex.IsMatch(code, @"(?i)(?:start(?:\.getTime\(\))?\s*(?:>|>=)\s*end|end(?:\.getTime\(\))?\s*<\s*start|isAfter\s*\()"))
            return false;

        if (Regex.IsMatch(code, @"(?i)^Task<.*>\s+\w+\s*\(")) return false; // interface contract, not implementation
        foreach (var status in new[] { "SUPERVISION", "REVISION_REQUIRED", "REJECTED", "CANCELLED" })
            if (text.Contains(status, StringComparison.OrdinalIgnoreCase) &&
                !code.Contains(status, StringComparison.OrdinalIgnoreCase)) return false;
        if (Regex.IsMatch(text, @"(?i)(?:cập\s+nhật|update|gia\s+hạn).{0,55}(?:hạn\s+nộp|deadline|due\s*date|kỳ\s+hạn)") &&
            !Regex.IsMatch(code, @"(?i)Update(?:Deadline|DueDate|Period)|Map(?:Put|Patch)\s*\(|(?:DueDate|Deadline|EndDate|dueDate|deadline)\s*=(?!=)|SaveChanges(?:Async)?\s*\(")) return false;

        // Strong feature terms must be evidenced by THIS cited code line, not a
        // vaguely related line from elsewhere in the same report.
        foreach (var (claim, expected) in FeatureDomains)
            if (claim.IsMatch(text) && !expected.IsMatch(code)) return false;
        if (Regex.IsMatch(text, @"(?i)\bendpoint\b|\broute\b|tuyến\s+api") &&
            !Regex.IsMatch(code, @"(?i)\bMap(?:Get|Post|Put|Patch|Delete)\s*\(")) return false;
        if (Regex.IsMatch(text, @"(?i)\baudit\b|ghi\s+nhật\s+ký") &&
            !Regex.IsMatch(code, @"(?i)\bAudit\b|AuditLog|WriteAudit")) return false;
        // Reading a status in a conditional is not evidence of changing it.
        if (Regex.IsMatch(text, @"(?i)cập\s+nhật\s+trạng\s+thái|thay\s+đổi\s+trạng\s+thái|(?:set|update)s?\s+(?:the\s+)?(?:proposal\s+)?status") &&
            !Regex.IsMatch(code, @"(?i)\b(?:MapPut|MapPatch|Update|SaveChanges|PENDING_REVIEW)\b|(?:Status|status)\s*=(?!=)"))
            return false;

        // Installer scripts and generic SQL/session settings are never feature steps.
        if (path.StartsWith("scripts/", StringComparison.OrdinalIgnoreCase)) return false;
        if (path.EndsWith(".sql", StringComparison.OrdinalIgnoreCase) &&
            (!Regex.IsMatch(text, @"(?i)database|cơ sở dữ liệu|bảng|schema|migration") ||
             !Regex.IsMatch(code, @"(?i)CREATE\s+TABLE|ALTER\s+TABLE|INSERT\s+INTO|CREATE\s+(?:UNIQUE\s+)?INDEX|migrationBuilder"))) return false;
        if (IsTestPath(path) &&
            (!Regex.IsMatch(text, @"(?i)test|kiểm thử|kiểm tra tự động") ||
             Regex.IsMatch(code, @"(?i)Fake|ConfigureTestServices|TestServer|WebApplicationFactory|new\s+HttpClient"))) return false;

        // UI/form claims require UI evidence even when they mention link or unlink.
        if (Regex.IsMatch(text, @"(?i)\bform\b|biểu mẫu|giao diện nhập|nút .*?(?:liên kết|xóa|gỡ)") &&
            (!path.StartsWith("frontend/src/", StringComparison.OrdinalIgnoreCase) ||
             !Regex.IsMatch(code, @"(?i)<form|<input|<button|v-model|@submit|@click|onSubmit|function\s+(?:link|remove)")))
            return false;

        // Check removal before the broader 'liên kết repository' creation phrase.
        if (Regex.IsMatch(text, @"(?i)xóa liên kết|gỡ liên kết|unlink|remove.*repository"))
            return Regex.IsMatch(code, @"(?i)Unlink|RemoveLink|removeLink|MapDelete|DELETE");
        if (Regex.IsMatch(text, @"(?i)tạo liên kết|liên kết repository|link repository|create.*repository"))
            return Regex.IsMatch(code, @"(?i)\b(link|Link|MapPost|repositoryUrl|repository_url)\b") &&
                   !Regex.IsMatch(code, @"(?i)commits|MapGet");
        if (Regex.IsMatch(text, @"(?i)danh sách repository|liệt kê repository|list repositories|repository list"))
            return Regex.IsMatch(code, @"(?i)\b(List|list|MapGet|links)\b|v-for") &&
                   !Regex.IsMatch(code, @"(?i)commits|GetRepository");
        if (Regex.IsMatch(text, @"(?i)chi tiết repository|thông tin repository|repository details"))
            return Regex.IsMatch(code, @"(?i)GetRepository|RepositoryInfo|FullName|DefaultBranch") &&
                   !Regex.IsMatch(code, @"(?i)commits");
        return true;
    }

    private static bool SupportsText(string text, IReadOnlyList<ProgressFeatureCitation> citations)
    {
        if (CompletionClaim.IsMatch(text)) return false;
        var code = string.Join("\n", citations.Select(c => c.Evidence[1..]));
        var paths = citations.Select(c => c.File).ToArray();
        foreach (var (claim, evidence) in Anchors)
            if (claim.IsMatch(text) && !evidence.IsMatch(code)) return false;
        if (Regex.IsMatch(text, @"(?i)frontend|giao\s+diện") &&
            !paths.Any(p => p.StartsWith("frontend/src/", StringComparison.OrdinalIgnoreCase))) return false;
        if (Regex.IsMatch(text, @"(?i)backend|máy\s+chủ|server") &&
            !paths.Any(p => p.StartsWith("backend/", StringComparison.OrdinalIgnoreCase) && !IsTestPath(p))) return false;
        if (Regex.IsMatch(text, @"(?i)kiểm\s+thử|test(?:\s+tự\s+động)?") &&
            !paths.Any(IsTestPath)) return false;
        if (Regex.IsMatch(text, @"(?i)cơ\s+sở\s+dữ\s+liệu|database|migration") &&
            !paths.Any(p => p.Contains("/Migrations/", StringComparison.OrdinalIgnoreCase) ||
                            p.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)) &&
            !Regex.IsMatch(code, @"(?i)DbContext|DbSet|SaveChanges|INSERT\s+INTO|CREATE\s+TABLE|ExecuteSql")) return false;
        foreach (Match symbol in BacktickSymbol.Matches(text))
            if (!Regex.IsMatch(code, @"(?<![A-Za-z0-9_])" + Regex.Escape(symbol.Groups["name"].Value) +
                    @"(?![A-Za-z0-9_])", RegexOptions.IgnoreCase)) return false;
        foreach (Match route in Route.Matches(text))
            if (!code.Contains(route.Value, StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    private static IReadOnlyList<string> Components(IReadOnlyList<ProgressFeatureCitation> citations)
    {
        var components = new List<string>();
        if (citations.Any(c => c.File.StartsWith("frontend/src/", StringComparison.OrdinalIgnoreCase))) components.Add("Frontend");
        if (citations.Any(c => c.File.StartsWith("backend/", StringComparison.OrdinalIgnoreCase) && !IsTestPath(c.File))) components.Add("Backend/API");
        if (citations.Any(c => c.File.Contains("/Migrations/", StringComparison.OrdinalIgnoreCase) || c.File.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))) components.Add("Cơ sở dữ liệu");
        if (citations.Any(c => IsTestPath(c.File))) components.Add("Kiểm thử");
        if (citations.Any(c => c.File.StartsWith("scripts/", StringComparison.OrdinalIgnoreCase))) components.Add("Script/cấu hình");
        if (citations.Any(c => c.File.EndsWith(".md", StringComparison.OrdinalIgnoreCase))) components.Add("Tài liệu");
        return components;
    }

    private static IReadOnlyList<string> Technologies(IReadOnlyList<ProgressFeatureCitation> citations)
    {
        var result = new List<string>();
        var code = string.Join("\n", citations.Select(c => c.Evidence));
        var paths = citations.Select(c => c.File).ToArray();
        if (paths.Any(p => p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))) result.Add("C#/.NET");
        if (paths.Any(p => p.EndsWith(".vue", StringComparison.OrdinalIgnoreCase))) result.Add("Vue");
        if (paths.Any(p => p.EndsWith(".ts", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase))) result.Add("TypeScript");
        if (Regex.IsMatch(code, @"(?i)\bMap(?:Get|Post|Put|Delete|Patch)\s*\(")) result.Add("ASP.NET Minimal API");
        if (Regex.IsMatch(code, @"(?i)DbContext|DbSet|migrationBuilder")) result.Add("Entity Framework Core");
        if (Regex.IsMatch(code, @"(?i)\bJWT\b|Jwt[A-Za-z0-9_]*|Bearer|CreateToken")) result.Add("JWT/token");
        if (Regex.IsMatch(code, @"(?i)PasswordHasher|BCrypt|Argon2|PBKDF2|PasswordHash")) result.Add("Hash/xác thực mật khẩu");
        if (paths.Any(p => p.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)) ||
            Regex.IsMatch(code, @"(?i)SqlConnection|CREATE\s+TABLE|INSERT\s+INTO")) result.Add("SQL");
        return result;
    }

    private static bool IsTestPath(string path) =>
        path.Contains("IntegrationTests/", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("/tests/", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".spec.ts", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".test.ts", StringComparison.OrdinalIgnoreCase);

    private static bool IsProductionPath(string path) =>
        path.StartsWith("backend/", StringComparison.OrdinalIgnoreCase) && !IsTestPath(path) ||
        path.StartsWith("frontend/src/", StringComparison.OrdinalIgnoreCase);

    private static string Clean(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim()[..Math.Min(value.Trim().Length, max)];
}
