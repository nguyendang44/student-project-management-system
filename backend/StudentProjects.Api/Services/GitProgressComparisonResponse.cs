using System.Text.Json;

namespace StudentProjects.Api.Services;

/// <summary>Bound transport metadata separately from the generated comparison JSON.</summary>
public static class GitProgressComparisonResponse
{
    public const int MaxEnvelopeBytes = 2 * 1024 * 1024;
    public const int MaxGeneratedCharacters = 32_000;
    public const string EnvelopeTooLarge = "Gói phản hồi Ollama vượt giới hạn nhận dữ liệu.";
    public const string GeneratedTooLarge = "Nội dung kết quả đối chiếu từ AI quá dài.";

    public static async Task<string> ReadGeneratedAsync(HttpContent content, CancellationToken ct)
    {
        if (content.Headers.ContentLength is long length && length > MaxEnvelopeBytes)
            throw new InvalidDataException(EnvelopeTooLarge);
        await using var input = await content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        while (true)
        {
            var count = await input.ReadAsync(chunk.AsMemory(), ct);
            if (count == 0) break;
            if (buffer.Length + count > MaxEnvelopeBytes) throw new InvalidDataException(EnvelopeTooLarge);
            buffer.Write(chunk, 0, count);
        }
        using var envelope = JsonDocument.Parse(buffer.GetBuffer().AsMemory(0, checked((int)buffer.Length)));
        var root = envelope.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("response", out var response) || response.ValueKind != JsonValueKind.String ||
            (root.TryGetProperty("done", out var done) && done.ValueKind != JsonValueKind.True) ||
            (root.TryGetProperty("done_reason", out var reason) && reason.ValueKind == JsonValueKind.String && reason.GetString() == "length"))
            throw new InvalidDataException("Ollama chưa hoàn thành lượt đối chiếu.");
        var generated = response.GetString()!;
        if (generated.Length > MaxGeneratedCharacters) throw new InvalidDataException(GeneratedTooLarge);
        if (string.IsNullOrWhiteSpace(generated)) throw new InvalidDataException("Ollama chưa trả kết quả đối chiếu.");
        return generated;
    }
}
