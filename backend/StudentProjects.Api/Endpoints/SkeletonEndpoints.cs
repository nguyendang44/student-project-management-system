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
        api.MapGet("/automation/runs", () => Pending("automation", "UC-33,UC-34,UC-38")).RequireAuthorization(policy => policy.RequireRole("Admin"));
        api.MapGet("/audit-log", () => Pending("audit", "UC-43")).RequireAuthorization(policy => policy.RequireRole("Admin"));
        api.MapGet("/system-errors", () => Pending("errors", "UC-39")).RequireAuthorization(policy => policy.RequireRole("Admin"));
        api.MapGet("/settings", () => Pending("settings", "UC-40")).RequireAuthorization(policy => policy.RequireRole("Admin"));
        api.MapPut("/settings/{key}", () => Pending("settings", "UC-40")).RequireAuthorization(policy => policy.RequireRole("Admin"));
        api.MapPost("/milestones/{id:guid}/approve", () => Pending("milestones", "UC-25")).RequireAuthorization(policy => policy.RequireRole("Lecturer"));
        api.MapPost("/milestones/{id:guid}/request-revision", () => Pending("milestones", "UC-24")).RequireAuthorization(policy => policy.RequireRole("Lecturer"));
        api.MapGet("/projects/{id:guid}/milestones", () => Pending("milestones", "UC-21")).RequireAuthorization(policy => policy.RequireRole("Student","Lecturer","Admin"));
        api.MapGet("/repositories/{id:guid}/commits", () => Pending("github", "UC-28")).RequireAuthorization(policy => policy.RequireRole("Student","Lecturer","Admin"));
        api.MapGet("/notifications/{id:guid}", () => Pending("notifications", "UC-31")).RequireAuthorization(policy => policy.RequireRole("Student","Lecturer","Admin"));
        api.MapGet("/audit-log/{id:guid}", () => Pending("audit", "UC-43")).RequireAuthorization(policy => policy.RequireRole("Admin"));
        api.MapGet("/system-errors/{id:guid}", () => Pending("errors", "UC-39")).RequireAuthorization(policy => policy.RequireRole("Admin"));
    }
}
