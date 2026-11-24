namespace StudentProjects.Domain.Entities;

public static class GitProgressRunHistory
{
    /// <summary>One latest saved run per ordered, full-SHA pair and repository/project. Model versions do not create separate items.</summary>
    public static IReadOnlyList<GitProgressRun> Latest(IEnumerable<GitProgressRun> runs) => runs
        .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.CompletedAt).ThenByDescending(r => r.Id)
        .GroupBy(r => (r.ProjectId, r.RepositoryLinkId, Base: r.BaseSha.ToLowerInvariant(), Head: r.HeadSha.ToLowerInvariant()))
        .Select(group => group.First()).ToArray();

    public static bool CanRemove(GitProgressRun run) => run.Status == "FAILED";
}
