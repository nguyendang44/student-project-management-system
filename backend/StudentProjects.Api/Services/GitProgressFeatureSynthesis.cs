using System.Text.RegularExpressions;

namespace StudentProjects.Api.Services;

public sealed record ProgressFeatureCitation(string File, string Evidence);
public sealed record ProgressFeatureFact(string Area, string Title, string Description,
    IReadOnlyList<ProgressFeatureCitation> Citations);

/// <summary>
/// Describe observable, changed source structures, NOT runtime results or AI judgments.
/// Unlike Ollama's excerpts, these facts come from GitHub's returned diff files directly.
/// Each citation must be an exact, safe, ADDED line from the compared commits.
/// </summary>
public static class GitProgressFeatureSynthesis
{
    private static readonly Regex Route = new(
        @"^(?:[A-Za-z_]\w*\.)+Map(?<verb>Get|Post|Put|Delete|Patch)\s*\(\s*""(?<url>[^""\r\n]{1,100})""\s*,\s*(?<handler>[A-Za-z_]\w*)",
        RegexOptions.Compiled);
    private static readonly Regex TableName = new(
        @"\bname\s*:\s*""(?<name>[A-Za-z_][A-Za-z_0-9]{1,85})""",
        RegexOptions.Compiled);
    private static readonly Regex TestMethod = new(
        @"\bpublic\s+(?:async\s+)?(?:Task|void)\s+(?<name>[A-Za-z_][A-Za-z_0-9]{4,90})\s*\(",
        RegexOptions.Compiled);
    private static readonly Regex UiAction = new(
        "(?:@click|v-model(?:\\.[a-z]+)?)=\\\"(?<name>[A-Za-z_][A-Za-z_0-9.]*)\\\"",
        RegexOptions.Compiled);

    private sealed record SafeLine(string File, string Text)
    {
        public ProgressFeatureCitation Citation => new(File, Text);
    }

    public static IReadOnlyList<ProgressFeatureFact> Extract(IReadOnlyList<ProgressFile> files)
    {
        var lines = files
            // "scripts/v010-backups/.../frontend/..." is not the active frontend.
            .Where(f => f.Path.StartsWith("backend/", StringComparison.OrdinalIgnoreCase) ||
                        f.Path.StartsWith("frontend/src/", StringComparison.OrdinalIgnoreCase))
            .Where(f => !string.IsNullOrWhiteSpace(f.Patch) && GitProgressEvidencePolicy.SafePath(f.Path))
            .Where(f => !f.Path.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase) &&
                        !f.Path.EndsWith("ModelSnapshot.cs", StringComparison.OrdinalIgnoreCase))
            .SelectMany(f => GitProgressEvidencePolicy.EvidenceLines(f, 500)
                .Where(x => x.StartsWith('+') && !x.StartsWith("+++", StringComparison.Ordinal))
                // `using var` creates a scoped HTTP request/response; it is not a namespace import.
                .Where(x => GitProgressMeaningPolicy.IsFunctionalLine(x) ||
                            x[1..].TrimStart().StartsWith("using var ", StringComparison.Ordinal))
                .Select(x => new SafeLine(f.Path, x)))
            .ToList();

