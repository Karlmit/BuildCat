namespace BuildCat;

internal sealed record BuildSnapshot(
    BuildState State,
    long? RunId,
    string StatusText,
    string Repository,
    string? WorkflowName,
    string? DisplayTitle,
    string? HtmlUrl,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset CheckedAt,
    bool TokenConfigured,
    int? RateLimitLimit,
    int? RateLimitRemaining,
    DateTimeOffset? RateLimitReset,
    string? ErrorMessage)
{
    public bool IsRunning => State == BuildState.Running;
    public bool IsCompleted => State is BuildState.Success or BuildState.Failed;
    public bool LooksAuthenticated => RateLimitLimit > 60;

    public string MenuWorkflowText
    {
        get
        {
            var text = !string.IsNullOrWhiteSpace(DisplayTitle) ? DisplayTitle : WorkflowName;
            return string.IsNullOrWhiteSpace(text) ? "Latest workflow: Unknown" : $"Latest workflow: {text}";
        }
    }

    public string Tooltip
    {
        get
        {
            if (Repository == "Not configured")
            {
                return "BuildCat: Not configured";
            }

            var shortRepository = Repository.Contains('/') ? Repository[(Repository.IndexOf('/') + 1)..] : Repository;
            return $"BuildCat - {shortRepository}: {StatusText}";
        }
    }

    // Red > Yellow > Gray > Green priority for the tray icon.
    public static BuildState AggregateState(IReadOnlyList<BuildSnapshot> snapshots)
    {
        if (snapshots.Count == 0) return BuildState.Unknown;
        if (snapshots.Any(s => s.State == BuildState.Failed)) return BuildState.Failed;
        if (snapshots.Any(s => s.State == BuildState.Running)) return BuildState.Running;
        if (snapshots.Any(s => s.State == BuildState.Success)) return BuildState.Success;
        return BuildState.Unknown;
    }
}
