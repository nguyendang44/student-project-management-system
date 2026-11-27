using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using StudentProjects.Api.Endpoints;
using StudentProjects.Domain.Entities;
using StudentProjects.Domain.Enums;
using StudentProjects.Infrastructure.Persistence;
using Xunit;
using Api = StudentProjects.Api.Endpoints.V08Endpoints;

namespace StudentProjects.IntegrationTests;

// Actual handlers and EF InMemory. Does not connect to the development database.
public sealed class GuidanceWorkflowIntegrationTests
{
    [Fact]
    public async Task Student_resubmits_without_overwriting_feedback_and_confirmation_does_not_award_points()
    {
        await using var f = await Fixture.Create();
        Assert.Equal(201, await Call("CreateGuidance", f.Project.Id, f.Input, f.Student, f.Db, CancellationToken.None));
        var entry = await f.Db.GuidanceSessions.SingleAsync();
        Assert.Equal(200, await Call("ReviewGuidance", entry.Id,
            new Api.GuidanceReviewInput(false, "Cần bổ sung tài liệu", "Viết hướng dẫn chạy", entry.UpdatedAt), f.Lecturer, f.Db, CancellationToken.None));
        Assert.Equal("CHANGES_REQUESTED", GuidanceWorkflow.Status(entry));
        Assert.Equal(409, await Call("UpdateGuidance", entry.Id,
            f.Input with { ExpectedUpdatedAt = entry.UpdatedAt.AddMinutes(-1) }, f.Student, f.Db, CancellationToken.None));
        Assert.Equal(200, await Call("UpdateGuidance", entry.Id,
            f.Input with { Summary = "Đã bổ sung tài liệu", ExpectedUpdatedAt = entry.UpdatedAt }, f.Student, f.Db, CancellationToken.None));
        Assert.Equal("Cần bổ sung tài liệu", entry.Feedback);
        Assert.Equal("PENDING", GuidanceWorkflow.Status(entry));
        Assert.Equal(200, await Call("ReviewGuidance", entry.Id,
            new Api.GuidanceReviewInput(true, "Đã xem báo cáo", "Viết tài liệu cài đặt", entry.UpdatedAt), f.Lecturer, f.Db, CancellationToken.None));
        Assert.Equal(0, f.Milestone.PercentComplete);
        Assert.Equal(409, await Call("UpdateGuidance", entry.Id,
            f.Input with { ExpectedUpdatedAt = entry.UpdatedAt }, f.Student, f.Db, CancellationToken.None));
        var task = new Api.GuidanceTaskInput(f.Milestone.Id, "Viết tài liệu", "Có hướng dẫn cài đặt", 20, null, entry.UpdatedAt);
        Assert.Equal(201, await Call("CreateGuidanceTask", entry.Id, task, f.Lecturer, f.Db, CancellationToken.None));
        Assert.Equal(409, await Call("CreateGuidanceTask", entry.Id,
            task with { ExpectedUpdatedAt = entry.UpdatedAt }, f.Lecturer, f.Db, CancellationToken.None));
        Assert.Single(await f.Db.MilestoneCriteria.ToListAsync());
        Assert.Equal(0, f.Milestone.PercentComplete);
        Assert.Empty(await f.Db.MilestoneCriterionAssessments.ToListAsync());
    }

