using Microsoft.EntityFrameworkCore;
using StudentProjects.Domain.Entities;
using StudentProjects.Domain.Enums;
using StudentProjects.Infrastructure.Persistence;

namespace StudentProjects.Api.Services;

public sealed record MilestoneAssessmentRequest
{
    public Guid MilestoneId { get; init; }
    public Guid RunId { get; init; }
}

public static class MilestoneAssessmentRequests
{
    public const string Automatic = "MILESTONE_COMPARE_REQUESTED";
    public const string Manual = "MILESTONE_RECHECK_REQUESTED";
    public const string Done = "MILESTONE_CHECK_DONE";
    public static AuditEntry CreateComparison(Guid runId, Guid actor) => new()
    { ActorUserId = actor, Action = Automatic, EntityName = "GitProgressRun", EntityId = runId };
    public static AuditEntry CreateRecheck(MilestoneAssessmentRequest request, Guid actor) => new()
    { ActorUserId = actor, Action = Manual, EntityName = $"MilestoneRecheck:{request.RunId:N}", EntityId = request.MilestoneId };
    public static Guid? ManualRun(AuditEntry request) => request.Action == Manual &&
        request.EntityName.StartsWith("MilestoneRecheck:", StringComparison.Ordinal) &&
        Guid.TryParseExact(request.EntityName[17..], "N", out var id) ? id : null;
}

/// <summary>Consumes only explicit compare/recheck events. Never discovers work by scanning old reports.</summary>
public sealed class MilestoneAutoAssessmentWorker(IServiceScopeFactory scopes,
    ILogger<MilestoneAutoAssessmentWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<StudentProjectsDbContext>();
                var requests = await db.AuditEntries.AsNoTracking().Where(a =>
                    (a.Action == MilestoneAssessmentRequests.Automatic || a.Action == MilestoneAssessmentRequests.Manual) &&
                    !db.AuditEntries.Any(done => done.Action == MilestoneAssessmentRequests.Done && done.EntityId == a.Id))
                    .OrderBy(a => a.CreatedAt).Take(20).ToListAsync(stoppingToken);
                foreach (var request in requests)
                {
                    try
                    {
                        var runId = request.Action == MilestoneAssessmentRequests.Automatic
                            ? request.EntityId : MilestoneAssessmentRequests.ManualRun(request);
                        if (!runId.HasValue)
                        { await FinishAsync(db, request.Id, stoppingToken, MilestoneAssessmentStatus.Skipped); continue; }
                        var run = await db.GitProgressRuns.AsNoTracking().SingleOrDefaultAsync(r => r.Id == runId, stoppingToken);
                        if (run is null || run.Status == "FAILED")
                        { await FinishAsync(db, request.Id, stoppingToken, MilestoneAssessmentStatus.Skipped); continue; }
                        if (run.Status != "COMPLETED") continue;
                        var project = await db.Projects.AsNoTracking().SingleOrDefaultAsync(p => p.Id == run.ProjectId, stoppingToken);
                        if (project is null || project.Status is ProjectStatus.COMPLETED or ProjectStatus.CANCELLED)
                        { await FinishAsync(db, request.Id, stoppingToken, MilestoneAssessmentStatus.Skipped); continue; }
                        await MilestoneAssessmentStatus.MarkAsync(db, request.Id, MilestoneAssessmentStatus.Started, stoppingToken);
                        var cutoff = request.CreatedAt;
                        if (request.Action == MilestoneAssessmentRequests.Automatic && run.CompletedAt > cutoff)
                            cutoff = run.CompletedAt.Value;
                        var reports = await db.GitProgressRuns.AsNoTracking().Where(r => r.ProjectId == project.Id && r.Status == "COMPLETED" &&
                            (r.CompletedAt ?? r.CreatedAt) <= cutoff).OrderBy(r => r.CreatedAt).ThenBy(r => r.Id).ToListAsync(stoppingToken);
                        var milestones = await db.Milestones.AsNoTracking().Where(m => m.ProjectId == project.Id && m.ProposalStatus == "CONFIRMED" &&
                            (request.Action == MilestoneAssessmentRequests.Automatic || m.Id == request.EntityId) &&
                            db.MilestoneCriteria.Any(c => c.MilestoneId == m.Id && !c.IsDeleted)).Select(m => m.Id).ToListAsync(stoppingToken);
                        foreach (var milestoneId in milestones)
                        {
                            await using var itemScope = scopes.CreateAsyncScope();
                            await MilestoneAutoAssessment.EvaluateAsync(
                                itemScope.ServiceProvider.GetRequiredService<StudentProjectsDbContext>(),
                                itemScope.ServiceProvider.GetRequiredService<AiGitProgressClient>(), milestoneId, run,
                                request.ActorUserId ?? run.RequestedByUserId, stoppingToken, force: true, reports: reports);
                            await MilestoneAssessmentStatus.MarkAsync(db, request.Id, MilestoneAssessmentStatus.ItemDone,
                                stoppingToken, milestoneId);
                        }
                        await FinishAsync(db, request.Id, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                    catch (Exception ex)
                    {
                        await FinishAsync(db, request.Id, stoppingToken, MilestoneAssessmentStatus.FailureAction(ex));
                        logger.LogWarning(ex, "Milestone assessment request {RequestId} failed; retaining existing scores", request.Id);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning(ex, "Milestone assessment request queue failed"); }
            try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private static async Task FinishAsync(StudentProjectsDbContext db, Guid requestId, CancellationToken ct, string? terminal = null)
    {
        db.AuditEntries.Add(new AuditEntry { Action = MilestoneAssessmentRequests.Done,
            EntityName = "MilestoneAssessmentRequest", EntityId = requestId });
        if (terminal is not null)
            db.AuditEntries.Add(new AuditEntry { Action = terminal, EntityName = "MilestoneAssessmentRequest", EntityId = requestId });
        await db.SaveChangesAsync(ct);
    }
}
