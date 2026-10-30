using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;

namespace StudentProjects.Api.Services;

// v0.10: GitHub public repository reader. The user-supplied URL is never fetched directly.
// Only the fixed api.github.com host is contacted to avoid SSRF and token exposure.
public sealed record GitHubRepositoryInfo(
    string RepositoryUrl, string FullName, string DefaultBranch,
    string? Description, DateTimeOffset? PushedAt, bool Archived);
public sealed record GitHubCommitInfo(string Sha, string Message, string Author,
    DateTimeOffset? CommittedAt, string HtmlUrl);

public sealed class GitHubAccessException(int httpStatus, string code, string message) : Exception(message)
{
    public int HttpStatus { get; } = httpStatus;
    public string Code { get; } = code;
}

public interface IGitHubPublicClient
{
    Task<GitHubRepositoryInfo> GetRepositoryAsync(string owner, string repo, CancellationToken ct);
    Task<IReadOnlyList<GitHubCommitInfo>> GetCommitsAsync(string owner, string repo, CancellationToken ct);
    Task<IReadOnlyList<string>> GetBranchesAsync(string owner, string repo, CancellationToken ct);
}

public sealed class GitHubPublicClient(HttpClient http, IMemoryCache cache) : IGitHubPublicClient
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(90);

    public async Task<GitHubRepositoryInfo> GetRepositoryAsync(string owner, string repo, CancellationToken ct)
    {
        var cacheKey = $"v010:repo:{owner.ToLowerInvariant()}/{repo.ToLowerInvariant()}";
        if (cache.TryGetValue<GitHubRepositoryInfo>(cacheKey, out var cached) && cached is not null)
            return cached;
        using var document = await GetAsync($"repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repo)}", ct);
        var root = document.RootElement;
        if (!root.TryGetProperty("private", out var privateValue) || privateValue.ValueKind != JsonValueKind.False)
            throw new GitHubAccessException(422, "PRIVATE_REPOSITORY", "v0.10 chỉ hỗ trợ repository public. Repository private sẽ cần GitHub OAuth.");
        var fullName = root.GetProperty("full_name").GetString() ?? $"{owner}/{repo}";
        var branch = root.GetProperty("default_branch").GetString() ?? "";
        var description = root.TryGetProperty("description", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() : null;
        DateTimeOffset? pushed = Date(root, "pushed_at");
        var archived = root.TryGetProperty("archived", out var a) && a.ValueKind == JsonValueKind.True;
        var result = new GitHubRepositoryInfo($"https://github.com/{fullName}", fullName, branch, description, pushed, archived);
        cache.Set(cacheKey, result, CacheLifetime);
        return result;
    }

    public async Task<IReadOnlyList<GitHubCommitInfo>> GetCommitsAsync(string owner, string repo, CancellationToken ct)
    {
        var cacheKey = $"v010:commits:{owner.ToLowerInvariant()}/{repo.ToLowerInvariant()}";
        if (cache.TryGetValue<IReadOnlyList<GitHubCommitInfo>>(cacheKey, out var cached) && cached is not null)
            return cached;
        JsonDocument document;
        try { document = await GetAsync($"repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repo)}/commits?per_page=20", ct); }
        catch (GitHubAccessException e) when (e.Code == "EMPTY_REPOSITORY")
        {
            return Array.Empty<GitHubCommitInfo>();
        }
        using (document)
        {
            var commits = new List<GitHubCommitInfo>();
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                throw new GitHubAccessException(502, "GITHUB_INVALID_RESPONSE", "GitHub trả về danh sách commit không hợp lệ.");
            foreach (var item in document.RootElement.EnumerateArray())
            {
                var sha = item.GetProperty("sha").GetString() ?? "";
                var commit = item.GetProperty("commit");
                var message = commit.GetProperty("message").GetString() ?? "";
                if (message.Length > 1000) message = message[..1000];
                var author = commit.TryGetProperty("author", out var authorObj) && authorObj.ValueKind == JsonValueKind.Object ? authorObj : default;
                var name = author.ValueKind == JsonValueKind.Object && author.TryGetProperty("name", out var nm) ? nm.GetString() ?? "" : "";
                var date = author.ValueKind == JsonValueKind.Object ? Date(author, "date") : null;
                var url = item.TryGetProperty("html_url", out var h) ? h.GetString() ?? "" : "";
                commits.Add(new GitHubCommitInfo(sha, message, name, date, url));
            }
            cache.Set(cacheKey, (IReadOnlyList<GitHubCommitInfo>)commits, CacheLifetime);
            return commits;
        }
    }

    public async Task<IReadOnlyList<string>> GetBranchesAsync(string owner, string repo, CancellationToken ct)
    {
        var cacheKey = $"v010:branches:{owner.ToLowerInvariant()}/{repo.ToLowerInvariant()}";
        if (cache.TryGetValue<IReadOnlyList<string>>(cacheKey, out var cached) && cached is not null)
            return cached;
        using var document = await GetAsync($"repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repo)}/branches?per_page=20", ct);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new GitHubAccessException(502, "GITHUB_INVALID_RESPONSE", "GitHub trả về danh sách branch không hợp lệ.");
        var branches = document.RootElement.EnumerateArray().Select(x => x.GetProperty("name").GetString() ?? "")
            .Where(name => name.Length > 0).ToArray();
        cache.Set(cacheKey, (IReadOnlyList<string>)branches, CacheLifetime);
        return branches;
    }

    private static DateTimeOffset? Date(JsonElement element, string key)
    {
        if (element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(value.GetString(), out var parsed)) return parsed;
        return null;
    }

    private async Task<JsonDocument> GetAsync(string relativePath, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, relativePath);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("StudentProjects", "0.10"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
                throw new GitHubAccessException(422, "REPOSITORY_UNAVAILABLE", "Không tìm thấy repository public. Kiểm tra URL hoặc quyền riêng tư trên GitHub.");
            if (response.StatusCode == HttpStatusCode.Conflict)
                throw new GitHubAccessException(409, "EMPTY_REPOSITORY", "Repository chưa có commit.");
            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
                throw new GitHubAccessException(429, "GITHUB_RATE_LIMIT", "GitHub đang giới hạn số lượt truy cập. Hãy thử lại sau.");
            if (!response.IsSuccessStatusCode)
                throw new GitHubAccessException(502, "GITHUB_UNAVAILABLE", "Không truy cập được GitHub ở thời điểm hiện tại.");
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        }
        catch (GitHubAccessException) { throw; }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new GitHubAccessException(504, "GITHUB_TIMEOUT", "Kết nối GitHub quá thời gian cho phép.");
        }
        catch (HttpRequestException)
        {
            throw new GitHubAccessException(502, "GITHUB_NETWORK", "Không kết nối được đến GitHub.");
        }
        catch (JsonException)
        {
            throw new GitHubAccessException(502, "GITHUB_INVALID_RESPONSE", "GitHub trả về dữ liệu không hợp lệ.");
        }
    }
}

public static class GitHubUrl
{
    private static readonly Regex Owner = new(@"^[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,37}[a-zA-Z0-9])?$", RegexOptions.CultureInvariant);
    private static readonly Regex Repo = new(@"^[a-zA-Z0-9_.-]{1,100}$", RegexOptions.CultureInvariant);
    public static bool TryParse(string? input, out string owner, out string repo)
    {
        owner = repo = "";
        if (string.IsNullOrWhiteSpace(input) || input.Length > 250 ||
            !Uri.TryCreate(input.Trim(), UriKind.Absolute, out var url) ||
            url.Scheme != Uri.UriSchemeHttps || !string.Equals(url.Host, "github.com", StringComparison.OrdinalIgnoreCase) ||
            !url.IsDefaultPort || url.UserInfo.Length > 0 || url.Query.Length > 0 || url.Fragment.Length > 0)
            return false;
        var parts = url.AbsolutePath.Trim('/').Split('/');
        if (parts.Length != 2 || !Owner.IsMatch(parts[0]) || !Repo.IsMatch(parts[1])) return false;
        owner = parts[0];
        repo = parts[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? parts[1][..^4] : parts[1];
        if (!Repo.IsMatch(repo)) return false;
        return true;
    }
}
