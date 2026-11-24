using System.Text.Json;

namespace StudentProjects.Api.Services;

public sealed partial class AiGitProgressClient
{
    private async Task<WordComparisonResult> CompareMilestoneCorpusAsync(IReadOnlyList<WordReportLine> claims,
        IReadOnlyList<WordEvidence> evidence, CancellationToken ct)
    {
        // Send every feature, description, step and citation. Split by source feature instead of
        // sampling its first ten lines. Context is attached once per feature in each batch.
        var chunks = new List<IReadOnlyList<WordEvidence>>();
        var current = new List<WordEvidence>();
        var size = 0;
        foreach (var feature in evidence.GroupBy(e => (e.GitProgressRunId, e.Title)))
        {
            var first = feature.First();
            var metadata = (first.FeatureDescription?.Length ?? 0) + (first.ImplementationSteps?.Length ?? 0);
            if (metadata > 16000) throw new InvalidDataException("Một nhóm chức năng quá dài để đối chiếu an toàn; chưa tự cho điểm.");
            var firstLine = true;
            foreach (var row in feature)
            {
                var line = firstLine ? row : row with { FeatureDescription = null, ImplementationSteps = null };
                var cost = JsonSerializer.Serialize(line).Length;
                if (cost > 24000) throw new InvalidDataException("Một dòng hoặc mô tả vượt ngữ cảnh AI; chưa tự cho điểm.");
                if (current.Count > 0 && size + cost > 24000)
                { chunks.Add(current.ToArray()); current.Clear(); size = 0; }
                current.Add(line); size += cost; firstLine = false;
            }
        }
        if (current.Count > 0) chunks.Add(current.ToArray());
        if (chunks.Count == 0) return GitProgressWordComparison.Score(claims.Select(c =>
            new WordComparisonRow(c.Id, c.Text, "missing", [])).ToArray());
        if (chunks.Count == 1) return await CompareClaimsAsync(claims, chunks[0], true, ct);
        var candidates = claims.ToDictionary(c => c.Id, _ => new List<WordEvidence>());
        foreach (var chunk in chunks)
        {
            var part = await CompareClaimsAsync(claims, chunk, true, ct, applyMilestoneGuard: false);
            foreach (var item in part.Items) candidates[item.Id].AddRange(item.Evidence);
        }
        var merged = new List<WordComparisonRow>();
        foreach (var claim in claims)
        {
            var selected = GitProgressMilestoneEvidence.SelectSupporting(claim.Text, candidates[claim.Id]
                .DistinctBy(e => (e.File, e.Evidence)).ToArray());
            if (selected.Count == 0) { merged.Add(new(claim.Id, claim.Text, "missing", [])); continue; }
            // Selection follows a complete first pass over the corpus; this final pass checks
            // whether evidence from different weeks jointly covers the acceptance conditions.
            var compact = selected.Select(e => e with { FeatureDescription = null, ImplementationSteps = null }).ToArray();
            if (JsonSerializer.Serialize(compact).Length > 24000)
                throw new InvalidDataException("Bằng chứng tổng hợp vượt ngữ cảnh AI; chưa tự cho điểm.");
            var final = await CompareClaimsAsync([claim], compact, true, ct);
            merged.Add(final.Items.Single());
        }
        return GitProgressWordComparison.Score(merged);
    }
}
