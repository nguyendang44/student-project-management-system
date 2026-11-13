using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using StudentProjects.Api.Services;
using Xunit;

namespace StudentProjects.IntegrationTests;

public sealed class GitProgressFeatureReportV44Tests
{
    [Fact]
    public async Task Groups_frontend_api_backend_and_test_evidence_into_one_login_feature()
    {
        const string response = """
        {"features":[{
          "name":"Đăng nhập bằng email và mật khẩu",
          "summary":"Sinh viên bổ sung luồng đăng nhập: giao diện thu thập email và mật khẩu, backend xác thực thông tin và tạo token trả về.",
          "steps":[
            {"detail":"Giao diện lấy email và mật khẩu người dùng nhập để tạo thông tin đăng nhập.","evidenceIds":["E1"]},
            {"detail":"Backend khai báo endpoint POST /auth/login để nhận yêu cầu đăng nhập.","evidenceIds":["E2"]},
            {"detail":"Dịch vụ backend kiểm tra mật khẩu nhập vào với mật khẩu đã băm của tài khoản.","evidenceIds":["E3"]},
            {"detail":"Backend tạo token cho tài khoản.","evidenceIds":["E4"]},
            {"detail":"Bổ sung test kiểm tra trường hợp đăng nhập hợp lệ trả về token.","evidenceIds":["E5"]}
          ]
        }]}
        """;
        var evidence = LoginEvidence();
        using var http = new HttpClient(new FakeOllama(response))
        { BaseAddress = new Uri("http://127.0.0.1:11434/") };
        var client = new AiGitProgressClient(http, new ConfigurationBuilder().Build());

        var report = await client.AnalyzeFeatureReportAsync(evidence, null, CancellationToken.None);

        var feature = Assert.Single(report.Items);
        Assert.Equal("Đăng nhập bằng email và mật khẩu", feature.Name);
        Assert.Equal(5, feature.Steps.Count);
        Assert.Contains("Frontend", feature.Components);
        Assert.Contains("Backend/API", feature.Components);
        Assert.Contains("Kiểm thử", feature.Components);
        Assert.Contains("Vue", feature.Technologies);
        Assert.Contains("C#/.NET", feature.Technologies);
        Assert.Contains("ASP.NET Minimal API", feature.Technologies);
        Assert.Contains("JWT/token", feature.Technologies);
        Assert.Contains("chưa có kết quả chạy test", feature.VerificationStatus.ToLowerInvariant());
        Assert.Equal("accepted", report.Status);
        var row = Assert.Single(GitProgressFeatureReport.BuildAi(report.Items));
        var json = JsonSerializer.Serialize(row);
        Assert.Contains("ai-feature-report", json);
        Assert.Contains("POST /auth/login", json);
    }

    [Fact]
    public void Rejects_unknown_evidence_ids_and_completion_claims()
    {
        var evidence = new[]
        {
            new ProgressEvidenceReference("E1", "backend/StudentProjects.Api/Endpoints/AuthEndpoints.cs",
                "+api.MapPost(\"/auth/login\", Login);")
        };
        var unknownIdCandidate = new[]
        {
            new ProgressFeatureReportCandidate("Đăng nhập", "Sinh viên bổ sung endpoint nhận yêu cầu đăng nhập.",
                [new ProgressFeatureReportStepCandidate("Backend nhận yêu cầu đăng nhập qua endpoint /auth/login.", ["E1", "FAKE"])])
        };
        var unknownIdResult = GitProgressFeatureReport.Screen(unknownIdCandidate, evidence, 1);
        Assert.Single(unknownIdResult.Items);
        Assert.Equal(1, unknownIdResult.RejectedEvidence);

        var completionClaim = new[]
        {
            new ProgressFeatureReportCandidate("Đăng nhập", "Sinh viên đã hoàn thành chức năng đăng nhập.",
                [new ProgressFeatureReportStepCandidate("Backend nhận yêu cầu đăng nhập qua endpoint /auth/login.", ["E1"])])
        };
        var completionResult = GitProgressFeatureReport.Screen(completionClaim, evidence, 1);
        Assert.Empty(completionResult.Items);
        Assert.Equal("filtered", completionResult.Status);
    }

