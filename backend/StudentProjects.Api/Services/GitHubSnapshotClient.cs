using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace StudentProjects.Api.Services;

public sealed record SourceFile(string Path, string Content)
{
    public string[] Lines => Content.Replace("\r\n", "\n").Split('\n');
}

public sealed record RepositorySnapshot(string CommitSha, IReadOnlyList<SourceFile> Files, int SkippedFiles);

// Dedicated GitHub snapshot reader. The remote host is fixed in Program.cs; never follow user URLs.
public sealed class GitHubSnapshotClient(HttpClient http, IConfiguration config)
{
    private const int ResponseLimit = 2_000_000;
    private const int FileLimit = 12_000;
    private const int MaxFiles = 12;
    private static readonly Regex ValidSha = new("^[0-9a-fA-F]{40}$", RegexOptions.Compiled);
    private static readonly Regex SuspiciousSecret = new(
        @"(?i)(-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----|gh[pousr]_[a-z0-9_]{25,}|sk-[a-z0-9_-]{20,}|(?:api[_-]?key|client[_-]?secret|password|access[_-]?token)\s*[:=]\s*['""][^'""]{12,}['""])",
        RegexOptions.Compiled);

    public async Task<string> ResolveCommitAsync(string owner, string repo, string branch, CancellationToken ct)
    {
        if (!ValidBranch(branch)) throw new AnalysisInputException("Branch không hợp lệ.");
        using var doc = await GetAsync($"repos/{Esc(owner)}/{Esc(repo)}/commits/{Esc(branch)}", ct);
        var sha = doc.RootElement.GetProperty("sha").GetString() ?? "";
        if (!ValidSha.IsMatch(sha)) throw new AnalysisInputException("GitHub trả về SHA không hợp lệ.");
        return sha.ToLowerInvariant();
    }

    public async Task EnsurePublicAsync(string owner, string repo, CancellationToken ct)
    {
        using var doc = await GetAsync($"repos/{Esc(owner)}/{Esc(repo)}", ct);
        if (!doc.RootElement.TryGetProperty("private", out var p) || p.ValueKind != JsonValueKind.False)
            throw new AnalysisInputException("v0.11 chỉ hỗ trợ repository public.");
    }

    public async Task<RepositorySnapshot> ReadAtCommitAsync(string owner, string repo, string sha, CancellationToken ct)
    {
        if (!ValidSha.IsMatch(sha)) throw new AnalysisInputException("SHA không hợp lệ.");
        // A commit SHA is not necessarily a tree SHA. Resolve the tree explicitly.
        using var commitDoc = await GetAsync($"repos/{Esc(owner)}/{Esc(repo)}/git/commits/{sha}", ct);
        var treeSha = commitDoc.RootElement.GetProperty("tree").GetProperty("sha").GetString() ?? "";
        if (!ValidSha.IsMatch(treeSha)) throw new AnalysisInputException("GitHub trả về tree SHA không hợp lệ.");
        using var treeDoc = await GetAsync($"repos/{Esc(owner)}/{Esc(repo)}/git/trees/{treeSha}?recursive=1", ct);
        var root = treeDoc.RootElement;
        if (root.TryGetProperty("truncated", out var truncated) && truncated.GetBoolean())
            throw new AnalysisInputException("Cây thư mục GitHub quá lớn; chưa hỗ trợ phân tích repository này.");
        if (!root.TryGetProperty("tree", out var tree) || tree.ValueKind != JsonValueKind.Array)
            throw new AnalysisInputException("Không đọc được cây thư mục GitHub.");

        var candidates = new List<(string Path, string Sha, long Size)>();
        foreach (var item in tree.EnumerateArray())
        {
            if (item.GetProperty("type").GetString() != "blob" || item.GetProperty("mode").GetString() == "120000") continue;
            var path = item.GetProperty("path").GetString() ?? "";
            var blobSha = item.GetProperty("sha").GetString() ?? "";
            var size = item.TryGetProperty("size", out var s) ? s.GetInt64() : 0;
            if (AllowedFile(path) && size > 0 && size <= FileLimit && ValidSha.IsMatch(blobSha))
                candidates.Add((path, blobSha, size));
        }
        // Choose related files (API, service, contracts, frontend, tests, shared context)
        // from the same bounded, size/secret-filtered candidate list.
        var selectedPaths = AiRelatedFileSelector.SelectPaths(candidates.Select(c => c.Path), MaxFiles);
        var candidateMap = candidates.ToDictionary(c => c.Path, StringComparer.Ordinal);
        var selected = selectedPaths.Select(p => candidateMap[p]).ToArray();
        var files = new List<SourceFile>();
        var skipped = candidates.Count - selected.Length;
        foreach (var item in selected)
        {
            using var doc = await GetAsync($"repos/{Esc(owner)}/{Esc(repo)}/git/blobs/{item.Sha}", ct);
            var blob = doc.RootElement;
            if (blob.GetProperty("encoding").GetString() != "base64") { skipped++; continue; }
            string source;
            try
            {
                var bytes = Convert.FromBase64String(blob.GetProperty("content").GetString() ?? "");
                if (bytes.Length > FileLimit) { skipped++; continue; }
                source = new UTF8Encoding(false, true).GetString(bytes);
            }
            catch (Exception e) when (e is FormatException or DecoderFallbackException) { skipped++; continue; }
            if (SuspiciousSecret.IsMatch(source) || source.Contains('\0')) { skipped++; continue; }
            files.Add(new SourceFile(item.Path, source));
        }
        if (files.Count == 0) throw new AnalysisInputException("Không có file phù hợp để phân tích trong giới hạn v0.11.");
        return new RepositorySnapshot(sha, files, skipped);
    }

