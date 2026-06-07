namespace BuildCat;

internal sealed class BuildStatusService
{
    private readonly GitHubActionsClient _client;

    public BuildStatusService(GitHubActionsClient client)
    {
        _client = client;
    }

    public async Task<BuildSnapshot> GetSnapshotAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        var checkedAt = DateTimeOffset.Now;
        var repository = settings.RepositorySlug;

        if (string.IsNullOrWhiteSpace(settings.Owner) || string.IsNullOrWhiteSpace(settings.Repo))
        {
            return Unknown(repository, checkedAt, "Missing GitHub owner or repo.");
        }

        var result = await _client.GetLatestRunAsync(settings, cancellationToken);
        if (result.Run is null)
        {
            return Unknown(
                repository,
                checkedAt,
                result.ErrorMessage,
                !string.IsNullOrWhiteSpace(settings.GitHubToken),
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
            repository,
            run.Name,
            run.DisplayTitle,
            run.HtmlUrl,
            run.CreatedAt,
            run.UpdatedAt,
            checkedAt,
            !string.IsNullOrWhiteSpace(settings.GitHubToken),
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
