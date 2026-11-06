namespace StudentProjects.Domain.Entities;

/// <summary>One reproducible comparison between two immutable Git commit SHAs.</summary>
public sealed class GitProgressRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid RepositoryLinkId { get; set; }
    public Guid RequestedByUserId { get; set; }
    public string BaseSha { get; set; } = "";
    public string HeadSha { get; set; } = "";
    public string ModelName { get; set; } = "";
    public string Status { get; set; } = "PENDING";
    public int TotalCommits { get; set; }
    public int TotalFiles { get; set; }
    public int Additions { get; set; }
    public int Deletions { get; set; }
    public int SentFiles { get; set; }
    public bool PartialCoverage { get; set; }
    public string Summary { get; set; } = "";
    public string FilesJson { get; set; } = "[]";
    public string EvidenceJson { get; set; } = "[]";
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
