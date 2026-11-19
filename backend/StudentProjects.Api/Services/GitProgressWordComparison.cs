using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace StudentProjects.Api.Services;

public sealed record WordReportLine(int Id, string Text);
public sealed record WordEvidence(int Id, string Title, string File, string Evidence);
public sealed record WordComparisonRow(int Id, string Claim, string Status, IReadOnlyList<WordEvidence> Evidence);
public sealed record WordComparisonResult(decimal Score, int Full, int Partial, int Missing,
    IReadOnlyList<WordComparisonRow> Items);

public static partial class GitProgressWordComparison
{
    public const int MaxFileBytes = 2 * 1024 * 1024;
    private static readonly XNamespace Word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    public static IReadOnlyList<WordReportLine> ReadClaims(Stream input)
    {
        using var zip = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        var part = zip.GetEntry("word/document.xml");
        if (part is null || part.Length > 4 * 1024 * 1024)
            throw new InvalidDataException("Không đọc được nội dung Word hoặc nội dung vượt giới hạn.");
        using var source = part.Open();
        using var reader = XmlReader.Create(source, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4 * 1024 * 1024
        });
        var document = XDocument.Load(reader);
        var body = document.Root?.Element(Word + "body")
            ?? throw new InvalidDataException("File Word không có nội dung văn bản.");
        var lines = new List<(string Text, bool Heading)>();
        foreach (var element in body.Elements())
        {
            if (element.Name == Word + "p")
            {
                var style = (string?)element.Element(Word + "pPr")?.Element(Word + "pStyle")?.Attribute(Word + "val");
                lines.Add((Text(element), style is "Title" or "Subtitle" ||
                    (style?.StartsWith("Heading", StringComparison.OrdinalIgnoreCase) ?? false)));
            }
            else if (element.Name == Word + "tbl")
            {
                lines.AddRange(element.Elements(Word + "tr").Where(row =>
                    row.Element(Word + "trPr")?.Element(Word + "tblHeader") is null)
                    .Select(row => (string.Join(" | ", row.Elements(Word + "tc").Select(cell =>
                        string.Join(" ", cell.Descendants(Word + "p").Select(Text))).Where(s => s.Length > 0)), false)));
            }
        }

        // A work section lets the author keep context and plans outside the denominator.
        var start = lines.FindIndex(line => line.Text.TrimEnd(':').Equals("Công việc đã thực hiện",
            StringComparison.OrdinalIgnoreCase));
        var claims = (start >= 0 ? lines.Skip(start + 1).TakeWhile(line => !line.Heading) : lines)
            .Where(line => !line.Heading && line.Text.Length > 0).Select(line => line.Text)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (claims.Length == 0)
            throw new InvalidDataException("Không tìm thấy công việc dạng văn bản trong file Word.");
        if (claims.Length > 20 || claims.Any(claim => claim.Length > 500))
            throw new InvalidDataException("Dùng tối đa 20 công việc, mỗi đoạn văn hoặc dòng bảng tối đa 500 ký tự.");
        return claims.Select((claim, index) => new WordReportLine(index + 1, claim)).ToArray();

