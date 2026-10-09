namespace StudentProjects.Application.Contracts;

public sealed record TopicInput(string? Title, string? Description, string? Objective,
    string? ExpectedContent, string? ProposedTechnology, bool? IsRegistrationOpen);
public sealed record TopicItem(Guid Id, string Title, string Description, string? Objective,
    string? ExpectedContent, string? ProposedTechnology, Guid ProposedByUserId, string ProposedBy,
    string Status, bool IsRegistrationOpen, DateTimeOffset CreatedAt, Guid? ReservedForStudentUserId, bool CanDelete);
public sealed record TopicPage(IReadOnlyList<TopicItem> Items, int Total, int Page, int PageSize);
public sealed record RejectTopicRequest(string? Reason);
public sealed record TopicTransitionItem(string FromStatus, string ToStatus, string? Reason, DateTimeOffset At);
public sealed record PeriodInput(string? Name, DateTimeOffset StartsAt, DateTimeOffset EndsAt, bool IsOpen);
public sealed record PeriodStateInput(bool? IsOpen);
public sealed record PeriodItem(Guid Id, string Name, DateTimeOffset StartsAt, DateTimeOffset EndsAt, bool IsOpen);
public sealed record RegisterTopicRequest(Guid TopicId, Guid RegistrationPeriodId);
public sealed record RegistrationItem(Guid Id, Guid TopicId, string TopicTitle,
    Guid StudentUserId, string StudentName, Guid RegistrationPeriodId,
    string PeriodName, string Status, DateTimeOffset CreatedAt);
