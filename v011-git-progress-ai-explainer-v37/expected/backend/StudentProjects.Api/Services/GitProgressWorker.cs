using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StudentProjects.Infrastructure.Persistence;

namespace StudentProjects.Api.Services;

/// <summary>Single-instance LocalDB worker; deploy only one API instance, as with CodeAnalysisWorker.</summary>
public sealed class GitProgressWorker(IServiceScopeFactory scopes, ILogger<GitProgressWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        try
        {
            await using var startupScope = scopes.CreateAsyncScope();
            var db = startupScope.ServiceProvider.GetRequiredService<StudentProjectsDbContext>();
            var interrupted = await db.GitProgressRuns.Where(x => x.Status == "RUNNING").ToListAsync(stoppingToken);
            foreach (var run in interrupted) { run.Status = "PENDING"; run.StartedAt = null; }
            await db.SaveChangesAsync(stoppingToken);
        }
        catch (Exception ex) { logger.LogError(ex, "Git progress recovery failed; check DB migration"); }
        while (!stoppingToken.IsCancellationRequested)
        {
            Guid? id = null;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<StudentProjectsDbContext>();
                id = await db.GitProgressRuns.AsNoTracking().Where(x => x.Status == "PENDING")
                    .OrderBy(x => x.CreatedAt).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(stoppingToken);
                if (id.HasValue) await ProcessAsync(id.Value, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning(ex, "Git progress queue poll failed"); }
            try { await Task.Delay(TimeSpan.FromSeconds(id.HasValue ? 1 : 5), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task ProcessAsync(Guid id, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<StudentProjectsDbContext>();
        var run = await db.GitProgressRuns.FirstOrDefaultAsync(x => x.Id == id && x.Status == "PENDING", ct);
        if (run is null) return;
        run.Status = "RUNNING";
        run.StartedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        try
        {
            var link = await db.Repositories.AsNoTracking().FirstOrDefaultAsync(x => x.Id == run.RepositoryLinkId, ct)
                ?? throw new AnalysisInputException("Repository không còn liên kết với Project.");
            if (!GitHubUrl.TryParse(link.RepositoryUrl, out var owner, out var repo))
                throw new AnalysisInputException("Repository URL không hợp lệ.");
            await services.GetRequiredService<GitHubSnapshotClient>().EnsurePublicAsync(owner, repo, ct);
            var diff = await services.GetRequiredService<GitHubProgressClient>()
                .CompareAsync(owner, repo, run.BaseSha, run.HeadSha, ct);
            var ai = await services.GetRequiredService<AiGitProgressClient>().AnalyzeAsync(diff, ct);
            logger.LogInformation(
                "Git progress {RunId}: diff files sent={Sent}, AI candidates={Candidates}, verified={Verified}",
                id, ai.SentFiles, ai.CandidateCount, ai.RuleBasedFallback ? 0 : ai.Items.Count);
            if (ai.RuleBasedFallback)
                logger.LogInformation("Git progress {RunId}: ruleBasedFallback={Count} (no verified AI observations)",
                    id, ai.Items.Count);
            if (ai.Diagnostics is { } diagnostics)
            {
                logger.LogInformation(
                    "Git progress {RunId}: rejected unknownId={UnknownId}, unsentFile={UnsentFile}, " +
                    "unsentLine={UnsentLine}, nonFunctionalLine={NonFunctional}, " +
                    "weakDescription={WeakDescription}, duplicate={Duplicate}; eligibleBeforeCap={Eligible}",
                    id, diagnostics.UnknownId, diagnostics.UnsentFile, diagnostics.UnsentLine,
                    diagnostics.NonFunctionalLine, diagnostics.WeakDescription,
                    diagnostics.Duplicate, diagnostics.Eligible);
                logger.LogInformation(
                    "Git progress {RunId}: weakDescription reasons invalidLength={InvalidLength}, " +
                    "unsupportedClaim={UnsupportedClaim}, boilerplate={Boilerplate}, missingAction={MissingAction}",
                    id, diagnostics.InvalidLength, diagnostics.UnsupportedClaim,
                    diagnostics.Boilerplate, diagnostics.MissingAction);
            }
            run.TotalCommits = diff.AheadBy;
            run.TotalFiles = diff.Files.Count;
            run.Additions = diff.Files.Sum(f => f.Additions);
            run.Deletions = diff.Files.Sum(f => f.Deletions);
            run.SentFiles = ai.SentFiles;
            run.PartialCoverage = diff.PartialCoverage;
            // Persist metadata and screened citations, never raw Git patches / secrets.
            run.FilesJson = JsonSerializer.Serialize(diff.Files.Take(100).Select(f => new
                { path = f.Path, status = f.Status, additions = f.Additions, deletions = f.Deletions }));
            // Feature facts are derived from the GitHub diff, independently of the
            // Ollama 10-file sample. Keep old JSON history compatible, no SQL migration.
            var features = GitProgressFeatureSynthesis.Extract(diff.Files);
            // Optional AI explanation is separate from and cannot replace verified facts.
            // A failing/slow model must not erase the deterministic progress report.
            IReadOnlyList<ProgressFeatureExplanation> explanations = [];
            if (features.Count > 0)
            {
                try
                {
                    explanations = await services.GetRequiredService<AiGitProgressClient>()
                        .ExplainFeaturesAsync(features, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    logger.LogWarning("Git progress {RunId}: AI explanation unavailable ({Reason})",
                        id, ex.GetType().Name);
                }
            }
            var aiVerified = ai.RuleBasedFallback ? 0 : ai.Items.Count;
            var observations = features.Select(f => new
                {
                    file = f.Citations[0].File,
                    area = f.Area,
                    title = f.Title,
                    evidence = f.Citations[0].Evidence,
                    description = f.Description,
                    citations = f.Citations.Select(c => new { file = c.File, evidence = c.Evidence }).ToArray(),
                    source = "feature-rule"
                }).Cast<object>().ToList();
            // AI interprets only known feature facts; citations ALWAYS come from
            // the original rule-derived feature, never from free-form model text.
            foreach (var explanation in explanations)
            {
                if (!GitProgressAiExplanationPolicy.TryResolve(explanation.FactId, features, out var fact))
                    continue;
                observations.Add(new
                {
                    file = fact.Citations[0].File, area = fact.Area,
                    title = "AI diễn giải: " + fact.Title,
                    evidence = fact.Citations[0].Evidence,
                    description = explanation.Explanation,
                    citations = fact.Citations.Select(c => new { file = c.File, evidence = c.Evidence }).ToArray(),
                    source = "ollama-feature"
                });
            }
            // Show genuinely verified AI comments separately. Suppress the old
            // one-line fallback whenever grouped feature observations exist.
            if (aiVerified > 0 || features.Count == 0)
                observations.AddRange(ai.Items.Select(f => (object)new
                {
                    file = f.File, area = GitProgressMeaningPolicy.AreaForPath(f.File),
                    title = (string?)null, evidence = f.Evidence,
                    description = f.Description, citations = (object?)null,
                    source = ai.RuleBasedFallback ? "diff-rule" : "ollama"
                }));
            run.EvidenceJson = JsonSerializer.Serialize(observations);
            run.Summary = GitProgressEvidencePolicy.ComposeSummary(diff, ai.SentFiles, aiVerified);
            if (features.Count > 0)
                run.Summary += $" Báo cáo tổng hợp {features.Count} nhóm công việc bằng quy tắc từ các dòng Git diff có thể đối chiếu, không phải nhận xét Ollama; không xác nhận chức năng đã chạy.";
            else if (ai.RuleBasedFallback)
                run.Summary += " Ollama không tạo nhận xét đạt bộ lọc; hệ thống chỉ có thể trình bày những dòng diff được trích xuất theo quy tắc.";
            run.Summary += explanations.Count > 0
                ? $" Ollama bổ sung {explanations.Count} diễn giải tham khảo gắn với nhóm có bằng chứng; vẫn cần giảng viên xác minh."
                : " Chưa có diễn giải AI bổ sung đạt điều kiện; phần tổng hợp theo quy tắc vẫn có giá trị độc lập.";
            logger.LogInformation("Git progress {RunId}: groupedFeatures={Features}, verifiedAi={AiVerified}, aiFeatureExplanations={Explained}",
                id, features.Count, aiVerified, explanations.Count);
            run.Status = "COMPLETED";
            run.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { /* reset on restart */ }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Git progress run {Id} failed", id);
            await using var errorScope = scopes.CreateAsyncScope();
            var errorDb = errorScope.ServiceProvider.GetRequiredService<StudentProjectsDbContext>();
            var failed = await errorDb.GitProgressRuns.FirstOrDefaultAsync(x => x.Id == id);
            if (failed is not null)
            {
                failed.Status = "FAILED";
                var message = ex is AnalysisInputException ? ex.Message : "Không hoàn thành phân tích tiến độ. Xem log backend.";
                failed.ErrorMessage = message[..Math.Min(message.Length, 1000)];
                failed.CompletedAt = DateTimeOffset.UtcNow;
                await errorDb.SaveChangesAsync();
            }
        }
    }
}
