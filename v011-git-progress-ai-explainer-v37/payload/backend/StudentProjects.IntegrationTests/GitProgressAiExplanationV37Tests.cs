using System.Text.Json;
using StudentProjects.Api.Services;
using Xunit;

namespace StudentProjects.IntegrationTests;

public sealed class GitProgressAiExplanationV37Tests
{
    private static readonly ProgressFeatureFact ApiFact = new(
        "API", "Thay đổi tuyến API",
        "Bổ sung khai báo POST /repositories/{id:guid}/analyses, GET /repositories/{id:guid}/analyses trong endpoint production; chưa chứng minh handler hoạt động.",
        [new ProgressFeatureCitation("backend/StudentProjects.Api/Endpoints/CodeAnalysisV11Endpoints.cs",
            "+api.MapPost(\"/repositories/{id:guid}/analyses\", Start);")]);

    private static readonly ProgressFeatureFact DatabaseFact = new(
        "CSDL", "Thay đổi cấu trúc lưu trữ dữ liệu",
        "Migration bổ sung khai báo tạo 3 bảng: CodeAnalysisRuns, CodeAnalysisFindings, CodeAnalysisReviews; cần đối chiếu đã áp dụng.",
        [new ProgressFeatureCitation("backend/StudentProjects.Infrastructure/Persistence/Migrations/V011.cs",
            "+name: \"CodeAnalysisRuns\",")]);

    [Fact]
    public void Rejects_paraphrase_of_api_routes_without_purpose()
    {
        var result = GitProgressAiExplanationPolicy.Screen(
            [new("F1", "Đã bổ sung khai báo API POST /repositories/{id:guid}/analyses, GET /repositories/{id:guid}/analyses trong endpoint production.")],
            [ApiFact]);
        Assert.Empty(result);
    }

    [Fact]
    public void Rejects_paraphrased_database_summary_even_with_de_de_clause()
    {
        var result = GitProgressAiExplanationPolicy.Screen(
            [new("F1", "Đã bổ sung migration để tạo 3 bảng: CodeAnalysisRuns, CodeAnalysisFindings, CodeAnalysisReviews.")],
            [DatabaseFact]);
        Assert.Empty(result);
    }

    [Fact]
    public void Accepts_useful_purpose_for_api_with_known_route()
    {
        var result = GitProgressAiExplanationPolicy.Screen(
            [new("F1", "Các tuyến POST /repositories/{id:guid}/analyses giúp tiếp nhận yêu cầu phân tích mã nguồn để frontend có điểm gửi yêu cầu.")],
            [ApiFact]);
        Assert.Single(result);
    }

    [Fact]
    public void Rejects_repeated_words_and_generic_benefits()
    {
        Assert.Empty(GitProgressAiExplanationPolicy.Screen(
            [new("F1", "Các tuyến phân tích giúp giúp người dùng gửi yêu cầu phân tích và tra cứu lịch sử.")], [ApiFact]));
        Assert.Empty(GitProgressAiExplanationPolicy.Screen(
            [new("F1", "Các tuyến API giúp cải thiện trải nghiệm hệ thống và nâng cao chất lượng chung.")], [ApiFact]));
    }

    [Fact]
    public void Rejects_unverified_technology_even_with_grounded_route()
    {
        var output = GitProgressAiExplanationPolicy.Screen(
            [new("F1", "Các tuyến POST /repositories/{id:guid}/analyses giúp gửi yêu cầu đến Redis để xử lý dữ liệu phân tích.")],
            [ApiFact]);
        Assert.Empty(output);
    }

    [Fact]
    public void Six_rule_facts_with_six_ai_notes_remain_six_work_items()
    {
        var facts = Enumerable.Repeat(ApiFact, 6).ToArray();
        var explanations = Enumerable.Range(1, 6)
            .Select(i => new ProgressFeatureExplanation($"F{i}", $"Diễn giải tham khảo nhóm {i}"))
            .ToArray();
        var result = GitProgressFeatureReport.Build(facts, explanations);
        Assert.Equal(6, result.Count);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result));
        Assert.Equal(6, json.RootElement.GetArrayLength());
        Assert.All(json.RootElement.EnumerateArray(), row =>
        {
            Assert.Equal("feature-rule", row.GetProperty("source").GetString());
            Assert.NotEqual(JsonValueKind.Null, row.GetProperty("aiExplanation").ValueKind);
        });
    }

    [Fact]
    public void Evidence_json_contains_one_group_with_nested_explanation_not_two_groups()
    {
        var explanation = new ProgressFeatureExplanation("F1", "Các tuyến POST /repositories/{id:guid}/analyses giúp tiếp nhận yêu cầu phân tích mã nguồn để frontend có điểm gửi yêu cầu.");
        var rows = GitProgressFeatureReport.Build([ApiFact], [explanation]);
        Assert.Single(rows);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(rows));
        var one = json.RootElement[0];
        Assert.Equal("feature-rule", one.GetProperty("source").GetString());
        Assert.Equal("F1", one.GetProperty("factId").GetString());
        Assert.Equal(explanation.Explanation, one.GetProperty("aiExplanation").GetString());
        Assert.Single(one.GetProperty("citations").EnumerateArray());
    }

    [Fact]
    public void Missing_explanation_retains_original_fact_and_citation()
    {
        var rows = GitProgressFeatureReport.Build([ApiFact], []);
        Assert.Single(rows);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(rows));
        var one = json.RootElement[0];
        Assert.Equal(JsonValueKind.Null, one.GetProperty("aiExplanation").ValueKind);
        Assert.Contains("CodeAnalysisV11Endpoints.cs", one.GetProperty("file").GetString());
    }
}
