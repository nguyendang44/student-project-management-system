using StudentProjects.Domain.Entities;
using Xunit;

namespace StudentProjects.IntegrationTests;

public sealed class GitProgressRunHistoryTests
{
    [Fact]
    public void Same_full_pair_keeps_the_last_saved_model_version()
    {
        var old = Run("a", "b", DateTimeOffset.UtcNow.AddMinutes(-10));
        var recent = Run("a", "b", DateTimeOffset.UtcNow);
        recent.ModelName = "qwen2.5-coder:7b|gp-web-v5.2";
        Assert.Equal(recent.Id, GitProgressRunHistory.Latest([old, recent]).Single().Id);
    }

    [Fact]
    public void Repository_project_direction_and_full_sha_are_part_of_identity()
    {
        var first = Run("a", "b", DateTimeOffset.UtcNow);
        var otherRepo = Run("a", "b", first.CreatedAt); otherRepo.RepositoryLinkId = Guid.NewGuid();
        var otherProject = Run("a", "b", first.CreatedAt); otherProject.ProjectId = Guid.NewGuid();
        var reversed = Run("b", "a", first.CreatedAt);
        var samePrefix = Run("a", "b", first.CreatedAt); samePrefix.HeadSha = new string('b', 39) + "c";
        Assert.Equal(5, GitProgressRunHistory.Latest([first, otherRepo, otherProject, reversed, samePrefix]).Count);
    }

    [Fact]
    public void Sha_case_does_not_create_a_second_pair()
    {
        var old = Run("a", "b", DateTimeOffset.UtcNow.AddMinutes(-1));
        var recent = Run("A", "B", DateTimeOffset.UtcNow);
        Assert.Equal(recent.Id, GitProgressRunHistory.Latest([old, recent]).Single().Id);
    }

    [Fact]
    public void Deduplication_happens_before_the_history_display_limit()
    {
        var now = DateTimeOffset.UtcNow;
        var rows = Enumerable.Range(0, 50).Select(i => Run("a", "b", now.AddMinutes(-i))).ToList();
        rows.Add(Run("b", "c", now.AddDays(-1)));
        Assert.Equal(2, GitProgressRunHistory.Latest(rows).Take(40).Count());
    }

    [Fact]
    public void Failed_runs_can_be_removed_but_processing_and_completed_runs_cannot()
    {
        var run = Run("a", "b", DateTimeOffset.UtcNow);
        foreach (var status in new[] { "PENDING", "RUNNING", "COMPLETED" })
        { run.Status = status; Assert.Equal(false, GitProgressRunHistory.CanRemove(run)); }
        run.Status = "FAILED";
        Assert.Equal(true, GitProgressRunHistory.CanRemove(run));
    }

    private static GitProgressRun Run(string before, string after, DateTimeOffset created) => new()
    {
        BaseSha = string.Concat(Enumerable.Repeat(before, 40)), HeadSha = string.Concat(Enumerable.Repeat(after, 40)),
        CreatedAt = created, ModelName = "qwen2.5-coder:7b|gp-web-v5.1", Status = "COMPLETED"
    };
}