    [Fact]
    public void Does_not_claim_password_authentication_from_a_login_route_alone()
    {
        var evidence = new[]
        {
            new ProgressEvidenceReference("E1", "backend/StudentProjects.Api/Endpoints/AuthEndpoints.cs",
                "+api.MapPost(\"/auth/login\", Login);")
        };
        var candidates = new[]
        {
            new ProgressFeatureReportCandidate("Đăng nhập", "Sinh viên bổ sung luồng đăng nhập và xác thực thông tin tài khoản.",
                [new ProgressFeatureReportStepCandidate("Backend xác thực thông tin đăng nhập qua endpoint /auth/login.", ["E1"])])
        };

        var result = GitProgressFeatureReport.Screen(candidates, evidence, 1);

        Assert.Empty(result.Items);
        Assert.Equal("filtered", result.Status);
    }

    [Fact]
    public void One_release_can_produce_multiple_independent_features()
    {
        var evidence = new[]
        {
            new ProgressEvidenceReference("E1", "backend/StudentProjects.Api/Endpoints/AuthEndpoints.cs",
                "+api.MapPost(\"/auth/login\", Login);"),
            new ProgressEvidenceReference("E2", "backend/StudentProjects.Api/Endpoints/AuthEndpoints.cs",
                "+api.MapPost(\"/auth/register\", Register);"),
            new ProgressEvidenceReference("E3", "backend/StudentProjects.Api/Services/AuthService.cs",
                "+var valid = passwordHasher.Verify(user.PasswordHash, input.Password);")
        };
        var candidates = new[]
        {
            new ProgressFeatureReportCandidate("Đăng nhập", "Sinh viên bổ sung endpoint login để tài khoản gửi thông tin đăng nhập.",
                [new ProgressFeatureReportStepCandidate("API nhận yêu cầu login qua route /auth/login.", ["E1"])]),
            new ProgressFeatureReportCandidate("Đăng ký tài khoản", "Sinh viên bổ sung API register để tạo tài khoản mới.",
                [new ProgressFeatureReportStepCandidate("API nhận yêu cầu đăng ký qua route /auth/register.", ["E2"])]),
        };

        var result = GitProgressFeatureReport.Screen(candidates, evidence, 2);

        Assert.Equal(2, result.Items.Count);
        Assert.Contains(result.Items, x => x.Name == "Đăng nhập");
        Assert.Contains(result.Items, x => x.Name == "Đăng ký tài khoản");
    }

    [Fact]
    public void Evidence_budget_is_shared_across_files_instead_of_crowding_out_later_files()
    {
        var refs = Enumerable.Range(0, 3).SelectMany(file => Enumerable.Range(0, 50).Select(line =>
            new ProgressEvidenceReference($"L{file}-{line}",
                $"backend/StudentProjects.Api/Services/Service{file}.cs",
                $"+public void Feature{line}() {{ }}"))).ToArray();

        var set = GitProgressFeatureReport.BuildEvidence(refs, []);

        Assert.Equal(150, set.Total);
        Assert.Equal(150, set.Items.Count);
        Assert.Equal(0, set.Omitted);
        Assert.Equal(3, set.Items.Select(x => x.File).Distinct().Count());
        Assert.Equal(set.Items.Count, set.Items.Select(x => x.Id).Distinct().Count());
    }

