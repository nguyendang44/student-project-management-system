using System.Text.Json;

namespace StudentProjects.Api.Services;

/// <summary>Aggregate counters only. Do not persist raw model output, source or credentials.</summary>
public sealed record ProgressFeatureExplanationResult(
    IReadOnlyList<ProgressFeatureExplanation> Items, int RawItems, int ParsedItems);

public sealed record ProgressReportDiagnostics(
    int SentFiles,
    int ModelItems,
    int ParsedCandidates,
    int Verified,
    int RuleFallbackItems,
    GitProgressMeaningPolicy.RejectionDiagnostics Rejections,
    int FeatureGroups,
    int ExplanationModelItems,
    int ExplanationCandidates,
    int ExplanationAccepted,
    string ExplanationStatus,
    string? ContextStatus = null,
    int ContextExcerpts = 0,
    int ContextFiles = 0);

/// <summary>
/// A private metadata element in the existing EvidenceJson array avoids a DB migration.
/// The authenticated GET endpoint strips it from evidenceJson and returns it separately.
/// Legacy reports remain readable; their diagnostics are explicitly unavailable.
/// </summary>
public static class GitProgressDiagnostics
{
    private const string Source = "__git-progress-diagnostics-v1";

    public static string Write(IReadOnlyList<object> evidence, ProgressReportDiagnostics diagnostics)
    {
        var rows = new List<object>(evidence) { new { source = Source, diagnostics } };
        return JsonSerializer.Serialize(rows);
    }

    public static ProgressReportDiagnostics? Read(string? persisted)
    {
        if (!TryReadMetadata(persisted, out var metadata)) return null;
        try
        {
            return JsonSerializer.Deserialize<ProgressReportDiagnostics>(metadata.GetRawText(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException) { return null; }
    }

    // Preserve the old JSON bytes when metadata is absent, including unknown historical shapes.
    public static string PublicEvidence(string? persisted)
    {
        var raw = persisted ?? "[]";
        try
        {
            using var document = JsonDocument.Parse(raw);
            if (document.RootElement.ValueKind != JsonValueKind.Array) return raw;
            if (!document.RootElement.EnumerateArray().Any(IsMetadata)) return raw;
            return JsonSerializer.Serialize(document.RootElement.EnumerateArray()
                .Where(e => !IsMetadata(e)).ToArray());
        }
        catch (JsonException) { return raw; }
    }

    private static bool TryReadMetadata(string? raw, out JsonElement metadata)
    {
        metadata = default;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        try
        {
            using var document = JsonDocument.Parse(raw);
            if (document.RootElement.ValueKind != JsonValueKind.Array) return false;
            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (IsMetadata(element) && element.TryGetProperty("diagnostics", out var value)
                    && value.ValueKind == JsonValueKind.Object)
                {
                    metadata = value.Clone();
                    return true;
                }
            }
        }
        catch (JsonException) { /* historical malformed payload has no diagnostics */ }
        return false;
    }

    private static bool IsMetadata(JsonElement value) =>
        value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty("source", out var source) &&
        source.ValueKind == JsonValueKind.String &&
        source.GetString() == Source;
}
