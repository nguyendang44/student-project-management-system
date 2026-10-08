using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace StudentProjects.Api.Endpoints;

/// <summary>Explicit 501 stubs. The API surface is NOT a working business implementation.</summary>
public static class SkeletonEndpoints
{
    private static IResult Pending(string module, string useCases) =>
        Results.Json(new
        {
            code = "NOT_IMPLEMENTED", message = "Endpoint declared for architecture only; business logic is not implemented.",
            module, useCases = useCases.Split(',', StringSplitOptions.TrimEntries), nextStep = "Implement application service, access/ownership checks, persistence and tests."
        }, statusCode: StatusCodes.Status501NotImplemented);

    public static void MapSkeletonEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1");
        // Auth endpoints are implemented separately in AuthEndpoints.

        api.MapGet("/dashboard", () => Pending("dashboard", "UC-35")).RequireAuthorization(policy => policy.RequireRole("Student","Lecturer","Admin"));
        api.MapGet("/topics", () => Pending("topics", "UC-05,UC-06")).RequireAuthorization(policy => policy.RequireRole("Student","Lecturer","Admin"));
        api.MapPost("/topics", () => Pending("topics", "UC-05,UC-06")).RequireAuthorization(policy => policy.RequireRole("Student","Lecturer","Admin"));
        api.MapPatch("/topics/{id:guid}", () => Pending("topics", "UC-05,UC-06")).RequireAuthorization(policy => policy.RequireRole("Student","Lecturer","Admin"));
        api.MapGet("/topic-proposals", () => Pending("proposals", "UC-08,UC-09,UC-10")).RequireAuthorization(policy => policy.RequireRole("Student","Lecturer","Admin"));
        api.MapPost("/topic-proposals", () => Pending("proposals", "UC-08,UC-09,UC-10")).RequireAuthorization(policy => policy.RequireRole("Student"));
        api.MapPost("/topic-proposals/{id:guid}/approve", () => Pending("proposals", "UC-08,UC-09,UC-10")).RequireAuthorization(policy => policy.RequireRole("Lecturer"));
        api.MapGet("/topic-registrations", () => Pending("topicregistrations", "UC-07,UC-41")).RequireAuthorization(policy => policy.RequireRole("Student","Lecturer","Admin"));
        api.MapPost("/topic-registrations", () => Pending("topicregistrations", "UC-07,UC-41")).RequireAuthorization(policy => policy.RequireRole("Student"));
        api.MapGet("/lecturer-capacity", () => Pending("capacity", "UC-15,UC-16,UC-17")).RequireAuthorization(policy => policy.RequireRole("Student","Lecturer","Admin"));
        api.MapPut("/lecturer-capacity/me", () => Pending("capacity", "UC-15,UC-16,UC-17")).RequireAuthorization(policy => policy.RequireRole("Lecturer"));
        api.MapGet("/lecturer-requests", () => Pending("lecturerrequests", "UC-11,UC-12,UC-13,UC-14")).RequireAuthorization(policy => policy.RequireRole("Student","Lecturer","Admin"));
        api.MapPost("/lecturer-requests", () => Pending("lecturerrequests", "UC-11,UC-12,UC-13,UC-14")).RequireAuthorization(policy => policy.RequireRole("Student"));
        api.MapPost("/lecturer-requests/{id:guid}/accept", () => Pending("lecturerrequests", "UC-11,UC-12,UC-13,UC-14")).RequireAuthorization(policy => policy.RequireRole("Lecturer"));
        api.MapGet("/projects", () => Pending("projects", "UC-18,UC-19")).RequireAuthorization(policy => policy.RequireRole("Student","Lecturer","Admin"));
        api.MapGet("/projects/{id:guid}", () => Pending("projects", "UC-18,UC-19")).RequireAuthorization(policy => policy.RequireRole("Student","Lecturer","Admin"));
        api.MapGet("/milestones", () => Pending("milestones", "UC-20,UC-21")).RequireAuthorization(policy => policy.RequireRole("Student","Lecturer","Admin"));
        api.MapPost("/milestones", () => Pending("milestones", "UC-20,UC-21")).RequireAuthorization(policy => policy.RequireRole("Lecturer"));
        api.MapGet("/progress", () => Pending("progress", "UC-22,UC-42")).RequireAuthorization(policy => policy.RequireRole("Student","Lecturer","Admin"));
        api.MapPost("/progress", () => Pending("progress", "UC-22,UC-42")).RequireAuthorization(policy => policy.RequireRole("Student"));
        api.MapGet("/submissions", () => Pending("submissions", "UC-23,UC-24,UC-25")).RequireAuthorization(policy => policy.RequireRole("Student","Lecturer","Admin"));
        api.MapPost("/submissions", () => Pending("submissions", "UC-23,UC-24,UC-25")).RequireAuthorization(policy => policy.RequireRole("Student"));
        api.MapGet("/evaluations", () => Pending("evaluations", "UC-24,UC-25,UC-26")).RequireAuthorization(policy => policy.RequireRole("Lecturer","Admin"));
        api.MapPost("/evaluations", () => Pending("evaluations", "UC-24,UC-25,UC-26")).RequireAuthorization(policy => policy.RequireRole("Lecturer"));
        api.MapGet("/repositories", () => Pending("github", "UC-27,UC-28")).RequireAuthorization(policy => policy.RequireRole("Student","Lecturer","Admin"));
        api.MapPost("/repositories", () => Pending("github", "UC-27,UC-28")).RequireAuthorization(policy => policy.RequireRole("Student"));
        api.MapGet("/ai-analysis", () => Pending("ai", "UC-29,UC-30")).RequireAuthorization(policy => policy.RequireRole("Student","Lecturer","Admin"));
        api.MapPost("/ai-analysis", () => Pending("ai", "UC-29,UC-30")).RequireAuthorization(policy => policy.RequireRole("Student","Lecturer"));
        api.MapGet("/notifications", () => Pending("notifications", "UC-31,UC-32")).RequireAuthorization(policy => policy.RequireRole("Student","Lecturer","Admin"));
        api.MapGet("/statistics", () => Pending("statistics", "UC-36")).RequireAuthorization(policy => policy.RequireRole("Lecturer","Admin"));
        api.MapGet("/reports", () => Pending("reports", "UC-37")).RequireAuthorization(policy => policy.RequireRole("Admin"));
        api.MapGet("/registration-periods", () => Pending("periods", "UC-41")).RequireAuthorization(policy => policy.RequireRole("Admin"));
        api.MapPost("/registration-periods", () => Pending("periods", "UC-41")).RequireAuthorization(policy => policy.RequireRole("Admin"));
        api.MapGet("/automation/runs", () => Pending("automation", "UC-33,UC-34,UC-38")).RequireAuthorization(policy => policy.RequireRole("Admin"));
        api.MapGet("/audit-log", () => Pending("audit", "UC-43")).RequireAuthorization(policy => policy.RequireRole("Admin"));
        api.MapGet("/system-errors", () => Pending("errors", "UC-39")).RequireAuthorization(policy => policy.RequireRole("Admin"));
        api.MapGet("/settings", () => Pending("settings", "UC-40")).RequireAuthorization(policy => policy.RequireRole("Admin"));
        api.MapPut("/settings/{key}", () => Pending("settings", "UC-40")).RequireAuthorization(policy => policy.RequireRole("Admin"));
        api.MapPost("/topic-proposals/{id:guid}/reject", () => Pending("proposals", "UC-10")).RequireAuthorization(policy => policy.RequireRole("Lecturer","Admin"));
        api.MapPost("/lecturer-requests/{id:guid}/reject", () => Pending("lecturerrequests", "UC-14")).RequireAuthorization(policy => policy.RequireRole("Lecturer"));
        api.MapPost("/milestones/{id:guid}/approve", () => Pending("milestones", "UC-25")).RequireAuthorization(policy => policy.RequireRole("Lecturer"));
        api.MapPost("/milestones/{id:guid}/request-revision", () => Pending("milestones", "UC-24")).RequireAuthorization(policy => policy.RequireRole("Lecturer"));
        api.MapGet("/projects/{id:guid}/milestones", () => Pending("milestones", "UC-21")).RequireAuthorization(policy => policy.RequireRole("Student","Lecturer","Admin"));
        api.MapGet("/repositories/{id:guid}/commits", () => Pending("github", "UC-28")).RequireAuthorization(policy => policy.RequireRole("Student","Lecturer","Admin"));
        api.MapGet("/notifications/{id:guid}", () => Pending("notifications", "UC-31")).RequireAuthorization(policy => policy.RequireRole("Student","Lecturer","Admin"));
        api.MapGet("/audit-log/{id:guid}", () => Pending("audit", "UC-43")).RequireAuthorization(policy => policy.RequireRole("Admin"));
        api.MapGet("/system-errors/{id:guid}", () => Pending("errors", "UC-39")).RequireAuthorization(policy => policy.RequireRole("Admin"));
    }
}
