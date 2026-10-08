using StudentProjects.Domain.Common;
using StudentProjects.Domain.Enums;

namespace StudentProjects.Domain.Entities;

// Authentication stores roles relationally and hashes passwords, never plaintext.
public static class RoleIds
{
    public static readonly Guid Student = Guid.Parse("11111111-1111-4111-8111-111111111111");
    public static readonly Guid Lecturer = Guid.Parse("22222222-2222-4222-8222-222222222222");
    public static readonly Guid Admin = Guid.Parse("33333333-3333-4333-8333-333333333333");
}

public sealed class Role : Entity
{
    public string Name { get; set; } = string.Empty;
}

public sealed class User : Entity
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    // Logout increments this value to invalidate all previously issued access tokens.
    public int TokenVersion { get; set; }
}

public sealed class StudentProfile : Entity
{
    public Guid UserId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string? Faculty { get; set; }
}

public sealed class LecturerProfile : Entity
{
    public Guid UserId { get; set; }
    public string? Specialty { get; set; }
}

public sealed class RegistrationPeriod : Entity
{
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public bool IsOpen { get; set; }
}

// Capacity is per lecturer AND registration period, not a lifetime counter on the profile.
public sealed class LecturerCapacity : Entity
{
    public Guid LecturerUserId { get; set; }
    public Guid RegistrationPeriodId { get; set; }
    public int MaxStudents { get; set; }
    public int CurrentStudents { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

// Topic proposals are represented by Topic.Status; no duplicate TopicProposal table.
public sealed class Topic : Entity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Objective { get; set; }
    public string? ExpectedContent { get; set; }
    public string? ProposedTechnology { get; set; }
    public Guid ProposedByUserId { get; set; }
    public TopicStatus Status { get; set; } = TopicStatus.DRAFT;
    public bool IsRegistrationOpen { get; set; }
}

public sealed class TopicStateHistory : Entity
{
    public Guid TopicId { get; set; }
    public Guid ActorUserId { get; set; }
    public TopicStatus FromStatus { get; set; }
    public TopicStatus ToStatus { get; set; }
    public string? Reason { get; set; }
}

public sealed class TopicRegistration : Entity
{
    public Guid TopicId { get; set; }
    public Guid StudentUserId { get; set; }
    public Guid RegistrationPeriodId { get; set; }
    public RequestStatus Status { get; set; } = RequestStatus.PENDING;
}

public sealed class LecturerRequest : Entity
{
    public Guid StudentUserId { get; set; }
    public Guid LecturerUserId { get; set; }
    public Guid TopicId { get; set; }
    public Guid RegistrationPeriodId { get; set; }
    public RequestStatus Status { get; set; } = RequestStatus.PENDING;
    public string? RejectionReason { get; set; }
}

// Single-student project is an explicit assumption pending group-project confirmation.
public sealed class Project : Entity
{
    public Guid TopicId { get; set; }
    public Guid StudentUserId { get; set; }
    public Guid LecturerUserId { get; set; }
    public Guid RegistrationPeriodId { get; set; }
    public Guid? AcceptedLecturerRequestId { get; set; }
    public string? Description { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.DRAFT;
}

public sealed class Milestone : Entity
{
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset DeadlineAt { get; set; }
    public string? ExpectedResult { get; set; }
    public int PercentComplete { get; set; }
    public MilestoneStatus Status { get; set; } = MilestoneStatus.PENDING;
}

public sealed class ProgressUpdate : Entity
{
    public Guid MilestoneId { get; set; }
    public Guid StudentUserId { get; set; }
    public int PercentComplete { get; set; }
    public string WorkDone { get; set; } = string.Empty;
    public string? Note { get; set; }
}

public sealed class MilestoneSubmission : Entity
{
    public Guid MilestoneId { get; set; }
    public Guid SubmittedByStudentUserId { get; set; }
    public string ResultSummary { get; set; } = string.Empty;
    public string? DocumentReference { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
}

public sealed class ProjectEvaluation : Entity
{
    public Guid ProjectId { get; set; }
    public Guid LecturerUserId { get; set; }
    public Guid? MilestoneId { get; set; }
    public string? Feedback { get; set; }
    public bool IsApproved { get; set; }
}

public sealed class RepositoryLink : Entity
{
    public Guid ProjectId { get; set; }
    public string RepositoryUrl { get; set; } = string.Empty;
    public string? DefaultBranch { get; set; }
    public DateTimeOffset? LastCommitAt { get; set; }
}

public sealed class CodeAnalysisReport : Entity
{
    public Guid ProjectId { get; set; }
    public Guid RepositoryLinkId { get; set; }
    public AnalysisStatus Status { get; set; } = AnalysisStatus.PENDING;
    public string? CodeQuality { get; set; }
    public string? Complexity { get; set; }
    public string? Maintainability { get; set; }
    public string? PotentialIssuesJson { get; set; }
    public string? SecurityIssuesJson { get; set; }
    public string? RecommendationsJson { get; set; }
}

public sealed class Notification : Entity
{
    public Guid RecipientUserId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
}

public sealed class AutomationRun : Entity
{
    public string JobName { get; set; } = string.Empty;
    public AutomationRunStatus Status { get; set; } = AutomationRunStatus.PENDING;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class SystemError : Entity
{
    public string Origin { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class AuditEntry : Entity
{
    public Guid? ActorUserId { get; set; } // null = system-initiated action
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
}

public sealed class SystemSetting : Entity
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
