using StudentProjects.Domain.Common;

namespace StudentProjects.Domain.Entities;

public sealed class MilestoneCriterion : Entity
{
    public Guid MilestoneId { get; set; }
    public string Title { get; set; } = "";
    public string AcceptanceCriteria { get; set; } = "";
    public int Points { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DefinitionChangedAt { get; set; }
    public DateTimeOffset? DeadlineAt { get; set; }
}

public sealed class MilestoneCriterionAssessment : Entity
{
    public Guid CriterionId { get; set; }
    public Guid ActorUserId { get; set; }
    public Guid? GitProgressRunId { get; set; }
    public string Source { get; set; } = "";
    public string Outcome { get; set; } = "";
    public string? Note { get; set; }
    public string EvidenceJson { get; set; } = "[]";
}
