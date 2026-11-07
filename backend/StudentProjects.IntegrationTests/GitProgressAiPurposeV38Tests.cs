using StudentProjects.Api.Services;
using Xunit;

namespace StudentProjects.IntegrationTests;

/// <summary>Regression tests for two real, low-information v3.7 explanations.</summary>
public sealed class GitProgressAiPurposeV38Tests
{
    private static readonly ProgressFeatureFact ApiFact = new(
        "API", "Thay đổi tuyến API",
        "Bổ sung khai báo POST /repositories/{id:guid}/analyses, GET /repositories/{id:guid}/analyses, GET /repositories/{id:guid}/analyses/compare trong endpoint production.",
        [new ProgressFeatureCitation("backend/StudentProjects.Api/Endpoints/CodeAnalysisV11Endpoints.cs",
            "+api.MapPost(\"/repositories/{id:guid}/analyses\", Start);"),
         new ProgressFeatureCitation("backend/StudentProjects.Api/Endpoints/CodeAnalysisV11Endpoints.cs",
            "+api.MapGet(\"/repositories/{id:guid}/analyses/compare\", Compare);")]);

    private static readonly ProgressFeatureFact DatabaseFact = new(
        "CSDL", "Thay đổi cấu trúc lưu trữ dữ liệu",
        "Migration bổ sung khai báo tạo 3 bảng: CodeAnalysisRuns, CodeAnalysisFindings, CodeAnalysisReviews.",
        [new ProgressFeatureCitation("backend/StudentProjects.Infrastructure/Persistence/Migrations/V011.cs",
            "+name: \"CodeAnalysisRuns\","),
         new ProgressFeatureCitation("backend/StudentProjects.Infrastructure/Persistence/Migrations/V011.cs",
            "+name: \"CodeAnalysisFindings\","),
         new ProgressFeatureCitation("backend/StudentProjects.Infrastructure/Persistence/Migrations/V011.cs",
            "+name: \"CodeAnalysisReviews\",")]);

    [Fact]
    public void Rejects_generic_api_sentence_from_v37()
    {
        var result = GitProgressAiExplanationPolicy.Screen(
            [new("F1", "Để tạo các endpoint cho phân tích mã và so sánh các báo cáo phân tích.")], [ApiFact]);
        Assert.Empty(result);
    }

    [Fact]
    public void Rejects_generic_database_sentence_from_v37()
    {
        var result = GitProgressAiExplanationPolicy.Screen(
            [new("F1", "Để tạo cấu trúc cơ sở dữ liệu cho lưu trữ các báo cáo phân tích.")], [DatabaseFact]);
        Assert.Empty(result);
    }

    [Fact]
    public void Accepts_real_api_use_case()
    {
        var result = GitProgressAiExplanationPolicy.Screen(
            [new("F1", "Các tuyến API giúp frontend gửi yêu cầu phân tích, tra cứu lịch sử và đối chiếu báo cáo qua các điểm truy cập backend riêng.")],
            [ApiFact]);
        Assert.Single(result);
    }

    [Fact]
    public void Accepts_distinct_database_record_roles()
    {
        var result = GitProgressAiExplanationPolicy.Screen(
            [new("F1", "Ba bảng tách dữ liệu lần chạy phân tích, các phát hiện và nhận xét để phục vụ truy vấn từng phần của báo cáo.")],
            [DatabaseFact]);
        Assert.Single(result);
    }

    [Fact]
    public void Rejects_database_claim_that_only_repeats_store_reports()
    {
        var result = GitProgressAiExplanationPolicy.Screen(
            [new("F1", "Các bảng mới giúp frontend lưu báo cáo phân tích vào cơ sở dữ liệu để truy vấn báo cáo trong hệ thống.")],
            [DatabaseFact]);
        Assert.Empty(result);
    }

    [Fact]
    public void Still_rejects_unverified_route_and_technology()
    {
        Assert.Empty(GitProgressAiExplanationPolicy.Screen(
            [new("F1", "Route /admin/delete giúp frontend gửi yêu cầu phân tích và tra cứu lịch sử để đối chiếu báo cáo.")],
            [ApiFact]));
        Assert.Empty(GitProgressAiExplanationPolicy.Screen(
            [new("F1", "Các tuyến API giúp frontend gửi yêu cầu qua Redis, tra cứu lịch sử và đối chiếu báo cáo phân tích.")],
            [ApiFact]));
    }

    [Fact]
    public void Legacy_single_endpoint_request_flow_remains_accepted()
    {
        var oneRoute = new ProgressFeatureFact("API", "Thay đổi tuyến API",
            "Bổ sung khai báo POST /repositories/{id:guid}/analyses trong endpoint production.",
            [new ProgressFeatureCitation("backend/StudentProjects.Api/Endpoints/CodeAnalysisV11Endpoints.cs",
                "+api.MapPost(\"/repositories/{id:guid}/analyses\", Start);")]);
        Assert.Single(GitProgressAiExplanationPolicy.Screen(
            [new("F1", "Bổ sung route POST /repositories/{id:guid}/analyses để tiếp nhận yêu cầu phân tích mã nguồn.")],
            [oneRoute]));
    }

    [Fact]
    public void Rejected_explanation_does_not_remove_verified_git_fact()
    {
        var screened = GitProgressAiExplanationPolicy.Screen(
            [new("F1", "Để tạo cấu trúc cơ sở dữ liệu cho lưu trữ các báo cáo phân tích.")],
            [DatabaseFact]);
        var json = System.Text.Json.JsonSerializer.Serialize(
            GitProgressFeatureReport.Build([DatabaseFact], screened));
        Assert.Contains("CodeAnalysisRuns", json);
        Assert.Contains("\"aiExplanation\":null", json);
    }
}
