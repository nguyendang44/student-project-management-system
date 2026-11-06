using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using StudentProjects.Api.Services;
using Xunit;

namespace StudentProjects.IntegrationTests;

public sealed class GitProgressMeaningfulTests
{
    [Fact]
    public void Selects_multiple_work_areas_instead_of_one_large_service()
    {
        var files = new[]
        {
            File("backend/StudentProjects.Api/Services/LargeService.cs", "+return await client.SendAsync(request);"),
            File("backend/StudentProjects.Api/Services/OtherService.cs", "+return await client.PostAsync(url, payload);"),
            File("backend/StudentProjects.Api/Endpoints/ProgressEndpoints.cs", "+api.MapPost(\"/progress\", Start);") ,
            File("frontend/src/views/ProgressView.vue", "+<button @click=\"start\">Phân tích</button>"),
            File("backend/StudentProjects.IntegrationTests/ProgressTests.cs", "+Assert.Equal(2, result.Count);") ,
            File("backend/StudentProjects.Infrastructure/Persistence/Migrations/NewProgress.cs", "+migrationBuilder.CreateTable(name: \"GitProgressRuns\");")
        };
        var selected = GitProgressMeaningPolicy.SelectFiles(files, 6);
        Assert.Equal(6, selected.Count);
        Assert.Contains(selected, f => GitProgressMeaningPolicy.AreaForPath(f.Path) == "API");
        Assert.Contains(selected, f => GitProgressMeaningPolicy.AreaForPath(f.Path) == "Frontend");
        Assert.Contains(selected, f => GitProgressMeaningPolicy.AreaForPath(f.Path) == "Kiểm thử");
        Assert.Contains(selected, f => GitProgressMeaningPolicy.AreaForPath(f.Path) == "CSDL");
    }

    [Fact]
    public void Does_not_send_comments_declarations_or_generated_migrations()
    {
        var comments = string.Join('\n', Enumerable.Repeat("+/// dependency heuristic, NOT a call graph", 32));
        var source = File("backend/StudentProjects.Api/Services/AiRelatedFileSelector.cs",
            comments + "\n+public static class AiRelatedFileSelector\n+return await analyzer.AnalyzeAsync(diff);");
        Assert.Equal(new[] { "+return await analyzer.AnalyzeAsync(diff);" },
            GitProgressMeaningPolicy.MeaningfulLines(source));
        var designer = File("backend/StudentProjects.Infrastructure/Persistence/Migrations/NewProgress.Designer.cs",
            "+migrationBuilder.CreateTable(name: \"Something\");");
        Assert.Empty(GitProgressMeaningPolicy.MeaningfulLines(designer));
        Assert.Empty(GitProgressMeaningPolicy.SelectFiles([File("README.md", "+release notes and implementation details") ]));
    }

    [Fact]
    public void Rejects_shallow_descriptions_even_with_valid_evidence_id()
    {
        var line = "+return await analyzer.AnalyzeAsync(diff);";
        var file = File("backend/StudentProjects.Api/Services/Analyzer.cs", line);
        var references = new[] { new ProgressEvidenceReference("L1", file.Path, line) };
        var candidates = new[]
        {
            new ProgressEvidenceSelection("L1", "The class Analyzer is defined."),
            new ProgressEvidenceSelection("L1", "The code snippet is a summary of the class."),
            new ProgressEvidenceSelection("L1", "Đã hoàn thành toàn bộ chức năng phân tích và chạy thành công."),
        };
        Assert.Empty(GitProgressMeaningPolicy.ScreenMeaningfulById(candidates, references, [file]));
    }

    [Fact]
    public void Accepts_specific_change_cited_with_an_exact_sent_line()
    {
        const string line = "+api.MapPost(\"/progress\", Start).RequireAuthorization();";
        var file = File("backend/StudentProjects.Api/Endpoints/ProgressEndpoints.cs", line);
        var references = new[] { new ProgressEvidenceReference("L1", file.Path, line) };
        var choices = new[] { new ProgressEvidenceSelection("L1",
            "Bổ sung route POST /progress trỏ đến hàm Start và yêu cầu xác thực ở endpoint này.") };
        var result = Assert.Single(GitProgressMeaningPolicy.ScreenMeaningfulById(choices, references, [file]));
        Assert.Equal(line, result.Evidence);
        Assert.Equal(file.Path, result.File);
    }

