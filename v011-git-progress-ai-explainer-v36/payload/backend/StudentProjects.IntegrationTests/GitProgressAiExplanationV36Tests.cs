using StudentProjects.Api.Services;
using Xunit;

namespace StudentProjects.IntegrationTests;

public sealed class GitProgressAiExplanationV36Tests
{
    private static readonly ProgressFeatureFact SampleFact = new(
        "API", "Thay đổi tuyến API",
        "Bổ sung khai báo POST /repositories/{id:guid}/analyses trong endpoint production; chưa chứng minh handler hoạt động.",
        [new ProgressFeatureCitation("backend/StudentProjects.Api/Endpoints/CodeAnalysisV11Endpoints.cs",
            "+api.MapPost(\"/repositories/{id:guid}/analyses\", Start);")]);

    [Fact]
    public void Explanation_retains_original_fact_id_and_never_constructs_evidence()
    {
        var output = GitProgressAiExplanationPolicy.Screen(
            [new("F1", "Việc bổ sung route POST /repositories/{id:guid}/analyses tạo điểm tiếp nhận yêu cầu phân tích mã nguồn.")],
            [SampleFact]);
        Assert.Single(output);
        Assert.Equal("F1", output[0].FactId);
        Assert.True(GitProgressAiExplanationPolicy.TryResolve("F1", [SampleFact], out var resolved));
        Assert.Same(SampleFact, resolved);
    }

    [Theory]
    [InlineData("F2")]
    [InlineData("F0")]
    [InlineData("F-1")]
    [InlineData("nothing")]
    public void Rejects_fake_or_out_of_range_fact_ids(string id)
    {
        Assert.Empty(GitProgressAiExplanationPolicy.Screen(
            [new(id, "Việc bổ sung route POST /repositories/{id:guid}/analyses tạo điểm tiếp nhận yêu cầu phân tích mã nguồn.")], [SampleFact]));
    }

    [Fact]
    public void Rejects_unseen_route_and_runtime_success_claim()
    {
        Assert.Empty(GitProgressAiExplanationPolicy.Screen(
            [new("F1", "Bổ sung route POST /admin/delete để xóa toàn bộ dữ liệu người dùng trong cơ sở dữ liệu.")], [SampleFact]));
        Assert.Empty(GitProgressAiExplanationPolicy.Screen(
            [new("F1", "Route POST /repositories/{id:guid}/analyses đã chạy thành công và kiểm thử thành công.")], [SampleFact]));
    }

    [Fact]
    public void Rejects_generic_praise_and_duplicate_model_items()
    {
        Assert.Empty(GitProgressAiExplanationPolicy.Screen(
            [new("F1", "Hệ thống đã được cải tiến vô cùng mạnh mẽ và tốt hơn nhiều về mọi mặt.")], [SampleFact]));
        var valid = "Bổ sung route POST /repositories/{id:guid}/analyses để nhận yêu cầu phân tích mã nguồn.";
        var output = GitProgressAiExplanationPolicy.Screen(
            [new("F1", valid), new("F1", valid)], [SampleFact]);
        Assert.Single(output);
    }

    [Fact]
    public void Invalid_item_does_not_block_valid_second_item_for_same_fact()
    {
        var output = GitProgressAiExplanationPolicy.Screen(
            [new("F1", "ngắn"),
             new("F1", "Bổ sung route POST /repositories/{id:guid}/analyses để tiếp nhận yêu cầu phân tích mã nguồn.")],
            [SampleFact]);
        Assert.Single(output);
    }

    [Fact]
    public void Empty_features_produce_no_explanations()
    {
        Assert.Empty(GitProgressAiExplanationPolicy.Screen([new("F1", "Bổ sung route POST /progress để nhận dữ liệu.")], []));
    }
}
