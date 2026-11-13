using StudentProjects.Api.Services;
using Xunit;

namespace StudentProjects.IntegrationTests;

public sealed class GitProgressFeatureReportV49Tests
{
    private const string GitHubEndpoint = "backend/StudentProjects.Api/Endpoints/GitHubV10Endpoints.cs";

    private static ProgressAiFeature ModelFeature(string title, string file, string line) =>
        new(title, "Sinh viên thay đổi mã nguồn và cấu hình liên quan.",
            [new ProgressFeatureReportStep("Đổi mã", [new ProgressFeatureCitation(file, line)])],
            [new ProgressFeatureCitation(file, line)], ["Backend/API"], ["C#/.NET"],
            "Chưa xác nhận chạy đúng", "Bằng chứng một file");

    [Fact]
    public void GitHub_routes_are_normalized_even_when_model_cites_generic_setup_lines()
    {
        var files = new[]
        {
            new ProgressFile(GitHubEndpoint, "added", 5, 0,
                "+api.MapGet(\"/projects/{projectId:guid}/repositories\", List);\n" +
                "+api.MapPost(\"/projects/{projectId:guid}/repositories\", Link);\n" +
                "+api.MapGet(\"/repositories/{id:guid}/commits\", Commits);\n" +
                "+api.MapGet(\"/repositories/{id:guid}/branches\", Branches);\n" +
                "+api.MapDelete(\"/repositories/{id:guid}\", Unlink);"),
            new ProgressFile("backend/StudentProjects.Api/Program.cs", "modified", 1, 0,
                "+builder.Services.AddMemoryCache();"),
            new ProgressFile("frontend/src/features/github/github.types.ts", "modified", 1, 0,
                "+export interface GithubCommitResult {")
        };
        var genericModel = ModelFeature("Tạo và xem danh sách repository",
            "backend/StudentProjects.Api/Program.cs", "+builder.Services.AddMemoryCache();");
        Assert.False(GitProgressFeatureReport.IsRouteOnlyReport([genericModel]));
        Assert.True(GitProgressFeatureReport.IsGitHubFeatureClaim(genericModel));

        var routes = GitProgressFeatureReport.BuildGitHubRouteReport(files);
        Assert.Equal(5, routes.Count);
        Assert.Equal(5, routes.Select(x => x.Name).Distinct().Count());
        Assert.Contains(routes, r => r.Name == "Khai báo API liên kết repository");
        Assert.Contains(routes, r => r.Name == "Khai báo API gỡ liên kết repository");
        Assert.Contains(routes, r => r.Name == "Khai báo API xem danh sách repository");
        Assert.Contains(routes, r => r.Name == "Khai báo API xem commit");
        Assert.Contains(routes, r => r.Name == "Khai báo API xem nhánh repository");
        Assert.All(routes, r => Assert.DoesNotContain(r.Citations,
            c => c.Evidence.Contains("AddMemoryCache", StringComparison.Ordinal) ||
                 c.Evidence.Contains("GithubCommitResult", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("Kiểm tra hạn nộp dự án",
        "Sinh viên bổ sung chức năng kiểm tra hạn nộp dự án cho người dùng.",
        "Giao diện hiển thị nút kiểm tra hạn nộp dự án.",
        "+<button v-else type=\"button\" :disabled=\"saving\" @click=\"beginDeadlineEdit(selected)\">Sửa ngày</button>")]
    [InlineData("Kiểm tra hạn nộp dự án cho người dùng khác",
        "Sinh viên bổ sung chức năng kiểm tra hạn nộp dự án cho người dùng khác.",
        "Giao diện hiển thị nút kiểm tra hạn nộp dự án cho người dùng khác.",
        "+<h4>Xác nhận kế hoạch hoặc ngày nộp Milestone</h4>")]
    public void Deadline_editor_button_or_heading_is_not_a_deadline_check_feature(
        string name, string summary, string detail, string code)
    {
        var refs = new[] { new ProgressEvidenceReference("E1",
            "frontend/src/views/ProjectProgressV07View.vue", code) };
        var candidates = new[] { new ProgressFeatureReportCandidate(name, summary,
            [new ProgressFeatureReportStepCandidate(detail, ["E1"])]) };
        Assert.Empty(GitProgressFeatureReport.Screen(candidates, refs, 1).Items);
    }

    [Fact]
    public void Deadline_api_feature_drops_unrelated_contract_properties()
    {
        var refs = new[]
        {
            new ProgressEvidenceReference("E1",
                "backend/StudentProjects.Api/Endpoints/ProjectProgressV07Endpoints.cs",
                "+api.MapPut(\"/milestones/{id:guid}/deadline\", UpdateStudentDeadline);"),
            new ProgressEvidenceReference("E2",
                "backend/StudentProjects.Application/Contracts/TopicContracts.cs",
                "+string Status, bool IsRegistrationOpen, DateTimeOffset CreatedAt, Guid? ReservedForStudentUserId, bool CanDelete,")
        };
        var model = new ProgressFeatureReportCandidate("Cập nhật hạn nộp dự án",
            "Sinh viên bổ sung API để cập nhật hạn nộp dự án.",
            [new ProgressFeatureReportStepCandidate(
                "Khai báo endpoint cập nhật hạn nộp dự án.", ["E1"]),
             new ProgressFeatureReportStepCandidate(
                "Cập nhật contract để định nghĩa các thuộc tính liên quan đến hạn nộp.", ["E2"])]);
        var feature = Assert.Single(GitProgressFeatureReport.Screen([model], refs, 1).Items);
        var step = Assert.Single(feature.Steps);
        Assert.Contains("MapPut", Assert.Single(step.Citations).Evidence);
        Assert.DoesNotContain(feature.Citations, c => c.File.EndsWith("TopicContracts.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void A_date_edit_button_can_be_reported_as_a_ui_control_not_validation()
    {
        var refs = new[] { new ProgressEvidenceReference("E1",
            "frontend/src/views/ProjectProgressV07View.vue",
            "+<button type=\"button\" @click=\"beginDeadlineEdit(selected)\">Sửa ngày</button>") };
        var model = new ProgressFeatureReportCandidate("Bổ sung nút sửa ngày",
            "Sinh viên bổ sung nút Sửa ngày để mở chế độ chỉnh sửa hạn nộp.",
            [new ProgressFeatureReportStepCandidate(
                "Giao diện bổ sung nút Sửa ngày để sửa hạn nộp.", ["E1"])]);
        Assert.Single(GitProgressFeatureReport.Screen([model], refs, 1).Items);
    }

    [Fact]
    public void Unrelated_model_feature_is_not_removed_by_github_normalization()
    {
        var feature = ModelFeature("Kiểm tra ngày không hợp lệ",
            "frontend/src/views/TopicsManagementView.vue",
            "+if (!Number.isFinite(start.getTime())) return;");
        Assert.False(GitProgressFeatureReport.IsGitHubFeatureClaim(feature));
    }
}