    public static bool ValidBranch(string branch) =>
        !string.IsNullOrWhiteSpace(branch) && branch.Length <= 200 &&
        Regex.IsMatch(branch, "^[A-Za-z0-9_./-]+$") && !branch.Contains("..") &&
        !branch.Contains("//") && !branch.StartsWith('/') && !branch.EndsWith('/');

    private static string Esc(string value) => Uri.EscapeDataString(value);
    private static int Rank(string path)
    {
        var p = path.ToLowerInvariant();
        if (p.Contains("/endpoints/") || p.Contains("/services/") || p.Contains("/controllers/")) return 0;
        if (p.Contains("/security/") || p.Contains("/auth/")) return 1;
        if (p.Contains("/tests/") || p.Contains(".test.") || p.Contains(".spec.")) return 2;
        return 3;
    }
    private static bool AllowedFile(string path)
    {
        var p = path.Replace('\\', '/').ToLowerInvariant();
        var segments = p.Split('/');
        if (segments.Any(s => s is ".git" or "bin" or "obj" or "node_modules" or "dist" or "build" or "coverage" or "vendor" or "migrations")) return false;
        if (segments.Any(s => s.StartsWith('.') || s.Contains("secret") || s.Contains("credential") || s.Contains("private") || s.Contains("key"))) return false;
        if (p.EndsWith(".min.js") || p.EndsWith(".generated.cs") || p.EndsWith(".g.cs")) return false;
        return new[] { ".cs", ".ts", ".tsx", ".js", ".jsx", ".vue", ".py", ".java", ".go" }.Any(ext => p.EndsWith(ext, StringComparison.Ordinal));
    }

    private async Task<JsonDocument> GetAsync(string path, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, path);
        req.Headers.UserAgent.Add(new ProductInfoHeaderValue("StudentProjects", "0.11"));
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        req.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        var token = config["GitHub:Token"];
        if (!string.IsNullOrWhiteSpace(token)) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) throw new AnalysisInputException("Không tìm thấy branch hoặc repository public.");
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
            throw new AnalysisInputException("GitHub rate limit; thử lại sau.");
        if (!response.IsSuccessStatusCode) throw new AnalysisInputException($"GitHub HTTP {(int)response.StatusCode}.");
        if (response.Content.Headers.ContentLength > ResponseLimit) throw new AnalysisInputException("Phản hồi GitHub vượt giới hạn.");
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var memory = new MemoryStream();
        var buffer = new byte[16384];
        int total = 0, n;
        while ((n = await stream.ReadAsync(buffer, ct)) > 0)
        {
            total += n;
            if (total > ResponseLimit) throw new AnalysisInputException("Phản hồi GitHub vượt giới hạn.");
            memory.Write(buffer, 0, n);
        }
        memory.Position = 0;
        return await JsonDocument.ParseAsync(memory, cancellationToken: ct);
    }
}
public sealed class AnalysisInputException(string message) : Exception(message) { }
