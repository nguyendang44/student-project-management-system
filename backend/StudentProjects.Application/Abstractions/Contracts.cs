namespace StudentProjects.Application.Abstractions;
// Interfaces only: future implementations belong in Application/Infrastructure features.
public interface IGitHubRepositoryClient
{
    Task<string?> GetMetadataAsync(string repositoryUrl, CancellationToken cancellationToken = default);
}
public interface ICodeAnalysisClient
{
    Task<string> AnalyzeAsync(string repositoryUrl, CancellationToken cancellationToken = default);
}
public interface INotificationSender
{
    Task SendAsync(Guid recipientUserId, string message, CancellationToken cancellationToken = default);
}
public interface IDeadlineScanner
{
    Task ScanDueMilestonesAsync(CancellationToken cancellationToken = default);
}
public interface IClock { DateTimeOffset UtcNow { get; } }