        static string Text(XElement element) => string.Concat(element.Descendants(Word + "t")
            .Where(t => !t.Ancestors(Word + "drawing").Any() && !t.Ancestors(Word + "pict").Any())
            .Select(t => t.Value)).Trim();
    }

    public static IReadOnlyList<WordEvidence> ReadEvidence(string evidenceJson)
    {
        using var doc = JsonDocument.Parse(GitProgressDiagnostics.PublicEvidence(evidenceJson));
        var rows = new List<WordEvidence>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !item.TryGetProperty("title", out var title) || title.ValueKind != JsonValueKind.String ||
                !item.TryGetProperty("citations", out var citations) || citations.ValueKind != JsonValueKind.Array)
                continue;
            var candidates = new List<(string File, string Line)>();
            foreach (var citation in citations.EnumerateArray())
            {
                if (citation.ValueKind != JsonValueKind.Object ||
                    !citation.TryGetProperty("file", out var file) || file.ValueKind != JsonValueKind.String ||
                    !citation.TryGetProperty("evidence", out var line) || line.ValueKind != JsonValueKind.String)
                    continue;
                var path = file.GetString()!;
                var code = line.GetString()!;
                if (GitProgressEvidencePolicy.SafePath(path) && GitProgressEvidencePolicy.IsAiSafeDiffLine(code))
                    candidates.Add((path, code));
            }
            var distinct = candidates.Distinct().ToArray();
            // Preserve both route mappings for grouped work and represent each cited file.
            var representative = distinct.Where(x => x.Line.Contains(".Map", StringComparison.Ordinal))
                .Concat(distinct.GroupBy(x => x.File, StringComparer.OrdinalIgnoreCase).Select(file => file.First()))
                .Concat(distinct.Where(x => x.Line.Contains("@submit", StringComparison.Ordinal) ||
                    x.Line.Contains("@click", StringComparison.Ordinal) ||
                    x.Line.Contains("await githubApi.", StringComparison.Ordinal)))
                .Concat(distinct.Where(x => x.Line.Contains("await ", StringComparison.Ordinal)))
                .Concat(distinct).Distinct().Take(10);
            foreach (var (path, line) in representative)
                rows.Add(new WordEvidence(rows.Count + 1, title.GetString()!, path, line));
        }
        if (rows.Sum(row => row.Title.Length + row.File.Length + row.Evidence.Length) > 16_000)
            throw new InvalidDataException("Báo cáo có nhiều bằng chứng. Hãy đối chiếu với một khoảng commit nhỏ hơn.");
        return rows;
    }

    public static IReadOnlyList<WordComparisonRow> ValidateBatch(JsonElement answer,
        IReadOnlyList<WordReportLine> claims, IReadOnlyList<WordEvidence> evidence)
    {
        if (answer.ValueKind != JsonValueKind.Object || !answer.TryGetProperty("items", out var items) ||
            items.ValueKind != JsonValueKind.Array || items.GetArrayLength() != claims.Count)
            throw new InvalidDataException("Ollama chưa trả đủ kết quả để tính tỷ lệ.");
        var byId = claims.ToDictionary(x => x.Id);
        var citations = evidence.ToDictionary(x => x.Id);
        var seen = new HashSet<int>();
        var result = new List<WordComparisonRow>();
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("id", out var id) ||
                id.ValueKind != JsonValueKind.Number || !id.TryGetInt32(out var key) ||
                !byId.TryGetValue(key, out var claim) || !seen.Add(key) ||
                !item.TryGetProperty("status", out var statusValue) || statusValue.ValueKind != JsonValueKind.String ||
                !item.TryGetProperty("evidenceIds", out var ids) || ids.ValueKind != JsonValueKind.Array ||
                ids.GetArrayLength() > 4)
                throw new InvalidDataException("Kết quả đối chiếu từ Ollama sai cấu trúc.");
            var status = statusValue.GetString()!;
            if (status is not ("full" or "partial" or "missing"))
                throw new InvalidDataException("Ollama trả mức đối chiếu không hợp lệ.");
            var selected = new List<WordEvidence>();
            foreach (var reference in ids.EnumerateArray())
            {
                if (reference.ValueKind != JsonValueKind.Number || !reference.TryGetInt32(out var citationId) ||
                    !citations.TryGetValue(citationId, out var citation))
                    throw new InvalidDataException("Ollama dẫn bằng chứng ngoài báo cáo Git. Vui lòng thử lại.");
                if (!selected.Contains(citation)) selected.Add(citation);
            }
            if (status != "missing" && selected.Count == 0)
                throw new InvalidDataException("Ollama chưa dẫn bằng chứng cho một công việc. Vui lòng thử lại.");
            if (status == "full" && RuntimeClaim().IsMatch(claim.Text)) status = "partial";
            result.Add(new WordComparisonRow(key, claim.Text, status, status == "missing" ? [] : selected));
        }
        return result.OrderBy(x => x.Id).ToArray();
    }

    public static WordComparisonResult Score(IReadOnlyList<WordComparisonRow> items)
    {
        var full = items.Count(row => row.Status == "full");
        var partial = items.Count(row => row.Status == "partial");
        return new WordComparisonResult(Math.Round((full + partial * 0.5m) * 100 / items.Count, 1),
            full, partial, items.Count - full - partial, items);
    }

    [GeneratedRegex(@"(?i)\b(?:pass(?:ed)?|chạy\s+(?:đạt|đúng|thành công)|kiểm thử\s+(?:đạt|thành công)|triển khai\s+thành công|đã deploy|đã đưa lên production)\b")]
    private static partial Regex RuntimeClaim();
}
