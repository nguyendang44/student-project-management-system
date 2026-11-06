using StudentProjects.Api.Services;
using Xunit;

namespace StudentProjects.IntegrationTests;

public sealed class GitProgressDescriptionDiagnosticsV33Tests
{
    [Theory]
    [InlineData("", GitProgressMeaningPolicy.DescriptionIssue.InvalidLength)]
    [InlineData("This adds something.", GitProgressMeaningPolicy.DescriptionIssue.InvalidLength)]
    [InlineData("Đã hoàn thành toàn bộ hệ thống và đảm bảo an toàn bảo mật.", GitProgressMeaningPolicy.DescriptionIssue.UnsupportedClaim)]
    [InlineData("The class Analyzer is defined and nothing more.", GitProgressMeaningPolicy.DescriptionIssue.Boilerplate)]
    [InlineData("A dependency between this route and its handler exists in the diff.", GitProgressMeaningPolicy.DescriptionIssue.MissingAction)]
    [InlineData("Bổ sung route POST /progress để nhận yêu cầu phân tích tiến độ.", GitProgressMeaningPolicy.DescriptionIssue.None)]
    public void Identifies_description_rejection_condition(string description, GitProgressMeaningPolicy.DescriptionIssue expected)
    {
        Assert.Equal(expected, GitProgressMeaningPolicy.CheckDescription(description));
        Assert.Equal(expected == GitProgressMeaningPolicy.DescriptionIssue.None,
            GitProgressMeaningPolicy.UsefulDescription(description));
    }

    [Fact]
    public void Rejection_breakdown_sums_to_weak_description_count()
    {
        const string evidence = "+api.MapPost(\"/progress\", Start);";
        var file = new ProgressFile("backend/StudentProjects.Api/Endpoints/ProgressEndpoints.cs",
            "added", 1, 0, evidence);
        var refs = new[] { new ProgressEvidenceReference("L1", file.Path, evidence) };
        var candidates = new[]
        {
            new ProgressEvidenceSelection("L1", "This adds something."),
            new ProgressEvidenceSelection("L1", "The class Analyzer is defined and nothing more."),
            new ProgressEvidenceSelection("L1", "A dependency between this route and its handler exists in the diff."),
            new ProgressEvidenceSelection("L1", "Bổ sung route POST /progress để nhận yêu cầu phân tích tiến độ.")
        };
        var result = GitProgressMeaningPolicy.ExplainRejectionsById(candidates, refs, [file]);
        Assert.Equal(3, result.WeakDescription);
        Assert.Equal(1, result.InvalidLength);
        Assert.Equal(0, result.UnsupportedClaim);
        Assert.Equal(1, result.Boilerplate);
        Assert.Equal(1, result.MissingAction);
        Assert.Equal(result.WeakDescription,
            result.InvalidLength + result.UnsupportedClaim + result.Boilerplate + result.MissingAction);
        Assert.Equal(1, result.Eligible);
    }
}