    [Fact]
    public void Rejects_forged_id_and_evidence_never_sent()
    {
        const string line = "+return await analyzer.AnalyzeAsync(diff);";
        var file = File("backend/StudentProjects.Api/Services/Analyzer.cs", line);
        var refs = new[]
        {
            new ProgressEvidenceReference("L1", file.Path, "+return await ForbiddenSecret();")
        };
        var choices = new[]
        {
            new ProgressEvidenceSelection("L99", "Bổ sung lời gọi AnalyzeAsync trong bộ phân tích theo diff."),
            new ProgressEvidenceSelection("L1", "Bổ sung lời gọi AnalyzeAsync trong bộ phân tích theo diff.")
        };
        Assert.Empty(GitProgressMeaningPolicy.ScreenMeaningfulById(choices, refs, [file]));
    }

    [Fact]
    public void Limits_two_observations_per_file_and_distributes_work_areas()
    {
        var api = File("backend/StudentProjects.Api/Endpoints/P.cs",
            "+api.MapGet(\"/x\", Read);\n+api.MapPost(\"/x\", Write);\n+api.MapPut(\"/x\", Update);");
        var ui = File("frontend/src/views/P.vue", "+<button @click=\"save\">Lưu</button>");
        var references = new[]
        {
            new ProgressEvidenceReference("L1", api.Path, "+api.MapGet(\"/x\", Read);"),
            new ProgressEvidenceReference("L2", api.Path, "+api.MapPost(\"/x\", Write);"),
            new ProgressEvidenceReference("L3", api.Path, "+api.MapPut(\"/x\", Update);"),
            new ProgressEvidenceReference("L4", ui.Path, "+<button @click=\"save\">Lưu</button>")
        };
        var choices = new[]
        {
            new ProgressEvidenceSelection("L1", "Bổ sung GET /x để kết nối hàm Read với endpoint."),
            new ProgressEvidenceSelection("L2", "Bổ sung POST /x để kết nối hàm Write với endpoint."),
            new ProgressEvidenceSelection("L3", "Bổ sung PUT /x để kết nối hàm Update với endpoint."),
            new ProgressEvidenceSelection("L4", "Bổ sung nút Lưu có sự kiện click gọi hàm save trên giao diện.")
        };
        var results = GitProgressMeaningPolicy.ScreenMeaningfulById(choices, references, [api, ui]);
        Assert.Equal(3, results.Count);
        Assert.Equal(2, results.Count(x => x.File == api.Path));
        Assert.Contains(results, x => x.File == ui.Path);
    }

    [Fact]
    public void Keeps_rejected_secret_lines_out_of_model_evidence()
    {
        var file = File("backend/StudentProjects.Api/Services/Service.cs",
            "+var password = \"not-a-real-token-123\";\n+return await analyzer.AnalyzeAsync(diff);");
        var lines = GitProgressMeaningPolicy.MeaningfulLines(file);
        Assert.Single(lines);
        Assert.Equal("+return await analyzer.AnalyzeAsync(diff);", lines[0]);
    }

    [Fact]
    public async Task Ollama_is_prompted_with_multiple_work_areas_and_versioned_analysis_key()
    {
        var handler = new StubOllama();
        var client = new AiGitProgressClient(new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:11434/") },
            new ConfigurationBuilder().Build());
        var diff = new ProgressDiff(new string('a', 40), new string('b', 40), 1,
        [
            File("backend/StudentProjects.Api/Endpoints/P.cs", "+api.MapPost(\"/progress\", Start);"),
            File("frontend/src/views/P.vue", "+<button @click=\"start\">Phân tích</button>")
        ], false);
        var result = await client.AnalyzeAsync(diff, CancellationToken.None);
        Assert.Equal(2, result.SentFiles);
        Assert.Equal(1, result.CandidateCount);
        Assert.Single(result.Items);
        Assert.Contains("\"area\":\"Frontend\"", handler.Prompt);
        Assert.Contains("\"area\":\"API\"", handler.Prompt);
        Assert.Equal("qwen2.5-coder:7b|gp-meaningful-v3", client.AnalysisKey);
    }

    private static ProgressFile File(string path, string patch) => new(path, "added", 1, 0, patch);

    private sealed class StubOllama : HttpMessageHandler
    {
        public string Prompt { get; private set; } = "";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var payload = await request.Content!.ReadAsStringAsync(ct);
            using var parsed = JsonDocument.Parse(payload);
            Prompt = parsed.RootElement.GetProperty("prompt").GetString() ?? "";
            var inner = JsonSerializer.Serialize(new { items = new[] {
                new { evidenceId = "L1", description = "Bổ sung route POST /progress với hàm Start để nhận yêu cầu phân tích." }
            }});
            var body = JsonSerializer.Serialize(new { response = inner, done = true });
            return new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
