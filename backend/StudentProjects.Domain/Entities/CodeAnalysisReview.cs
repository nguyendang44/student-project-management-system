namespace StudentProjects.Domain.Entities;

public sealed class CodeAnalysisReview
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RunId { get; set; }
    public Guid LecturerUserId { get; set; }
    public string Comment { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