        var results = new List<ProgressFeatureFact>();
        AddApi(lines, results);
        AddModelIntegration(lines, results);
        AddWorker(lines, results);
        AddFrontend(lines, results);
        AddDatabase(lines, results);
        AddTests(lines, results);
        return results.Take(7).ToArray();
    }

    private static bool Production(SafeLine x) =>
        x.File.StartsWith("backend/", StringComparison.OrdinalIgnoreCase) &&
        !Testing(x);

    private static bool Testing(SafeLine x) =>
        x.File.Contains("IntegrationTests/", StringComparison.OrdinalIgnoreCase) ||
        x.File.Contains("/tests/", StringComparison.OrdinalIgnoreCase) ||
        x.File.EndsWith(".spec.ts", StringComparison.OrdinalIgnoreCase);

    private static void AddApi(IReadOnlyList<SafeLine> lines, List<ProgressFeatureFact> result)
    {
        var routes = lines.Where(x => Production(x) && x.File.Contains("/Endpoints/", StringComparison.OrdinalIgnoreCase))
            .Select(x => new { x, match = Route.Match(x.Text[1..].Trim()) })
            .Where(x => x.match.Success)
            .GroupBy(x => x.x.File, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .FirstOrDefault();
        if (routes is null) return;
        var chosen = routes.GroupBy(x => x.match.Groups["verb"].Value + x.match.Groups["url"].Value,
                StringComparer.Ordinal)
            .Select(g => g.First()).Take(3).ToArray();
        if (chosen.Length == 0) return;
        var names = chosen.Select(x => x.match.Groups["verb"].Value.ToUpperInvariant() + " " +
                                        x.match.Groups["url"].Value);
        result.Add(new ProgressFeatureFact("API", "Thay đổi tuyến API",
            "Bổ sung khai báo " + string.Join(", ", names) +
            " trong endpoint production; đây là bằng chứng định tuyến, không chứng minh handler chạy đúng.",
            chosen.Select(x => x.x.Citation).ToArray()));
    }

    private static void AddModelIntegration(IReadOnlyList<SafeLine> lines, List<ProgressFeatureFact> result)
    {
        var modelFiles = lines.Where(x => Production(x) && x.File.Contains("/Services/", StringComparison.OrdinalIgnoreCase) &&
                                      (x.File.Contains("AiCodeReviewClient", StringComparison.OrdinalIgnoreCase) ||
                                       x.File.Contains("AiGitProgressClient", StringComparison.OrdinalIgnoreCase) ||
                                       x.File.Contains("Ollama", StringComparison.OrdinalIgnoreCase)))
            .GroupBy(x => x.File, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal);
        foreach (var file in modelFiles)
        {
            var api = file.FirstOrDefault(x => x.Text.Contains("api/generate", StringComparison.OrdinalIgnoreCase) ||
                                               x.Text.Contains("api/chat", StringComparison.OrdinalIgnoreCase));
            var send = file.FirstOrDefault(x => x.Text.Contains("SendAsync(", StringComparison.Ordinal) ||
                                                x.Text.Contains("PostAsync(", StringComparison.Ordinal));
            if (api is null || send is null) continue;
            result.Add(new ProgressFeatureFact("AI/Backend", "Kết nối dịch vụ phân tích AI",
                "Bổ sung mã gửi yêu cầu HTTP đến API mô hình từ " + FileName(file.Key) +
                "; cần chạy thử để xác nhận Ollama phản hồi hợp lệ.",
                Distinct([api.Citation, send.Citation], 3)));
            break;
        }
    }

    private static void AddWorker(IReadOnlyList<SafeLine> lines, List<ProgressFeatureFact> result)
    {
        var workerFiles = lines.Where(x => Production(x) && x.File.EndsWith("Worker.cs", StringComparison.OrdinalIgnoreCase))
            .GroupBy(x => x.File, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal);
        foreach (var file in workerFiles)
        {
            var dispatch = file.FirstOrDefault(x =>
                x.Text.Contains("AnalyzeAsync(", StringComparison.Ordinal) ||
                x.Text.Contains("GetRequiredService<Ai", StringComparison.Ordinal));
            var queue = file.FirstOrDefault(x =>
                x.Text.Contains("PENDING", StringComparison.Ordinal) ||
                x.Text.Contains("CodeAnalysisRuns", StringComparison.Ordinal) ||
                x.Text.Contains("GitProgressRuns", StringComparison.Ordinal));
            if (dispatch is null || queue is null) continue;
            result.Add(new ProgressFeatureFact("AI/Backend", "Luồng xử lý yêu cầu phân tích",
                "Bổ sung mã tra cứu công việc chờ xử lý và gọi thành phần phân tích trong " +
                FileName(file.Key) + "; chưa xác nhận worker hoàn tất công việc khi chạy.",
                Distinct([queue.Citation, dispatch.Citation], 3)));
            break;
        }
    }

    private static void AddFrontend(IReadOnlyList<SafeLine> lines, List<ProgressFeatureFact> result)
    {
        var ui = lines.Where(x => x.File.StartsWith("frontend/src/views/", StringComparison.OrdinalIgnoreCase) &&
                                  x.File.EndsWith(".vue", StringComparison.OrdinalIgnoreCase))
            .Select(x => new { x, match = UiAction.Match(x.Text[1..]) })
            .Where(x => x.match.Success)
            .GroupBy(x => x.x.File, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)
            .FirstOrDefault();
        var api = lines.FirstOrDefault(x => x.File.StartsWith("frontend/", StringComparison.OrdinalIgnoreCase) &&
                                            x.File.EndsWith(".api.ts", StringComparison.OrdinalIgnoreCase) &&
                                            (x.Text.Contains("request<", StringComparison.Ordinal) ||
                                             x.Text.Contains("request(", StringComparison.Ordinal)));
        var router = lines.FirstOrDefault(x => x.File.StartsWith("frontend/src/", StringComparison.OrdinalIgnoreCase) &&
                                               x.File.EndsWith("/router.ts", StringComparison.OrdinalIgnoreCase) &&
                                               x.Text.Contains("path:", StringComparison.Ordinal));
        if (ui is null && api is null && router is null) return;
        var citations = new List<ProgressFeatureCitation>();
        if (ui is not null) citations.Add(ui.First().x.Citation);
        if (api is not null) citations.Add(api.Citation);
        if (router is not null) citations.Add(router.Citation);
        var parts = new List<string>();
        if (ui is not null) parts.Add("giao diện có sự kiện hoặc liên kết dữ liệu");
        if (api is not null) parts.Add("lời gọi API phía frontend");
        if (router is not null) parts.Add("cấu hình điều hướng");
        result.Add(new ProgressFeatureFact("Frontend", "Tích hợp giao diện người dùng",
            "Bổ sung " + string.Join(", ", parts) +
            "; các dòng này chưa chứng minh thao tác trên giao diện đã hoạt động.", Distinct(citations, 3)));
    }

    private static void AddDatabase(IReadOnlyList<SafeLine> lines, List<ProgressFeatureFact> result)
    {
        var migrationFiles = lines.Where(x => Production(x) &&
            x.File.Contains("/Migrations/", StringComparison.OrdinalIgnoreCase))
            .GroupBy(x => x.File, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal);
        foreach (var file in migrationFiles)
        {
            var entries = file.ToArray();
            var names = new List<(string name, ProgressFeatureCitation citation)>();
            for (var i = 0; i < entries.Length; i++)
            {
                if (!entries[i].Text.Contains("migrationBuilder.CreateTable(", StringComparison.Ordinal)) continue;
                // The name must appear in this CreateTable or near it in the
                // SAME migration file, not in a later index or another file.
                var inline = TableName.Match(entries[i].Text[1..]);
                if (inline.Success)
                {
                    var inlineName = inline.Groups["name"].Value;
                    if (!names.Any(x => x.name == inlineName))
                        names.Add((inlineName, entries[i].Citation));
                    continue;
                }
                for (var j = i + 1; j < Math.Min(i + 9, entries.Length); j++)
                {
                    if (entries[j].Text.Contains("migrationBuilder.", StringComparison.Ordinal)) break;
                    var match = TableName.Match(entries[j].Text[1..]);
                    if (!match.Success) continue;
                    var name = match.Groups["name"].Value;
                    if (!names.Any(x => x.name == name))
                        names.Add((name, entries[j].Citation));
                    break;
                }
            }
            if (names.Count == 0) continue;
            var selected = names.Take(3).ToArray();
            result.Add(new ProgressFeatureFact("CSDL", "Thay đổi cấu trúc lưu trữ dữ liệu",
                "Migration bổ sung khai báo tạo " + selected.Length + " bảng: " +
                string.Join(", ", selected.Select(x => x.name)) +
                "; cần đối chiếu migration đã được áp dụng trên database hay chưa.",
                selected.Select(x => x.citation).ToArray()));
            break;
        }
    }

    // A helper (SeedAsync, Setup, DisposeAsync) is NOT a test case. Prefer a
    // behavior-named [Fact]/[Theory] method; those names are often available in
    // Git's added lines even when the attribute was unchanged and is absent.
    private static bool IsBehaviorTestMethod(string name)
    {
        string[] helpers = ["Seed", "SetUp", "Setup", "Initialize", "Cleanup", "TearDown",
            "Dispose", "Arrange", "Prepare", "Build", "CreateFixture", "Configure", "Reset"];
        if (helpers.Any(h => name.StartsWith(h, StringComparison.OrdinalIgnoreCase))) return false;
        if (name.Contains('_')) return true;
        string[] prefixes = ["Should", "When", "Given", "Can", "Cannot", "Rejects", "Reject",
            "Allows", "Allow", "Returns", "Return", "Prevents", "Prevent", "Fails",
            "Throws", "Validates", "Verify", "Verifies", "Tests", "Test"];
        return prefixes.Any(p => name.StartsWith(p, StringComparison.OrdinalIgnoreCase)) ||
            name.EndsWith("Test", StringComparison.OrdinalIgnoreCase);
    }

    private static void AddTests(IReadOnlyList<SafeLine> lines, List<ProgressFeatureFact> result)
    {
        var files = lines.Where(Testing).GroupBy(x => x.File, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal);
        var citations = new List<ProgressFeatureCitation>();
        var names = new List<string>();
        foreach (var file in files)
        {
            var entries = file.ToArray();
            var methods = entries.Select((line, index) => new
                {
                    line, index, match = TestMethod.Match(line.Text[1..])
                })
                .Where(x => x.match.Success).ToArray();
            var testCase = methods
                .FirstOrDefault(x => IsBehaviorTestMethod(x.match.Groups["name"].Value));
            if (testCase is null) continue; // No evidence of a changed behavioral test.
            var testName = testCase.match.Groups["name"].Value;
            citations.Add(testCase.line.Citation);
            names.Add(testName);
            // Associate request + HTTP assertion with the nearest preceding
            // ADDED behavior-named test method in the sampled diff. A diff alone
            // cannot prove the full original method boundaries or runtime behavior.
            // Lone Assert.Equal/PostAsJsonAsync snippets are not extra work items.
            var nextMethodIndex = methods.Where(m => m.index > testCase.index)
                .Select(m => m.index).DefaultIfEmpty(entries.Length).Min();
            var body = entries.Skip(testCase.index + 1)
                .Take(nextMethodIndex - testCase.index - 1).ToArray();
            var request = body.FirstOrDefault(x =>
                x.Text.Contains("PostAsJsonAsync(", StringComparison.Ordinal) ||
                x.Text.Contains("GetAsync(", StringComparison.Ordinal) ||
                x.Text.Contains("PutAsJsonAsync(", StringComparison.Ordinal) ||
                x.Text.Contains("DeleteAsync(", StringComparison.Ordinal));
            var assertion = body.FirstOrDefault(x =>
                x.Text.Contains("HttpStatusCode.", StringComparison.Ordinal) &&
                x.Text.Contains("Assert.", StringComparison.Ordinal) &&
                !GitProgressMeaningPolicy.IsLowValueTestSetupLine(x.File, x.Text))
                ?? body.FirstOrDefault(x =>
                    x.Text.Contains("Assert.", StringComparison.Ordinal) &&
                    !GitProgressMeaningPolicy.IsLowValueTestSetupLine(x.File, x.Text));
            if (request is not null) citations.Add(request.Citation);
            if (assertion is not null) citations.Add(assertion.Citation);
            if (names.Count >= 2 || citations.Count >= 6) break;
        }
        if (names.Count == 0) return;
        // A group may cite a request and its assertion, but must NEVER claim
        // they executed successfully. Only mention the status seen in a cited line.
        var requestKinds = citations.Where(c => c.Evidence.Contains("AsJsonAsync(", StringComparison.Ordinal) ||
                c.Evidence.Contains("GetAsync(", StringComparison.Ordinal) ||
                c.Evidence.Contains("DeleteAsync(", StringComparison.Ordinal))
            .Select(c => c.Evidence.Contains("PostAsJsonAsync(", StringComparison.Ordinal) ? "POST" :
                c.Evidence.Contains("PutAsJsonAsync(", StringComparison.Ordinal) ? "PUT" :
                c.Evidence.Contains("DeleteAsync(", StringComparison.Ordinal) ? "DELETE" : "GET")
            .Distinct().ToArray();
        var statusNames = new[] { "Created", "Unauthorized", "Forbidden", "BadRequest", "NotFound", "OK", "NoContent" }
            .Where(status => citations.Any(c => c.Evidence.Contains("HttpStatusCode." + status, StringComparison.Ordinal)))
            .ToArray();
        var detail = requestKinds.Length > 0 && statusNames.Length > 0
            ? $"; có dòng gửi yêu cầu {string.Join("/", requestKinds)} và assertion đối chiếu HTTP {string.Join("/", statusNames)}"
            : "";
        result.Add(new ProgressFeatureFact("Kiểm thử", "Bổ sung kiểm thử tự động",
            "Bổ sung mã kiểm thử hành vi, trong đó có các phương thức " + string.Join(", ", names) +
            detail + "; đây là thay đổi của test, không phải bằng chứng API production mới hay xác nhận kiểm thử đã chạy thành công.",
            Distinct(citations, 6)));
    }

    private static string FileName(string path) => path.Split('/')[^1];
    private static ProgressFeatureCitation[] Distinct(IEnumerable<ProgressFeatureCitation> items, int max) =>
        items.Distinct().Take(max).ToArray();
}