    [Fact]
    public void Feature_steps_keep_only_evidence_that_matches_the_claimed_operation()
    {
        var evidence = new[]
        {
            new ProgressEvidenceReference("E1", "frontend/src/features/github/github.api.ts",
                "+  link: (projectId: string, repositoryUrl: string) => request(`/repositories`),"),
            new ProgressEvidenceReference("E2", "frontend/src/views/GitHubV10View.vue",
                "+defineProps<{ moduleId?: string }>()"),
            new ProgressEvidenceReference("E3", "scripts/install-github-v010.cjs",
                "+function prepare(rel, data) { updates.set(rel, data); }"),
            new ProgressEvidenceReference("E4", "scripts/upgrade-github-v010.sql", "+SET XACT_ABORT ON;"),
            new ProgressEvidenceReference("E5", "backend/StudentProjects.Api/Endpoints/GitHubV10Endpoints.cs",
                "+api.MapGet(\"/repositories/{id:guid}/commits\", Commits);"),
            new ProgressEvidenceReference("E6", "backend/StudentProjects.Api/Endpoints/GitHubV10Endpoints.cs",
                "+api.MapDelete(\"/repositories/{id:guid}\", Unlink);")
        };
        var candidates = new[]
        {
            new ProgressFeatureReportCandidate("Tạo liên kết repository",
                "Sinh viên bổ sung phương thức frontend gọi API tạo liên kết repository.",
                [
                    new ProgressFeatureReportStepCandidate("API client bổ sung phương thức link để gửi yêu cầu tạo liên kết repository.", ["E1"]),
                    new ProgressFeatureReportStepCandidate("View hiển thị form tạo liên kết repository.", ["E2", "E3", "E4", "E5"])
                ]),
            new ProgressFeatureReportCandidate("Xóa liên kết repository",
                "Backend khai báo endpoint để nhận yêu cầu xóa liên kết repository.",
                [new ProgressFeatureReportStepCandidate("Backend khai báo route DELETE để gỡ liên kết repository.", ["E6", "E5", "E3", "E4"])])
        };

        var result = GitProgressFeatureReport.Screen(candidates, evidence, 2);

        Assert.Equal(2, result.Items.Count);
        var create = result.Items.Single(x => x.Name.StartsWith("Tạo", StringComparison.Ordinal));
        var createStep = Assert.Single(create.Steps);
        Assert.Equal("frontend/src/features/github/github.api.ts", Assert.Single(createStep.Citations).File);
        Assert.DoesNotContain(create.Citations, c => c.File.StartsWith("scripts/", StringComparison.Ordinal));
        Assert.DoesNotContain("SQL", create.Technologies);
        Assert.DoesNotContain("C#/.NET", create.Technologies);
        Assert.Equal("Frontend", Assert.Single(create.Components));
        var remove = result.Items.Single(x => x.Name.StartsWith("Xóa", StringComparison.Ordinal));
        Assert.All(remove.Citations, c => Assert.Contains("MapDelete", c.Evidence));

        var falseUiClaim = GitProgressFeatureReport.Screen(
            [new ProgressFeatureReportCandidate("Xóa liên kết repository",
                "Sinh viên bổ sung giao diện để xóa liên kết repository.",
                [new ProgressFeatureReportStepCandidate("View hiển thị form xóa liên kết repository.", ["E6"])])],
            evidence, 1);
        Assert.Empty(falseUiClaim.Items);
    }

    [Fact]
    public void Repository_list_and_detail_steps_reject_unrelated_endpoint_and_setup_lines()
    {
        var evidence = new[]
        {
            new ProgressEvidenceReference("E1", "backend/StudentProjects.Api/Endpoints/GitHubV10Endpoints.cs",
                "+    public static void MapGitHubV10Endpoints(this IEndpointRouteBuilder app)"),
            new ProgressEvidenceReference("E2", "backend/StudentProjects.Api/Program.cs",
                "+        http.Timeout = TimeSpan.FromSeconds(10);"),
            new ProgressEvidenceReference("E3", "backend/StudentProjects.Api/Services/GitHubPublicClient.cs",
                "+    Task<GitHubRepositoryInfo> GetRepositoryAsync(string owner, string repo, CancellationToken ct);"),
            new ProgressEvidenceReference("E4", "frontend/src/views/GitHubV10View.vue",
                "+const links = ref<GithubRepositoryLink[]>([])")
        };
        var candidates = new[]
        {
            new ProgressFeatureReportCandidate("Xem danh sách repository",
                "Sinh viên bổ sung chức năng hiển thị danh sách repository của dự án.",
                [new ProgressFeatureReportStepCandidate("View hiển thị danh sách repository.", ["E1", "E2", "E4"])]),
            new ProgressFeatureReportCandidate("Xem chi tiết repository",
                "Sinh viên bổ sung chức năng lấy thông tin chi tiết repository.",
                [new ProgressFeatureReportStepCandidate("Backend lấy chi tiết repository qua GetRepositoryAsync.", ["E3"])])
        };

        var result = GitProgressFeatureReport.Screen(candidates, evidence, 2);
        Assert.Empty(result.Items);
    }

