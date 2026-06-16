namespace BuildCat;

internal sealed class AppSettings
{
    // Legacy fields kept only for migrating old settings.json files.
    public string Owner { get; set; } = string.Empty;
    public string Repo { get; set; } = string.Empty;

    public List<string> Repos { get; set; } = [];
    public string? GitHubToken { get; set; }
    public int PollIntervalSeconds { get; set; } = 30;
    public int RunningPollIntervalSeconds { get; set; } = 10;
    public bool NotifyBuildStarted { get; set; } = true;
    public bool NotifyBuildCompleted { get; set; } = true;
    public bool StartWithWindows { get; set; }

    public string RepositoriesDisplay => Repos.Count > 0
        ? string.Join(", ", Repos)
        : string.Empty;

    public string RepositorySlug => Repos.Count switch
    {
        0 => "Not configured",
        1 => Repos[0],
        _ => $"{Repos.Count} repositories"
    };

    public AppSettings Clone()
    {
        return new AppSettings
        {
            Repos = [.. Repos],
            GitHubToken = GitHubToken,
            PollIntervalSeconds = PollIntervalSeconds,
            RunningPollIntervalSeconds = RunningPollIntervalSeconds,
            NotifyBuildStarted = NotifyBuildStarted,
            NotifyBuildCompleted = NotifyBuildCompleted,
            StartWithWindows = StartWithWindows
        };
    }

    public void Normalize()
    {
        // Migrate legacy Owner/Repo into the Repos list.
        if (Repos.Count == 0
            && !string.IsNullOrWhiteSpace(Owner)
            && !string.IsNullOrWhiteSpace(Repo))
        {
            Repos = [$"{Owner.Trim()}/{Repo.Trim()}"];
        }
        Owner = string.Empty;
        Repo = string.Empty;

        // Parse, validate, and deduplicate each "owner/repo" entry.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalized = new List<string>();
        foreach (var entry in Repos)
        {
            var parts = entry.Trim().Split('/', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2
                && !string.IsNullOrWhiteSpace(parts[0])
                && !string.IsNullOrWhiteSpace(parts[1]))
            {
                var slug = $"{parts[0]}/{parts[1]}";
                if (seen.Add(slug))
                {
                    normalized.Add(slug);
                }
            }
        }
        Repos = normalized;

        GitHubToken = string.IsNullOrWhiteSpace(GitHubToken) ? null : GitHubToken.Trim();
        PollIntervalSeconds = Math.Clamp(PollIntervalSeconds, 10, 3600);
        RunningPollIntervalSeconds = Math.Clamp(RunningPollIntervalSeconds, 5, 3600);
    }
}
