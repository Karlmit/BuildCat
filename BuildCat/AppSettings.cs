namespace BuildCat;

internal sealed class AppSettings
{
    public string Owner { get; set; } = string.Empty;
    public string Repo { get; set; } = string.Empty;
    public string? GitHubToken { get; set; }
    public int PollIntervalSeconds { get; set; } = 30;
    public int RunningPollIntervalSeconds { get; set; } = 10;
    public bool NotifyBuildStarted { get; set; } = true;
    public bool NotifyBuildCompleted { get; set; } = true;
    public bool StartWithWindows { get; set; }

    public string RepositorySlug
    {
        get
        {
            var owner = Owner.Trim();
            var repo = Repo.Trim();
            return string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo)
                ? "Not configured"
                : $"{owner}/{repo}";
        }
    }

    public AppSettings Clone()
    {
        return new AppSettings
        {
            Owner = Owner,
            Repo = Repo,
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
        Owner = string.IsNullOrWhiteSpace(Owner) ? string.Empty : Owner.Trim();
        Repo = string.IsNullOrWhiteSpace(Repo) ? string.Empty : Repo.Trim();
        GitHubToken = string.IsNullOrWhiteSpace(GitHubToken) ? null : GitHubToken.Trim();
        PollIntervalSeconds = Math.Clamp(PollIntervalSeconds, 10, 3600);
        RunningPollIntervalSeconds = Math.Clamp(RunningPollIntervalSeconds, 5, 3600);
    }
}