    [Fact]
    public void Rejects_update_milestone_when_citations_only_show_topic_and_notifications()
    {
        var evidence = new[]
        {
            new ProgressEvidenceReference("E1", "backend/StudentProjects.Api/Endpoints/TopicEndpoints.cs",
                "+!(author.RoleId == RoleIds.Student && t.Status == TopicStatus.APPROVED),"),
            new ProgressEvidenceReference("E2", "backend/StudentProjects.Api/Services/V08NotificationEngine.cs",
                "+private static async Task RefreshRequestNotificationsAsync(StudentProjectsDbContext db,")
        };
        var candidate = new ProgressFeatureReportCandidate("UpdateMilestone",
            "This function updates an existing milestone and logs an audit event.",
            [new ProgressFeatureReportStepCandidate(
                "If the milestone is a student revision it sets the proposal status to PENDING_REVIEW.", ["E1"]),
             new ProgressFeatureReportStepCandidate("It logs an audit event.", ["E2"])]);
        Assert.Empty(GitProgressFeatureReport.Screen([candidate], evidence, 1).Items);
    }

    [Fact]
    public void Rejects_unrelated_branch_and_commit_endpoint_citations()
    {
        var evidence = new[]
        {
            new ProgressEvidenceReference("E1", "backend/StudentProjects.Api/Endpoints/GitHubV10Endpoints.cs",
                "+public static void MapGitHubV10Endpoints(this IEndpointRouteBuilder app)"),
            new ProgressEvidenceReference("E2", "backend/StudentProjects.Api/Endpoints/GitHubV10Endpoints.cs",
                "+api.MapGet(\"/repositories/{id:guid}/commits\", Commits);"),
            new ProgressEvidenceReference("E3", "frontend/src/views/GitHubV10View.vue",
                "+const activeLinkId = ref('')")
        };
        var branch = new ProgressFeatureReportCandidate("Xem danh sách branch",
            "Sinh viên bổ sung chức năng lấy danh sách branch từ GitHub.",
            [new ProgressFeatureReportStepCandidate("Tạo endpoint lấy danh sách branch.", ["E1", "E3"])]);
        var commits = new ProgressFeatureReportCandidate("Xem danh sách commit",
            "Sinh viên bổ sung API để lấy danh sách commit từ GitHub.",
            [new ProgressFeatureReportStepCandidate("Backend khai báo endpoint GET commits.", ["E1", "E2"])]);
        var result = GitProgressFeatureReport.Screen([branch, commits], evidence, 2);
        Assert.Single(result.Items);
        Assert.Single(result.Items[0].Citations);
        Assert.Contains("commits", result.Items[0].Citations[0].Evidence);
    }

