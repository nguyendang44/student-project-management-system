namespace StudentProjects.Domain.Entities;

public sealed class CodeAnalysisRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid RepositoryLinkId { get; set; }
    public Guid RequestedByUserId { get; set; }
    public string Branch { get; set; } = "";
    public string CommitSha { get; set; } = "";
    public string Scope { get; set; } = "sampled";
    public string ModelName { get; set; } = "";
    public string Status { get; set; } = "PENDING";
    public string Summary { get; set; } = "";
    public string DevelopmentWork { get; set; } = "";
    public string AnalyzedPathsJson { get; set; } = "[]";
    public int SkippedFiles { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
