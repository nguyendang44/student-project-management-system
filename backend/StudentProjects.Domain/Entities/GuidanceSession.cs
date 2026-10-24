using StudentProjects.Domain.Common;

namespace StudentProjects.Domain.Entities;

// v0.8: record of an in-person or online supervisor meeting.
public sealed class GuidanceSession : Entity
{
    public Guid ProjectId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset MeetingAt { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Feedback { get; set; } = string.Empty;
    public string NextActions { get; set; } = string.Empty;
    public DateTimeOffset? LecturerConfirmedAt { get; set; }
}
