namespace BuildCat;

internal sealed class BuildStatusService
{
    private readonly GitHubActionsClient _client;

    public BuildStatusService(GitHubActionsClient client)
    {
        _client = client;
    }

    public async Task<IReadOnlyList<BuildSnapshot>> GetSnapshotsAsync(
        AppSettings settings, CancellationToken cancellationToken)
    {
        if (settings.Repos.Count == 0)
        {
            return [Unknown("Not configured", DateTimeOffset.Now, "No repositories configured.",
                !string.IsNullOrWhiteSpace(settings.GitHubToken))];
        }

        var tasks = settings.Repos
            .Select(slug => FetchSnapshotAsync(slug, settings.GitHubToken, cancellationToken));
        return await Task.WhenAll(tasks);
    }

    private async Task<BuildSnapshot> FetchSnapshotAsync(
        string slug, string? token, CancellationToken cancellationToken)
    {
        var checkedAt = DateTimeOffset.Now;
        var parts = slug.Split('/', 2);
        var owner = parts[0];
        var repo = parts[1];

        var result = await _client.GetLatestRunAsync(owner, repo, token, cancellationToken);
        if (result.Run is null)
        {
            return Unknown(
                slug,
                checkedAt,
                result.ErrorMessage,
                !string.IsNullOrWhiteSpace(token),
                result.RateLimitLimit,
                result.RateLimitRemaining,
                result.RateLimitReset);
        }

        var run = result.Run;
        var state = EvaluateState(run.Status, run.Conclusion);
        return new BuildSnapshot(
            state,
            run.Id,
            ToStatusText(state, run.Status, run.Conclusion),
            slug,
            run.Name,
            run.DisplayTitle,
            run.HtmlUrl,
            run.CreatedAt,
            run.UpdatedAt,
            checkedAt,
            !string.IsNullOrWhiteSpace(token),
            result.RateLimitLimit,
            result.RateLimitRemaining,
            result.RateLimitReset,
            result.ErrorMessage);
    }

    private static BuildSnapshot Unknown(
        string repository,
        DateTimeOffset checkedAt,
        string? error,
        bool tokenConfigured = false,
        int? rateLimitLimit = null,
        int? rateLimitRemaining = null,
        DateTimeOffset? rateLimitReset = null)
    {
        return new BuildSnapshot(
            BuildState.Unknown,
            null,
            "Unknown",
            repository,
            null,
            null,
            null,
            null,
            null,
            checkedAt,
            tokenConfigured,
            rateLimitLimit,
            rateLimitRemaining,
            rateLimitReset,
            error);
    }

    private static BuildState EvaluateState(string? status, string? conclusion)
    {
        return status?.ToLowerInvariant() switch
        {
            "queued" or "in_progress" => BuildState.Running,
            "completed" when string.Equals(conclusion, "success", StringComparison.OrdinalIgnoreCase) => BuildState.Success,
            "completed" => BuildState.Failed,
            _ => BuildState.Unknown
        };
    }

    private static string ToStatusText(BuildState state, string? status, string? conclusion)
    {
        return state switch
        {
            BuildState.Running => "Building...",
            BuildState.Success => "Success",
            BuildState.Failed => string.IsNullOrWhiteSpace(conclusion) ? "Failed" : $"Failed ({conclusion})",
            _ => string.IsNullOrWhiteSpace(status) ? "Unknown" : $"Unknown ({status})"
        };
    }
}
