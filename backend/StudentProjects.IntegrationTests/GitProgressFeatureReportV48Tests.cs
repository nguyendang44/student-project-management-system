using StudentProjects.Api.Services;
using Xunit;

namespace StudentProjects.IntegrationTests;

public sealed class GitProgressFeatureReportV48Tests
{
    private static ProgressEvidenceReference Ref(string id, string path, string line) => new(id, path, line);

    [Fact]
    public void Route_only_model_groups_are_split_by_actual_operation_and_enriched_with_matching_code()
    {
        const string backend = "backend/StudentProjects.Api/Endpoints/GitHubV10Endpoints.cs";
        var files = new[]
        {
            new ProgressFile(backend, "added", 7, 0,
                "+api.MapGet(\"/projects/{projectId:guid}/repositories\", List);\n" +
                "+api.MapPost(\"/projects/{projectId:guid}/repositories\", Link);\n" +
                "+api.MapGet(\"/repositories/{id:guid}/commits\", Commits);\n" +
                "+api.MapDelete(\"/repositories/{id:guid}\", Unlink);\n" +
                "+private static async Task<IResult> Link(Guid projectId)\n" +
                "+db.Repositories.Add(link);\n" +
                "+private static async Task<IResult> Unlink(Guid id)\n" +
                "+db.Repositories.Remove(link);"),
            new ProgressFile("frontend/src/features/github/github.api.ts", "modified", 2, 0,
                "+  list: (projectId: string) => request(`/v10/projects/${projectId}/repositories`),\n" +
                "+  link: (projectId: string, repositoryUrl: string) => request(`/v10/projects/${projectId}/repositories`),"),
            new ProgressFile("frontend/src/views/GitHubV10View.vue", "added", 1, 0,
                "+async function link() {")
        };
        var oldAiGroups = GitProgressFeatureReport.BuildRouteFallback([files[0]]);
        Assert.True(GitProgressFeatureReport.IsRouteOnlyReport(oldAiGroups));

        var result = GitProgressFeatureReport.BuildRouteEvidenceReport(files);
        Assert.Equal(4, result.Count);
        Assert.Equal(4, result.Select(x => x.Name).Distinct().Count());
        var create = Assert.Single(result.Where(x => x.Name == "Khai báo API liên kết repository"));
        Assert.Contains(create.Citations, x => x.Evidence.Contains("MapPost", StringComparison.Ordinal));
        Assert.Contains(create.Citations, x => x.Evidence.Contains("db.Repositories.Add", StringComparison.Ordinal));
        Assert.Contains(create.Citations, x => x.File.EndsWith("github.api.ts", StringComparison.Ordinal));
        Assert.DoesNotContain(create.Citations, x => x.Evidence.Contains("MapGet", StringComparison.Ordinal));
        var remove = Assert.Single(result.Where(x => x.Name.Contains("gỡ liên kết", StringComparison.OrdinalIgnoreCase)));
        Assert.Contains(remove.Citations, x => x.Evidence.Contains("MapDelete", StringComparison.Ordinal));
        Assert.Contains(remove.Citations, x => x.Evidence.Contains("db.Repositories.Remove", StringComparison.Ordinal));
        Assert.DoesNotContain("hoàn thành", remove.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Xử lý lỗi ngày không hợp lệ", "Bổ sung kiểm tra ngày bắt đầu không hợp lệ so với ngày kết thúc.",
        "Kiểm tra ngày bắt đầu không thể sau ngày kết thúc.",
        "+if(!Number.isFinite(start.getTime())||!Number.isFinite(end.getTime())){error.value='Ngày giờ không hợp lệ.';return}")]
    [InlineData("Tạo và cập nhật chủ đề", "Sinh viên thêm chức năng tạo và cập nhật chủ đề.",
        "Tạo chủ đề mới bằng biểu mẫu.", "+<select v-model=\"jointLecturerId\" required>")]
    [InlineData("Tạo và cập nhật lịch trình", "Sinh viên thêm chức năng tạo và cập nhật lịch trình cho chủ đề.",
        "Tạo mới lịch trình theo ngày bắt đầu và kết thúc.", "+const end = new Date(`${periodForm.endsDate}T${periodForm.endsHour}:00:00`)")]
    [InlineData("Tạo và quản lý kỳ hạn", "Sinh viên thêm chức năng tạo và quản lý kỳ hạn cho đề tài.",
        "Thêm biểu mẫu để tạo kỳ hạn.", "+<form class=\"user-form\" @submit.prevent=\"createPeriod\">")]
    [InlineData("Tạo và quản lý đề tài", "Sinh viên đã thêm chức năng tạo và quản lý đề tài.",
        "Hiển thị danh sách đề tài và cho phép chỉnh sửa.", "+: result.requiresReapproval")]
    public void Model_cannot_call_unrelated_ui_or_date_setup_a_completed_feature(
        string name, string summary, string detail, string code)
    {
        var refs = new[] { Ref("E1", "frontend/src/views/TopicsManagementView.vue", code) };
        var candidate = new ProgressFeatureReportCandidate(name, summary,
            [new ProgressFeatureReportStepCandidate(detail, ["E1"])]);
        Assert.Empty(GitProgressFeatureReport.Screen([candidate], refs, 1).Items);
    }

    [Fact]
    public void Valid_date_parse_check_is_not_misnamed_as_a_start_end_comparison()
    {
        var refs = new[] { Ref("E1", "frontend/src/views/TopicsManagementView.vue",
            "+if (!Number.isFinite(start.getTime())) { error.value = 'Ngày giờ không hợp lệ'; return; }") };
        var model = new ProgressFeatureReportCandidate("Kiểm tra ngày không hợp lệ",
            "Bổ sung kiểm tra định dạng thời gian và báo lỗi khi không thể chuyển ngày.",
            [new ProgressFeatureReportStepCandidate("Kiểm tra ngày không hợp lệ trước khi tiếp tục xử lý.", ["E1"])]);
        Assert.Single(GitProgressFeatureReport.Screen([model], refs, 1).Items);
    }

    [Fact]
    public void Deadline_route_does_not_prove_lecturer_role()
    {
        var refs = new[] { Ref("E1", "backend/StudentProjects.Api/Endpoints/ProjectProgressV07Endpoints.cs",
            "+api.MapPut(\"/milestones/{id:guid}/deadline\", UpdateStudentDeadline);") };
        var model = new ProgressFeatureReportCandidate("Cập nhật hạn nộp dự án",
            "Bổ sung endpoint cập nhật hạn nộp dự án cho giảng viên.",
            [new ProgressFeatureReportStepCandidate("Khai báo endpoint cập nhật hạn nộp dự án.", ["E1"])]);
        Assert.Empty(GitProgressFeatureReport.Screen([model], refs, 1).Items);
    }
}
