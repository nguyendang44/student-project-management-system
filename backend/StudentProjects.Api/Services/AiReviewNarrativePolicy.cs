namespace StudentProjects.Api.Services;

/// <summary>
/// Never republishes model-authored security allegations in free-text summary
/// fields. Those fields have no line-level citations. Build a factual account
/// from the actual prompt paths and the already-screened findings instead.
/// </summary>
public static class AiReviewNarrativePolicy
{
    public static AiReport Compose(string feature, IEnumerable<string> sentPaths, AiReport screened)
    {
        var paths = sentPaths.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var findings = screened.Findings ?? new List<AiFinding>();
        var summary = findings.Count == 0
            ? $"{feature}: đã gửi đoạn mã của {paths.Length} file. Không có phát hiện vượt qua kiểm tra bằng chứng trong phạm vi này; không thể kết luận mã nguồn an toàn."
            : $"{feature}: đã gửi đoạn mã của {paths.Length} file. Có {findings.Count} nhận xét có vị trí và trích dẫn khớp source; tất cả vẫn cần giảng viên xác minh.";

        var names = paths.Select(p => p.Replace('\\', '/').Split('/').Last()).ToArray();
        var work = $"Các file tham khảo: {string.Join(", ", names)}. " +
            "Không suy luận lỗ hổng, quyền truy cập hoặc kết quả chạy thử từ những dòng chưa được cung cấp.";
        return screened with { Summary = summary, DevelopmentWork = work };
    }
}
