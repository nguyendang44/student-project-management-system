using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using StudentProjects.Api.Services;
using Xunit;

namespace StudentProjects.IntegrationTests;

public sealed class GitProgressDiagnosticsV411Tests
{
    [Fact]
    public async Task Candidates_dropped_by_two_per_file_limit_are_counted_separately()
    {
        const string path = "backend/StudentProjects.Api/Endpoints/ProgressEndpoints.cs";
        var lines = new[]
        {
            "+api.MapGet(\"/a\", ReadA);",
            "+api.MapPost(\"/b\", WriteB);",
            "+api.MapPut(\"/c\", UpdateC);",
            "+api.MapDelete(\"/d\", DeleteD);"
        };
        const string reply = """
            {"items":[
              {"evidenceId":"L1","description":"Bổ sung route GET /a để gọi hàm ReadA từ API."},
              {"evidenceId":"L2","description":"Bổ sung route POST /b để gọi hàm WriteB từ API."},
              {"evidenceId":"L3","description":"Bổ sung route PUT /c để gọi hàm UpdateC từ API."},
              {"evidenceId":"L4","description":"Bổ sung route DELETE /d để gọi hàm DeleteD từ API."}
            ]}
            """;
        using var handler = new FakeOllama(reply);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434/") };
        var ai = new AiGitProgressClient(http, new ConfigurationBuilder().Build());
        var diff = new ProgressDiff(new string('a', 40), new string('b', 40), 1,
            [new ProgressFile(path, "added", 4, 0, string.Join('\n', lines))], false);

        var result = await ai.AnalyzeAsync(diff, CancellationToken.None);
        Assert.Equal(4, result.CandidateCount);
        Assert.Equal(2, result.Diagnostics!.SelectionLimit);
        Assert.Equal(2, result.Diagnostics.GroupedFeatureDetail);
        Assert.Equal(0, result.Diagnostics.Eligible);
        Assert.Equal(4, result.Diagnostics.SelectionLimit +
            result.Diagnostics.GroupedFeatureDetail + result.Diagnostics.Eligible +
            result.Diagnostics.WeakDescription + result.Diagnostics.Duplicate);
    }

    [Fact]
    public void Commit_test_fact_cites_actual_added_Get_commits_OK_assertion()
    {
        const string path = "backend/StudentProjects.IntegrationTests/GitHubV10IntegrationTests.cs";
        const string commitLine =
            "+Assert.Equal(HttpStatusCode.OK, (await student.GetAsync($\"/api/v1/v10/repositories/{id}/commits\")).StatusCode);";
        var diff = new ProgressFile(path, "added", 4, 0, string.Join('\n', new[]
        {
            "+public async Task Student_can_link_and_view_but_other_student_cannot_access()",
            "+var response = await student.PostAsJsonAsync($\"/api/v1/v10/projects/{ProjectA}/repositories\", new { repositoryUrl = url });",
            "+Assert.Equal(HttpStatusCode.Created, response.StatusCode);",
            commitLine
        }));
        var fact = Assert.Single(GitProgressFeatureSynthesis.Extract([diff]));
        Assert.Contains(fact.Citations, c => c.Evidence == commitLine);
        Assert.Contains("GET /commits", fact.Description);
        Assert.Contains("không phải bằng chứng", fact.Description);
    }

    private sealed class FakeOllama(string reply) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { response = reply, done = true }),
                    Encoding.UTF8, "application/json")
            });
    }
}
