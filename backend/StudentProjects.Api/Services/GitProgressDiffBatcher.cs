using System.Text.Json;

namespace StudentProjects.Api.Services;

/// <summary>
/// Splits all available, safe text patches into small per-file prompts for a 7B model.
/// A file is fully covered only if every planned chunk actually received an Ollama reply.
/// GitHub can omit a patch (binary/large file); this is reported, never called complete.
/// </summary>
public static class GitProgressDiffBatcher
{
    public const int MaxBatchCharacters = 5_200;
    public const int MaxBatches = 80;

    private static readonly string[] TextExtensions = [
        ".cs", ".ts", ".tsx", ".vue", ".js", ".jsx", ".mjs", ".cjs",
        ".py", ".go", ".java", ".sql", ".json", ".md", ".yml",
        ".yaml", ".xml", ".sh", ".ps1", ".tf", ".toml", ".css",
        ".scss", ".html", ".csproj", ".props", ".targets"
    ];

    public sealed record DiffLine(string? EvidenceId, string Text);
    public sealed record Batch(ProgressFile File, int Part, IReadOnlyList<DiffLine> Lines)
    {
        public string PromptData => JsonSerializer.Serialize(new
        {
            file = File.Path, area = GitProgressMeaningPolicy.AreaForPath(File.Path), status = File.Status, part = Part,
            lines = Lines.Select(x => new { id = x.EvidenceId, code = x.Text })
        });
    }
    public sealed record Plan(IReadOnlyList<Batch> Batches,
        IReadOnlyList<ProgressEvidenceReference> References, int EligibleFiles,
        int MissingPatchFiles, int OmittedLines, int TruncatedBatches);

    public static Plan Build(IReadOnlyList<ProgressFile> files)
    {
        var batches = new List<Batch>();
        var refs = new List<ProgressEvidenceReference>();
        var eligible = 0;
        var missing = 0;
        var omitted = 0;
        var truncated = 0;
        foreach (var file in files)
        {
            if (!GitProgressEvidencePolicy.SafePath(file.Path) ||
                !TextExtensions.Any(e => file.Path.EndsWith(e, StringComparison.OrdinalIgnoreCase))) continue;
            eligible++;
            if (string.IsNullOrWhiteSpace(file.Patch)) { missing++; continue; }
            var lines = new List<DiffLine>();
            var size = 0;
            var part = 1;
            // Split on complete source lines. The exact changed code (+/-) and
            // available diff context are sent in original order; not ranked/sampled.
            foreach (var raw in file.Patch.Replace("\r\n", "\n").Split('\n'))
            {
                if (raw.Length == 0) continue;
                if (!GitProgressEvidencePolicy.IsAiSafeDiffLine(raw)) { omitted++; continue; }
                // Keep one file per batch: statements/citations cannot migrate
                // accidentally to an unrelated file when Ollama replies.
                var cost = raw.Length + 40;
                if (lines.Count > 0 && size + cost > MaxBatchCharacters)
                {
                    if (batches.Count < MaxBatches) batches.Add(new Batch(file, part, lines.ToArray()));
                    else truncated++;
                    part++;
                    lines = [];
                    size = 0;
                }
                string? id = null;
                if (raw.StartsWith('+') && !raw.StartsWith("+++", StringComparison.Ordinal) &&
                    raw.Length <= 210 && GitProgressMeaningPolicy.IsFunctionalLine(raw) &&
                    !GitProgressMeaningPolicy.IsLowValueTestSetupLine(file.Path, raw) &&
                    !GitProgressSemanticEvidencePolicy.IsSupportingOnlyLine(file.Path, raw))
                {
                    id = $"L{refs.Count + 1}";
                    refs.Add(new ProgressEvidenceReference(id, file.Path, raw));
                }
                lines.Add(new DiffLine(id, raw));
                size += cost;
            }
            if (lines.Count > 0)
            {
                if (batches.Count < MaxBatches) batches.Add(new Batch(file, part, lines.ToArray()));
                else truncated++;
            }
            else if (part == 1) missing++; // only unsafe or unprocessable patch lines
        }
        // Do not count any references in omitted batches as sent evidence.
        var actualIds = batches.SelectMany(b => b.Lines).Where(l => l.EvidenceId is not null)
            .Select(l => l.EvidenceId).ToHashSet(StringComparer.Ordinal);
        refs.RemoveAll(r => !actualIds.Contains(r.Id));
        return new Plan(batches, refs, eligible, missing, omitted, truncated);
    }
}