    [Fact]
    public void Route_fallback_keeps_each_added_endpoint_separate_and_never_fabricates_implementation()
    {
        var file = new ProgressFile("backend/StudentProjects.Api/Endpoints/GitHubV10Endpoints.cs", "added", 4, 0,
            "@@ -0,0 +1,5 @@\n" +
            "+api.MapGet(\"/projects/{projectId:guid}/repositories\", List);\n" +
            "+api.MapPost(\"/projects/{projectId:guid}/repositories\", Link);\n" +
            "+api.MapGet(\"/repositories/{id:guid}/commits\", Commits);\n" +
            "+api.MapDelete(\"/repositories/{id:guid}\", Unlink);\n" +
            "+public static void MapGitHubV10Endpoints(this IEndpointRouteBuilder app)");
        var rows = GitProgressFeatureReport.BuildRouteFallback([file]);
        Assert.Equal(4, rows.Count);
        Assert.Equal(4, rows.Select(x => x.Name).Distinct().Count());
        Assert.All(rows, f =>
        {
            Assert.Single(f.Steps);
            Assert.Single(f.Citations);
            Assert.Contains("Map", f.Citations[0].Evidence);
            Assert.Contains("chưa", f.VerificationStatus, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("hoàn thành", f.Summary, StringComparison.OrdinalIgnoreCase);
        });
        Assert.Contains(rows, f => f.Name.Contains("commit", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(rows, f => f.Name.Contains("gỡ liên kết", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("diff-feature-report", JsonSerializer.Serialize(
            GitProgressFeatureReport.BuildAi(rows, "diff-feature-report")));
    }

    [Fact]
    public void Route_fallback_rejects_unrelated_code_tests_and_removed_lines()
    {
        var files = new[]
        {
            new ProgressFile("backend/StudentProjects.Api/Endpoints/Api.cs", "modified", 1, 1,
                "@@ -1,2 +1,2 @@\n-api.MapGet(\"/old\", Old);\n+var name = \"MapGet\";"),
            new ProgressFile("backend/StudentProjects.IntegrationTests/ApiTests.cs", "added", 1, 0,
                "@@ -0,0 +1 @@\n+api.MapGet(\"/test\", Test);"),
            new ProgressFile("scripts/install.cjs", "added", 1, 0,
                "@@ -0,0 +1 @@\n+api.MapGet(\"/install\", Install);")
        };
        Assert.Empty(GitProgressFeatureReport.BuildRouteFallback(files));
    }

    [Theory]
    [InlineData("SUPERVISION")]
    [InlineData("REVISION_REQUIRED")]
    [InlineData("REJECTED")]
    [InlineData("CANCELLED")]
    public void Request_row_highlight_does_not_prove_status_workflow(string status)
    {
        var source = new ProgressEvidenceReference("E1", "frontend/src/views/LecturerSupervisionView.vue",
            "+<tr v-for=\"r in requests\" :id=\"'request-' + r.id\" :class=\"{ 'request-target-row': route.query.requestId === r.id }\">");
        var candidate = new ProgressFeatureReportCandidate(
            "Cập nhật chức năng xử lý yêu cầu" + status,
            "Sinh viên đã cập nhật chức năng xử lý yêu cầu" + status + " cho giảng viên.",
            [new ProgressFeatureReportStepCandidate("Cập nhật trong LecturerSupervisionView.vue để xử lý yêu cầu.", ["E1"])]);
        Assert.Empty(GitProgressFeatureReport.Screen([candidate], [source], 1).Items);
    }

    [Fact]
    public void Date_helper_and_notification_are_not_deadline_update_evidence()
    {
        var evidence = new[]
        {
            new ProgressEvidenceReference("E1", "frontend/src/views/TopicsManagementView.vue",
                "+function periodTomorrowLocal(){"),
            new ProgressEvidenceReference("E2", "backend/StudentProjects.Api/Services/V08NotificationEngine.cs",
                "+foreach (var r in requests.Where(r => r.Status == RequestStatus.PENDING))")
        };
        var candidate = new ProgressFeatureReportCandidate("Cập nhật hạn nộp dự án",
            "Sinh viên bổ sung chức năng cập nhật hạn nộp dự án cho học sinh.",
            [new ProgressFeatureReportStepCandidate("Cập nhật logic service xử lý yêu cầu hạn nộp dự án.", ["E1", "E2"])]);
        Assert.Empty(GitProgressFeatureReport.Screen([candidate], evidence, 1).Items);
    }

    [Fact]
    public void Period_form_open_option_does_not_prove_creation()
    {
        var evidence = new[]
        {
            new ProgressEvidenceReference("E1", "frontend/src/views/TopicsManagementView.vue",
                "+<label>Trạng thái<select v-model=\"periodForm.isOpen\"><option :value=\"true\">Mở</option></select></label>"),
            new ProgressEvidenceReference("E2", "frontend/src/views/ProjectProgressV07View.vue",
                "+? 'Đã đề xuất ngày nộp mới. Milestone chờ giảng viên xác nhận lại'")
        };
        var candidate = new ProgressFeatureReportCandidate("Tạo và quản lý kỳ hạn nộp bài tập",
            "Sinh viên bổ sung chức năng tạo và quản lý kỳ hạn nộp bài tập cho các dự án.",
            [new ProgressFeatureReportStepCandidate("Tạo form nhập thông tin kỳ hạn nộp bài tập.", ["E1", "E2"])]);
        Assert.Empty(GitProgressFeatureReport.Screen([candidate], evidence, 1).Items);
    }

    [Fact]
    public void Actual_deadline_update_route_remains_eligible()
    {
        var evidence = new[]
        {
            new ProgressEvidenceReference("E1", "backend/StudentProjects.Api/Endpoints/PeriodEndpoints.cs",
                "+api.MapPatch(\"/projects/{projectId:guid}/deadline\", UpdateDeadline);")
        };
        var candidate = new ProgressFeatureReportCandidate("Cập nhật hạn nộp dự án",
            "Sinh viên bổ sung tuyến API PATCH để cập nhật hạn nộp dự án.",
            [new ProgressFeatureReportStepCandidate("Backend khai báo tuyến API PATCH để cập nhật hạn nộp dự án.", ["E1"])]);
        Assert.Single(GitProgressFeatureReport.Screen([candidate], evidence, 1).Items);
    }

    [Fact]
    public async Task Feature_report_batches_all_165_lines_instead_of_stopping_at_100()
    {
        const string response = "{\"features\":[]}";
        using var handler = new CountingOllama(response);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:11434/") };
        var client = new AiGitProgressClient(http, new ConfigurationBuilder().Build());
        var evidence = Enumerable.Range(1, 165).Select(x =>
            new ProgressEvidenceReference($"E{x}", "backend/StudentProjects.Api/Services/Example.cs",
                $"+public void Action{x}() {{ }}")).ToArray();
        var result = await client.AnalyzeFeatureReportAsync(evidence, null, CancellationToken.None);
        Assert.Equal(3, handler.Calls);
        Assert.Equal("empty", result.Status);
    }

    private sealed class CountingOllama(string generated) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { response = generated, done = true }),
                    Encoding.UTF8, "application/json")
            });
        }
    }

    private static IReadOnlyList<ProgressEvidenceReference> LoginEvidence() =>
    [
        new("E1", "frontend/src/views/LoginView.vue",
            "+const credentials = { email: email.value, password: password.value }"),
        new("E2", "backend/StudentProjects.Api/Endpoints/AuthEndpoints.cs",
            "+api.MapPost(\"/auth/login\", Login);"),
        new("E3", "backend/StudentProjects.Api/Services/AuthService.cs",
            "+var valid = passwordHasher.Verify(user.PasswordHash, input.Password);"),
        new("E4", "backend/StudentProjects.Api/Services/TokenService.cs",
            "+var token = jwtService.CreateToken(user);"),
        new("E5", "backend/StudentProjects.IntegrationTests/AuthTests.cs",
            "+public async Task Login_returns_token_for_valid_credentials()")
    ];

    private sealed class FakeOllama(string generated) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { response = generated, done = true }),
                    Encoding.UTF8, "application/json")
            });
    }
}
