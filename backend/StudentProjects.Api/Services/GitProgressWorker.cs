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
            var codeContext = await RepositoryContextBuilder.ReadAsync(
                services.GetRequiredService<GitHubSnapshotClient>(), owner, repo, diff, ct);
            var attempt = await GitProgressAiAvailability.RunAsync(
                token => services.GetRequiredService<AiGitProgressClient>().AnalyzeAsync(diff, token, codeContext), ct);
            var ai = attempt.Result;
            if (attempt.Status != "available")
                logger.LogWarning("Git progress {RunId}: AI unavailable ({Status}); retaining Git facts", id, attempt.Status);
            logger.LogInformation(
                "Git progress {RunId}: eligible={Eligible}, fullyAnalyzed={Complete}, " +
                "sent={Sent}, batches={Done}/{Planned}, failedBatches={Failed}, candidates={Candidates}, verified={Verified}",
                id, ai.EligibleFiles, ai.FullyAnalyzedFiles, ai.SentFiles,
                ai.CompletedBatches, ai.PlannedBatches, ai.FailedBatches,
                ai.CandidateCount, ai.RuleBasedFallback ? 0 : ai.Items.Count);
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
                    "Git progress {RunId}: rejection details invalidLength={InvalidLength}, " +
                    "unsupportedClaim={UnsupportedClaim}, boilerplate={Boilerplate}, missingAction={MissingAction}, testSetupOnly={TestSetupOnly}",
                    id, diagnostics.InvalidLength, diagnostics.UnsupportedClaim,
                    diagnostics.Boilerplate, diagnostics.MissingAction, diagnostics.TestSetupOnly);
                logger.LogInformation("Git progress {RunId}: semanticMismatch={SemanticMismatch}, supportingDetail={SupportingDetail}",
                    id, diagnostics.SemanticMismatch, diagnostics.SupportingDetail);
            }
            run.TotalCommits = diff.AheadBy;
            // Persist GitHub's unfiltered comparison totals. The safe subset remains
            // in FilesJson and is the only set used for source evidence.
            run.TotalFiles = diff.RawFileCount;
            run.Additions = diff.RawAdditions;
            run.Deletions = diff.RawDeletions;
            run.SentFiles = ai.SentFiles;
            run.PartialCoverage = diff.PartialCoverage;
            // Persist metadata and screened citations, never raw Git patches / secrets.
            run.FilesJson = JsonSerializer.Serialize(diff.Files.Take(100).Select(f => new
                { path = f.Path, status = f.Status, additions = f.Additions, deletions = f.Deletions }));
            // Feature facts are derived from the GitHub diff, independently of the
            // Ollama 10-file sample. Keep old JSON history compatible, no SQL migration.
            var features = GitProgressFeatureSynthesis.Extract(diff.Files);
            // Ask Ollama to group the verified changed lines into user-facing features.
            // A release may contain many independent features; related UI/API/service/test
            // lines should become one feature card with a traceable implementation flow.
            var featureEvidenceSet = GitProgressFeatureReport.BuildEvidence(ai.FeatureEvidence ?? [], features);
            var featureEvidence = featureEvidenceSet.Items;
            var featureReport = new ProgressAiFeatureReportResult([], 0, 0, 0,
                featureEvidence.Count == 0 ? "empty" : "not-requested");
            if (featureEvidence.Count > 0 && attempt.Status == "available" && ai.CompletedBatches > 0)
            {
                try
                {
                    featureReport = await services.GetRequiredService<AiGitProgressClient>()
                        .AnalyzeFeatureReportAsync(featureEvidence, codeContext, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    featureReport = new ProgressAiFeatureReportResult([], 0, 0, 0, "error");
                    logger.LogWarning("Git progress {RunId}: feature report unavailable ({Reason})",
                        id, ex.GetType().Name);
                }
            }
            var aiVerified = ai.RuleBasedFallback ? 0 : ai.Items.Count;
            // Always ground GitHub V10 operations in exact GET/POST/DELETE route
            // mappings. The earlier route-only check missed model groups that
            // mixed one real route with generic cache or interface declarations.
            var githubRoutes = GitProgressFeatureReport.BuildGitHubRouteReport(diff.Files);
            var otherAiFeatures = githubRoutes.Count > 0
                ? featureReport.Items.Where(f => !GitProgressFeatureReport.IsGitHubFeatureClaim(f)).ToArray()
                : featureReport.Items.ToArray();
            var routeOnlyAi = githubRoutes.Count == 0 &&
                GitProgressFeatureReport.IsRouteOnlyReport(featureReport.Items);
            var routeFallback = githubRoutes.Count == 0 &&
                (featureReport.Items.Count == 0 || routeOnlyAi)
                ? GitProgressFeatureReport.BuildRouteEvidenceReport(diff.Files) : [];
            var displayedFeatures = githubRoutes.Count > 0
                ? otherAiFeatures.Concat(githubRoutes).ToArray()
                : routeFallback.Count > 0 ? routeFallback : featureReport.Items;
            var observations = githubRoutes.Count > 0
                ? GitProgressFeatureReport.BuildAi(otherAiFeatures).ToList()
                    .Concat(GitProgressFeatureReport.BuildAi(githubRoutes, "diff-feature-report")).ToList()
                : displayedFeatures.Count > 0
                    ? GitProgressFeatureReport.BuildAi(displayedFeatures,
                        routeFallback.Count > 0 ? "diff-feature-report" : "ai-feature-report").ToList()
                    : GitProgressFeatureReport.Build(features, []).ToList();
            // Keep old evidence comments only as a fallback/audit view. When the AI
            // feature report is available, don't flood the student-facing report with
            // a second flat list of code-line observations.
            if (displayedFeatures.Count == 0 && (aiVerified > 0 || features.Count == 0))
                observations.AddRange(ai.Items.Select(f => (object)new
                {
                    file = f.File, area = GitProgressMeaningPolicy.AreaForPath(f.File),
                    title = (string?)null, evidence = f.Evidence,
                    description = f.Description, citations = (object?)null,
                    source = ai.RuleBasedFallback ? "diff-rule" : "ollama"
                }));
            // Store ONLY aggregate diagnostic counters as a private metadata row in
            // EvidenceJson. GET strips it before returning normal work items. This
            // needs no migration and never persists Ollama text or diff snippets.
            var reportDiagnostics = new ProgressReportDiagnostics(
                ai.SentFiles, ai.RawItemCount, ai.CandidateCount, aiVerified,
                ai.RuleBasedFallback ? ai.Items.Count : 0,
                ai.Diagnostics ?? new GitProgressMeaningPolicy.RejectionDiagnostics(0, 0, 0, 0, 0, 0, 0),
                displayedFeatures.Count > 0 ? displayedFeatures.Count : features.Count,
                featureReport.RawItems, featureReport.ParsedItems, featureReport.Items.Count,
                featureReport.Status, codeContext.Status,
                codeContext.Excerpts.Count, codeContext.Excerpts.Select(x => x.File).Distinct().Count(), attempt.Status,
                ai.EligibleFiles, ai.FullyAnalyzedFiles, ai.MissingPatchFiles,
                ai.PlannedBatches, ai.CompletedBatches, ai.FailedBatches,
                ai.SkippedLines, ai.TruncatedBatches, ai.CoverageStatus,
                featureReport.RawItems, featureReport.ParsedItems, featureReport.Items.Count,
                featureReport.RejectedEvidence, featureReport.Status, featureEvidence.Count,
                featureEvidenceSet.Omitted);
            run.EvidenceJson = GitProgressDiagnostics.Write(observations, reportDiagnostics);
            run.Summary = GitProgressEvidencePolicy.ComposeSummary(diff, ai.SentFiles, aiVerified,
                attempt.Status == "available" && ai.CompletedBatches > 0);
            if (attempt.Status == "available")
                run.Summary += $" Phạm vi AI: {ai.FullyAnalyzedFiles}/{ai.EligibleFiles} file văn bản đủ điều kiện " +
                    $"đã phân tích hết phần patch an toàn, {ai.CompletedBatches}/{ai.PlannedBatches} lượt Ollama hoàn thành." +
                    (ai.CoverageStatus == "partial"
                        ? $" Chưa đầy đủ: {ai.MissingPatchFiles} file thiếu patch, " +
                          $"{ai.SkippedLines} dòng bị ẩn do an toàn/độ dài, " +
                          $"{ai.TruncatedBatches} đoạn vượt giới hạn, {ai.FailedBatches} lượt chưa hoàn tất."
                        : ai.CoverageStatus == "no-readable-patch" ? " Không có patch văn bản có thể gửi AI." : "");
            if (githubRoutes.Count > 0)
                run.Summary += $" Các nhận xét AI liên quan GitHub được đối chiếu và chuẩn hóa thành {githubRoutes.Count} thao tác API riêng biệt từ Git diff; " +
                    $"các dòng cấu hình chung không được tính là bằng chứng cho thao tác. " +
                    $"Đã giữ thêm {otherAiFeatures.Length} nhóm AI không thuộc GitHub nếu có bằng chứng. Chưa chứng minh chức năng chạy đúng.";
            else if (routeFallback.Count > 0)
                run.Summary += routeOnlyAi
                    ? $" Các nhóm AI chỉ dẫn khai báo định tuyến nên được chuẩn hóa thành {routeFallback.Count} thao tác API có bằng chứng Git; có đối chiếu handler/frontend nếu tìm thấy dòng thêm tương ứng. Chưa chứng minh chức năng chạy đúng."
                    : $" Các nhóm AI chưa đủ căn cứ; trích riêng {routeFallback.Count} tuyến API thực sự được thêm trong diff và mã liên quan có bằng chứng. Chưa chứng minh chức năng hoàn thành.";
            else if (featureReport.Items.Count > 0)
                run.Summary += $" Ollama đã tổng hợp {featureReport.Items.Count} chức năng/công việc theo mục tiêu người dùng; mỗi nhóm có các bước triển khai và dòng Git đối chiếu. Không suy ra hoàn thành nếu chưa có kết quả kiểm thử thực tế.";
            else if (features.Count > 0)
                run.Summary += $" AI chưa nhóm được chức năng đủ căn cứ; giữ {features.Count} nhóm bằng chứng Git dự phòng để đối chiếu.";
            else if (ai.RuleBasedFallback)
                run.Summary += " Chưa xác định được nhóm chức năng từ diff; xem danh sách file thay đổi để đối chiếu.";
            logger.LogInformation("Git progress {RunId}: aiFeatureGroups={FeatureGroups}, featureEvidence={FeatureEvidence}, verifiedAiDetails={AiVerified}, featureReportStatus={FeatureReportStatus}",
                id, featureReport.Items.Count, featureEvidence.Count, aiVerified, featureReport.Status);
            if (attempt.Status != "available")
                run.Summary += " Ollama chưa hoàn tất; báo cáo này chỉ giữ dữ kiện Git. Có thể gửi lại yêu cầu để thử AI.";
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
