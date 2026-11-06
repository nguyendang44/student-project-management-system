using StudentProjects.Api.Services;
using Xunit;

namespace StudentProjects.IntegrationTests;

public sealed class AiRelatedFileSelectorTests
{
    private static readonly string[] Paths =
    [
        "backend/StudentProjects.Api/Endpoints/AuthEndpoints.cs",
        "backend/StudentProjects.Api/Endpoints/GitHubV10Endpoints.cs",
        "backend/StudentProjects.Api/Endpoints/OtherEndpoints.cs",
        "backend/StudentProjects.Api/Services/GitHubPublicClient.cs",
        "backend/StudentProjects.Api/Program.cs",
        "backend/StudentProjects.Infrastructure/Persistence/StudentProjectsDbContext.cs",
        "backend/StudentProjects.Application/Contracts/AuthContracts.cs",
        "backend/StudentProjects.Application/Contracts/ApiContracts.cs",
        "backend/StudentProjects.IntegrationTests/GitHubV10IntegrationTests.cs",
        "frontend/src/features/auth/auth.api.ts",
        "frontend/src/features/auth/auth.types.ts",
        "frontend/src/features/github/github.api.ts",
        "frontend/src/views/GitHubV10View.vue",
        "frontend/src/api/client.ts",
        "frontend/src/App.vue",
    ];

    [Fact]
    public void Picks_related_api_frontend_contracts_and_shared_context()
    {
        var selected = AiRelatedFileSelector.SelectPaths(Paths);
        Assert.Contains("backend/StudentProjects.Api/Endpoints/AuthEndpoints.cs", selected);
        Assert.Contains("backend/StudentProjects.Application/Contracts/AuthContracts.cs", selected);
        Assert.Contains("frontend/src/features/auth/auth.api.ts", selected);
        Assert.Contains("backend/StudentProjects.Api/Endpoints/GitHubV10Endpoints.cs", selected);
        Assert.Contains("frontend/src/features/github/github.api.ts", selected);
        Assert.Contains("backend/StudentProjects.Api/Program.cs", selected);
        Assert.Contains("frontend/src/api/client.ts", selected);
        Assert.True(selected.Count <= 12);
    }

    [Fact]
    public void Same_input_different_order_selects_identical_paths()
    {
        var a = AiRelatedFileSelector.SelectPaths(Paths);
        var b = AiRelatedFileSelector.SelectPaths(Paths.Reverse());
        Assert.Equal(a.ToArray(), b.ToArray());
        Assert.Equal(a.Count, a.Distinct().Count());
    }

    [Fact]
    public void Does_not_invent_missing_files_or_exceed_limit()
    {
        var selected = AiRelatedFileSelector.SelectPaths(Paths.Take(4), maxFiles: 2);
        Assert.True(selected.Count <= 2);
        Assert.All(selected, p => Assert.Contains(p, Paths.Take(4)));
    }

    [Fact]
    public void Groups_related_files_even_when_they_belong_to_different_layers()
    {
        var chosen = AiRelatedFileSelector.SelectPaths(Paths);
        var files = chosen.Select(p => new SourceFile(p, "public class Sample { }")).ToArray();
        var groups = AiRelatedFileSelector.BuildReviewGroups(files, 4);
        var auth = Assert.Single(groups.Where(g => g.Name == "Auth"));
        Assert.Contains(auth.Files, f => f.Path.EndsWith("AuthEndpoints.cs"));
        Assert.Contains(auth.Files, f => f.Path.EndsWith("auth.api.ts"));
        Assert.Contains(auth.Files, f => f.Path.EndsWith("Program.cs"));
        Assert.DoesNotContain(auth.Files, f => f.Path.EndsWith("GitHubV10Endpoints.cs"));
    }

    [Fact]
    public void Does_not_confuse_project_namespace_with_project_feature()
    {
        Assert.Equal("Other", AiRelatedFileSelector.Feature(
            "backend/StudentProjects.Api/Endpoints/OtherEndpoints.cs"));
        Assert.Equal("Project", AiRelatedFileSelector.Feature(
            "backend/StudentProjects.Api/Endpoints/ProjectEndpoints.cs"));
    }

    [Fact]
    public void Skips_unavailable_shared_context()
    {
        var items = new[] { "backend/Api/Endpoints/AuthEndpoints.cs", "frontend/src/features/auth/auth.api.ts" };
        var chosen = AiRelatedFileSelector.SelectPaths(items);
        Assert.DoesNotContain(chosen, p => p.EndsWith("Program.cs"));
        Assert.Equal(2, chosen.Count);
    }
}
