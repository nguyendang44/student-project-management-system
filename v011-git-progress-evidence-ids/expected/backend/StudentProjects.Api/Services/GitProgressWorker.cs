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
            run.TotalCommits = diff.AheadBy;
            run.TotalFiles = diff.Files.Count;
            run.Additions = diff.Files.Sum(f => f.Additions);
            run.Deletions = diff.Files.Sum(f => f.Deletions);
            run.SentFiles = ai.SentFiles;
            run.PartialCoverage = diff.PartialCoverage;
            // Persist metadata and screened citations, never raw Git patches / secrets.
            run.FilesJson = JsonSerializer.Serialize(diff.Files.Take(100).Select(f => new
                { path = f.Path, status = f.Status, additions = f.Additions, deletions = f.Deletions }));
            run.EvidenceJson = JsonSerializer.Serialize(ai.Items.Select(f => new
                { file = f.File, evidence = f.Evidence, description = f.Description }));
            run.Summary = GitProgressEvidencePolicy.ComposeSummary(diff, ai.SentFiles, ai.Items.Count);
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
