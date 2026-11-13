using System.Text.Json;

namespace StudentProjects.Api.Services;

public sealed record ProgressAiAttempt(ProgressAiResult Result, string Status);

/// <summary>AI transport/format failures must not discard independently retrieved Git facts.</summary>
public static class GitProgressAiAvailability
{
    public static async Task<ProgressAiAttempt> RunAsync(
        Func<CancellationToken, Task<ProgressAiResult>> analyze, CancellationToken ct)
    {
        try { return new(await analyze(ct), "available"); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { return Unavailable("timeout"); }
        catch (HttpRequestException) { return Unavailable("connection-error"); }
        catch (Exception ex) when (ex is JsonException or AnalysisInputException or InvalidOperationException)
        { return Unavailable("invalid-response"); }
    }

    private static ProgressAiAttempt Unavailable(string status) => new(new([], 0), status);
}
