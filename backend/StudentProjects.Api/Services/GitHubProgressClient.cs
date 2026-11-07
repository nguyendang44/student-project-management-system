using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace StudentProjects.Api.Services;

/// <summary>Public GitHub compare reader; never connects to a user-supplied hostname.</summary>
public sealed class GitHubProgressClient(HttpClient http, IConfiguration config)
{
    private const int MaxJsonBytes = 2_500_000;

    public async Task<ProgressDiff> CompareAsync(string owner, string repo, string baseSha, string headSha, CancellationToken ct)
    {
        if (!GitProgressEvidencePolicy.IsSha(baseSha) || !GitProgressEvidencePolicy.IsSha(headSha))
            throw new AnalysisInputException("Cần hai SHA Git đầy đủ, mỗi SHA gồm 40 ký tự hex.");
        if (string.Equals(baseSha, headSha, StringComparison.OrdinalIgnoreCase))
            throw new AnalysisInputException("Hai commit phải khác nhau.");
        var path = $"repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repo)}/compare/{baseSha}...{headSha}";
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("StudentProjects", "0.11"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        if (!string.IsNullOrWhiteSpace(config["GitHub:Token"]))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config["GitHub:Token"]);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new AnalysisInputException("Không tìm thấy commit hoặc repository public trên GitHub.");
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
            throw new AnalysisInputException("GitHub giới hạn API; kiểm tra GitHub:Token hoặc thử lại sau.");
        if (!response.IsSuccessStatusCode)
            throw new AnalysisInputException($"GitHub Compare API trả về HTTP {(int)response.StatusCode}.");
        if (response.Content.Headers.ContentLength > MaxJsonBytes)
            throw new AnalysisInputException("Git diff quá lớn để phân tích an toàn; hãy chọn hai commit gần nhau hơn.");
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[16_384];
        int count;
        while ((count = await stream.ReadAsync(chunk, ct)) != 0)
        {
            if (buffer.Length + count > MaxJsonBytes)
                throw new AnalysisInputException("Git diff vượt giới hạn; hãy chọn hai commit gần nhau hơn.");
            buffer.Write(chunk, 0, count);
        }
        buffer.Position = 0;
        using var document = await JsonDocument.ParseAsync(buffer, cancellationToken: ct);
        var root = document.RootElement;
        var status = root.GetProperty("status").GetString() ?? "";
        var ahead = root.GetProperty("ahead_by").GetInt32();
        if (status != "ahead" || ahead <= 0)
            throw new AnalysisInputException("Commit mới phải là hậu duệ của commit cũ. Hai nhánh phân kỳ hoặc chọn sai thứ tự.");
        var files = root.GetProperty("files");
        if (files.ValueKind != JsonValueKind.Array)
            throw new AnalysisInputException("GitHub không trả danh sách file thay đổi.");
        var parsed = new List<ProgressFile>();
        var rawAdditions = 0;
        var rawDeletions = 0;
        foreach (var item in files.EnumerateArray())
        {
            var name = item.GetProperty("filename").GetString() ?? "";
            var kind = item.GetProperty("status").GetString() ?? "modified";
            var added = item.TryGetProperty("additions", out var a) ? a.GetInt32() : 0;
            var removed = item.TryGetProperty("deletions", out var d) ? d.GetInt32() : 0;
            // Git stats must include every file returned by GitHub, including backups.
            rawAdditions = checked(rawAdditions + added);
            rawDeletions = checked(rawDeletions + removed);
            // Unsafe/archived paths may affect Git totals but must not reach AI or filesJson.
            if (!GitProgressEvidencePolicy.SafePath(name)) continue;
            var patch = item.TryGetProperty("patch", out var p) && p.ValueKind == JsonValueKind.String
                ? p.GetString() : null;
            parsed.Add(new ProgressFile(name, kind, added, removed, patch));
        }
        // Only API response caps imply possible incompleteness. Screening is NOT truncation.
        return new ProgressDiff(baseSha.ToLowerInvariant(), headSha.ToLowerInvariant(), ahead,
            parsed, files.GetArrayLength() >= 300 || ahead > 100,
            files.GetArrayLength(), rawAdditions, rawDeletions);
    }
}
