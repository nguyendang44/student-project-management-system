using StudentProjects.Api.Services;
using Xunit;

namespace StudentProjects.IntegrationTests;

public sealed class GitProgressSourceQualityV39Tests
{
    private static ProgressFile File(string path, string patch) =>
        new(path, "modified", 1, 0, patch);

    [Theory]
    [InlineData("scripts/v010-backups/2026-10-30/frontend/src/app/router.ts")]
    [InlineData("scripts/v0.10-backup/frontend/src/app/router.ts")]
    [InlineData("frontend/backups/src/app/router.ts")]
    [InlineData("backend/backup/StudentProjects.Api/Endpoints/P.cs")]
    [InlineData("frontend/src/__backup__/views/OldView.vue")]
    [InlineData("backend/StudentProjects.Infrastructure/Persistence/Migrations/New.Designer.cs")]
    [InlineData("backend/StudentProjects.Infrastructure/Persistence/Migrations/StudentProjectsDbContextModelSnapshot.cs")]
    [InlineData("frontend/src/types/generated.g.ts")]
    public void Excludes_backup_and_generated_paths_from_evidence(string path)
    {
        Assert.False(GitProgressEvidencePolicy.SafePath(path));
    }

    [Fact]
    public void Keeps_active_source_even_when_file_name_mentions_backup()
    {
        Assert.True(GitProgressEvidencePolicy.SafePath(
            "backend/StudentProjects.Api/Services/BackupRestoreService.cs"));
        Assert.True(GitProgressEvidencePolicy.SafePath(
            "frontend/src/app/router.ts"));
    }

    [Fact]
    public void Legacy_meaningful_line_selector_excludes_scripts_from_production_facts()
    {
        var files = new[]
        {
            File("scripts/v010-backups/2026-10-30/frontend/src/app/router.ts",
                "+  path: mod.path.slice(1), name: mod.id,"),
            File("scripts/temporary_check.py", "+  result = await client.post('/progress')"),
            File("backend/StudentProjects.Api/Endpoints/GitHubV10Endpoints.cs",
                "+        api.MapGet(\"/repositories/{id:guid}/commits\", Commits);"),
            File("frontend/src/views/GitHubV10View.vue",
                "+      <select v-model=\"projectId\" :disabled=\"busy\">"),
        };
        var chosen = GitProgressMeaningPolicy.SelectFiles(files, 10);
        Assert.Equal(2, chosen.Count);
        Assert.All(chosen, f => Assert.True(f.Path.StartsWith("backend/") ||
                                                f.Path.StartsWith("frontend/src/")));
    }

    [Fact]
    public void Feature_summary_does_not_use_router_from_archived_tree()
    {
        var files = new[]
        {
            File("frontend/src/views/GitHubV10View.vue",
                "+      <select v-model=\"projectId\" :disabled=\"busy\">"),
            File("frontend/src/features/github/github.api.ts",
                "+  list: (id: string) => request<GithubRepositoryLink[]>(`/v10/projects/${id}/repositories`),"),
            File("scripts/v010-backups/2026-10-30/frontend/src/app/router.ts",
                "+  path: mod.path.slice(1), name: mod.id,"),
        };
        var result = Assert.Single(GitProgressFeatureSynthesis.Extract(files));
        Assert.Equal("Frontend", result.Area);
        Assert.DoesNotContain("cấu hình điều hướng", result.Description);
        Assert.Equal(2, result.Citations.Count);
        Assert.DoesNotContain(result.Citations, c => c.File.StartsWith("scripts/"));
    }

    [Fact]
    public void Test_group_prefers_behavioral_test_over_seed_and_its_login_assertion()
    {
        const string seed = "+    public async Task SeedAsync()";
        const string seedAssert = "+        Assert.Equal(HttpStatusCode.OK, login.StatusCode);";
        const string test = "+    public async Task Student_can_link_public_repository_and_list_commits()";
        const string testAssert = "+        Assert.Equal(HttpStatusCode.OK, commits.StatusCode);";
        var file = File("backend/StudentProjects.IntegrationTests/GitHubV10IntegrationTests.cs",
            string.Join('\n', seed, seedAssert, test, testAssert));
        var fact = Assert.Single(GitProgressFeatureSynthesis.Extract([file]));
        Assert.Equal("Kiểm thử", fact.Area);
        Assert.Contains("Student_can_link_public_repository", fact.Description);
        Assert.DoesNotContain("SeedAsync", fact.Description);
        Assert.Contains(fact.Citations, c => c.Evidence == test);
        Assert.Contains(fact.Citations, c => c.Evidence == testAssert);
        Assert.DoesNotContain(fact.Citations, c => c.Evidence == seedAssert);
    }

    [Fact]
    public void Only_a_setup_helper_and_login_assertion_do_not_count_as_added_test_case()
    {
        var file = File("backend/StudentProjects.IntegrationTests/GitHubV10IntegrationTests.cs", """
            +    public async Task SeedAsync()
            +        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            """);
        Assert.DoesNotContain(GitProgressFeatureSynthesis.Extract([file]), f => f.Area == "Kiểm thử");
    }

    [Fact]
    public void Legitimate_test_still_produces_exact_added_line_citations()
    {
        var file = File("backend/StudentProjects.IntegrationTests/AuthorizationTests.cs", """
            +    public void Anonymous_login_route_is_allowed()
            +        Assert.True(result.AllowAnonymous);
            """);
        var fact = Assert.Single(GitProgressFeatureSynthesis.Extract([file]));
        Assert.All(fact.Citations, c =>
            Assert.Contains(c.Evidence, GitProgressEvidencePolicy.EvidenceLines(file, 500)));
        Assert.DoesNotContain(fact.Citations, c => !c.Evidence.StartsWith('+'));
    }

    [Fact]
    public void Scripts_outside_application_are_not_reported_as_production_features()
    {
        var file = File("scripts/archived/APIEndpoints.cs",
            "+        api.MapPost(\"/fake\", Start);");
        Assert.Empty(GitProgressFeatureSynthesis.Extract([file]));
    }
}