    [Fact]
    public async Task Rejects_spoofed_feedback_cross_project_links_and_non_owner_writes()
    {
        await using var f = await Fixture.Create();
        Assert.Equal(400, await Call("CreateGuidance", f.Project.Id, f.Input with { Feedback = "Tự xác nhận" }, f.Student, f.Db, CancellationToken.None));
        var other = new Milestone { ProjectId = Guid.NewGuid() }; f.Db.Milestones.Add(other); await f.Db.SaveChangesAsync();
        Assert.Equal(400, await Call("CreateGuidance", f.Project.Id, f.Input with { MilestoneId = other.Id }, f.Student, f.Db, CancellationToken.None));
        var outside = Fixture.User(Guid.NewGuid(), "Student");
        Assert.Equal(403, await Call("GuidanceContext", f.Project.Id, outside, f.Db, CancellationToken.None));
        Assert.Equal(403, await Call("CreateGuidance", f.Project.Id, f.Input, outside, f.Db, CancellationToken.None));
        Assert.Equal(201, await Call("CreateGuidance", f.Project.Id, f.Input, f.Student, f.Db, CancellationToken.None));
        var entry = await f.Db.GuidanceSessions.SingleAsync();
        Assert.Equal(403, await Call("ReviewGuidance", entry.Id,
            new Api.GuidanceReviewInput(true, "", "", entry.UpdatedAt), f.Student, f.Db, CancellationToken.None));
        Assert.Equal(400, await Call("ReviewGuidance", entry.Id,
            new Api.GuidanceReviewInput(false, "", "", entry.UpdatedAt), f.Lecturer, f.Db, CancellationToken.None));
        f.Project.Status = ProjectStatus.COMPLETED; await f.Db.SaveChangesAsync();
        Assert.Equal(409, await Call("CreateGuidance", f.Project.Id, f.Input, f.Student, f.Db, CancellationToken.None));
    }

    [Fact]
    public void Historic_confirmation_is_preserved_and_report_limits_are_enforced()
    {
        var entry = new GuidanceSession { LecturerConfirmedAt = DateTimeOffset.UtcNow, ReviewStatus = "PENDING" };
        Assert.Equal("CONFIRMED", GuidanceWorkflow.Status(entry));
        Assert.False(GuidanceWorkflow.CanResubmit(entry, entry.CreatedByUserId));
        Assert.False(GuidanceWorkflow.ValidReport(DateTimeOffset.UtcNow, " ", "", DateTimeOffset.UtcNow));
        Assert.False(GuidanceWorkflow.ValidReport(DateTimeOffset.UtcNow, new string('x', 4001), "", DateTimeOffset.UtcNow));
        Assert.False(GuidanceWorkflow.ValidReport(DateTimeOffset.UtcNow.AddDays(2), "Report", "", DateTimeOffset.UtcNow));
    }

    private static async Task<int?> Call(string name, params object?[] args)
    {
        var method = typeof(Api).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!;
        var result = await (Task<IResult>)method.Invoke(null, args)!;
        return result is ForbidHttpResult ? 403 : ((IStatusCodeHttpResult)result).StatusCode;
    }
    private sealed class Fixture : IAsyncDisposable
    {
        public StudentProjectsDbContext Db { get; } = new(new DbContextOptionsBuilder<StudentProjectsDbContext>()
            .UseInMemoryDatabase("guidance-" + Guid.NewGuid()).Options);
        public Project Project { get; } = new() { StudentUserId = Guid.NewGuid(), LecturerUserId = Guid.NewGuid(), Status = ProjectStatus.IN_PROGRESS };
        public Milestone Milestone { get; } = new();
        public ClaimsPrincipal Student => User(Project.StudentUserId, "Student");
        public ClaimsPrincipal Lecturer => User(Project.LecturerUserId, "Lecturer");
        public Api.GuidanceInput Input => new(DateTimeOffset.UtcNow, "Báo cáo tiến độ", null, null, Milestone.Id, null, "Cần hỗ trợ kiểm thử");
        public static async Task<Fixture> Create()
        {
            var f = new Fixture();
            await f.Db.Database.EnsureCreatedAsync();
            f.Milestone.ProjectId = f.Project.Id;
            f.Db.Projects.Add(f.Project); f.Db.Milestones.Add(f.Milestone); await f.Db.SaveChangesAsync();
            return f;
        }
        public static ClaimsPrincipal User(Guid id, string role) => new(new ClaimsIdentity(new[] {
            new Claim(ClaimTypes.NameIdentifier, id.ToString()), new Claim(ClaimTypes.Role, role) }, "test"));
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
