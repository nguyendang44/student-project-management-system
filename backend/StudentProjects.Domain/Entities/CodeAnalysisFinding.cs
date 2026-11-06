namespace StudentProjects.Domain.Entities;

public sealed class CodeAnalysisFinding
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RunId { get; set; }
    public string Category { get; set; } = "";
    public string Severity { get; set; } = "";
    public string FilePath { get; set; } = "";
    public int LineNumber { get; set; }
    public string Evidence { get; set; } = "";
    public string Explanation { get; set; } = "";
    public string Recommendation { get; set; } = "";
    public string Fingerprint { get; set; } = "";
}
