using StudentProjects.Api.Services;
using Xunit;

namespace StudentProjects.IntegrationTests;

public sealed class GitProgressDescriptionV32Tests
{
    [Theory]
    [InlineData("This change configures an HTTP client for GitHub API requests and commit lookups.")]
    [InlineData("The service exposes a POST endpoint that invokes the Start handler for progress analysis.")]
    [InlineData("Thiết lập tuyến POST /progress để tiếp nhận yêu cầu phân tích tiến độ Git.")]
    [InlineData("Thực hiện truy xuất danh sách commit từ GitHub bằng yêu cầu HTTP.")]
    public void Accepts_meaningful_behavioral_paraphrases_in_two_languages(string description)
    {
        Assert.True(GitProgressMeaningPolicy.UsefulDescription(description));
    }

    [Theory]
    [InlineData("The class AiRelatedFileSelector is defined.")]
    [InlineData("The code snippet is a summary of the class and its methods.")]
    [InlineData("Đã hoàn thành toàn bộ hệ thống, đảm bảo an toàn bảo mật.")]
    [InlineData("This adds something.")]
    public void Still_rejects_boilerplate_overclaims_and_empty_observations(string description)
    {
        Assert.False(GitProgressMeaningPolicy.UsefulDescription(description));
    }

    [Fact]
    public void A_real_evidence_id_and_behavioral_description_are_both_required()
    {
        const string evidence = "+api.MapPost(\"/progress\", Start).RequireAuthorization();";
        var file = new ProgressFile("backend/StudentProjects.Api/Endpoints/ProgressEndpoints.cs", "added", 1, 0, evidence);
        var references = new[] { new ProgressEvidenceReference("L1", file.Path, evidence) };
        var accepted = GitProgressMeaningPolicy.ScreenMeaningfulById(
            [new ProgressEvidenceSelection("L1", "The API exposes a POST /progress route that invokes Start for analysis requests.")],
            references, [file]);
        Assert.Single(accepted);
        Assert.Equal(evidence, accepted[0].Evidence);
        Assert.Empty(GitProgressMeaningPolicy.ScreenMeaningfulById(
            [new ProgressEvidenceSelection("L999", "The API exposes a POST /progress route that invokes Start for analysis requests.")],
            references, [file]));
    }
}
