using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StudentProjects.Domain.Entities;
using StudentProjects.Infrastructure.Persistence;

namespace StudentProjects.Api.Services;

// Single-process worker for LocalDB deployment. Do not run multiple API instances with this worker
// without adding a SQL-backed claim/lease mechanism.
public sealed class CodeAnalysisWorker(IServiceScopeFactory scopes, ILogger<CodeAnalysisWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        // Recover interrupted runs after application restart.
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<StudentProjectsDbContext>();
            var interrupted = await db.CodeAnalysisRuns.Where(r => r.Status == "RUNNING").ToListAsync(stoppingToken);
            foreach (var run in interrupted) { run.Status = "PENDING"; run.StartedAt = null; }
            await db.SaveChangesAsync(stoppingToken);
        }
        catch (Exception ex) { logger.LogError(ex, "Could not recover interrupted analysis runs"); }

        while (!stoppingToken.IsCancellationRequested)
        {
            Guid? runId = null;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<StudentProjectsDbContext>();
                runId = await db.CodeAnalysisRuns.AsNoTracking()
                    .Where(r => r.Status == "PENDING").OrderBy(r => r.CreatedAt)
                    .Select(r => (Guid?)r.Id).FirstOrDefaultAsync(stoppingToken);
                if (runId.HasValue) await ProcessAsync(runId.Value, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Error polling code analysis queue"); }
            try { await Task.Delay(TimeSpan.FromSeconds(runId.HasValue ? 1 : 4), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task ProcessAsync(Guid id, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<StudentProjectsDbContext>();
        var run = await db.CodeAnalysisRuns.FirstOrDefaultAsync(r => r.Id == id && r.Status == "PENDING", ct);
        if (run is null) return;
        run.Status = "RUNNING";
        run.StartedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        try
        {
            var link = await db.Repositories.AsNoTracking().FirstOrDefaultAsync(x => x.Id == run.RepositoryLinkId, ct)
                ?? throw new AnalysisInputException("Repository đã bị gỡ liên kết.");
            if (!GitHubUrl.TryParse(link.RepositoryUrl, out var owner, out var repo))
                throw new AnalysisInputException("URL repository trong database không hợp lệ.");
            // Recheck public status before fetching source, even if the link was created in v0.10.
            var snapshotClient = services.GetRequiredService<GitHubSnapshotClient>();
            await snapshotClient.EnsurePublicAsync(owner, repo, ct);
            var snapshot = await snapshotClient.ReadAtCommitAsync(owner, repo, run.CommitSha, ct);
            var report = await services.GetRequiredService<AiCodeReviewClient>().AnalyzeAsync(snapshot, ct);

            run.Summary = report.Summary;
            run.DevelopmentWork = report.DevelopmentWork;
            // These are paths actually submitted to the model, NOT merely fetched from GitHub.
            var analyzedPaths = report.AnalyzedPaths ?? Array.Empty<string>();
            run.AnalyzedPathsJson = JsonSerializer.Serialize(analyzedPaths);
            run.SkippedFiles = snapshot.SkippedFiles + snapshot.Files.Count - analyzedPaths.Count;
            foreach (var f in report.Findings)
            {
                db.CodeAnalysisFindings.Add(new CodeAnalysisFinding
                {
                    RunId = run.Id, Category = f.Category, Severity = f.Severity, FilePath = f.File,
                    LineNumber = f.Line, Evidence = Cut(f.Evidence, 400),
                    Explanation = Cut(f.Explanation, 3000), Recommendation = Cut(f.Recommendation, 3000),
                    Fingerprint = Fingerprint(f)
                });
            }
            run.Status = "COMPLETED";
            run.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { /* recovered at restart */ }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Code analysis run {RunId} failed", id);
            // Use a fresh DB context to avoid saving partially-added findings.
            await using var errorScope = scopes.CreateAsyncScope();
            var errorDb = errorScope.ServiceProvider.GetRequiredService<StudentProjectsDbContext>();
            var failed = await errorDb.CodeAnalysisRuns.FirstOrDefaultAsync(r => r.Id == id, CancellationToken.None);
            if (failed is not null)
            {
                failed.Status = "FAILED";
                failed.ErrorMessage = Cut(ex is AnalysisInputException ? ex.Message : "Không thể hoàn thành phân tích.", 1000);
                failed.CompletedAt = DateTimeOffset.UtcNow;
                await errorDb.SaveChangesAsync(CancellationToken.None);
            }
        }
    }
    private static string Cut(string? s, int max) => (s ?? "")[..Math.Min((s ?? "").Length, max)];
    private static string Fingerprint(AiFinding f)
    {
        var normalized = string.Join(" ", f.Evidence.ToLowerInvariant().Split((char[]?)null,
            StringSplitOptions.RemoveEmptyEntries));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{f.Category}|{f.File}|{normalized}"));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
