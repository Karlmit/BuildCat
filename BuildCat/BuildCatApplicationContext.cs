namespace BuildCat;

internal sealed class BuildCatApplicationContext : ApplicationContext
{
    private readonly SettingsService _settingsService = new();
    private readonly StartupService _startupService = new();
    private readonly GitHubActionsClient _gitHubClient = new();
    private readonly BuildStatusService _statusService;
    private readonly TrayIconManager _tray;
    private readonly NotificationService _notifications;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly SemaphoreSlim _checkLock = new(1, 1);
    private readonly SemaphoreSlim _pollWake = new(0, 1);
    private readonly Dictionary<string, RepoNotificationTracker> _notificationTrackers = [];
    private AppSettings _settings;
    private IReadOnlyList<BuildSnapshot> _lastSnapshots = [];
    private bool _isExiting;

    public BuildCatApplicationContext()
    {
        _settings = _settingsService.Load();
        _settings.StartWithWindows = _startupService.IsEnabled();
        _statusService = new BuildStatusService(_gitHubClient);
        _tray = new TrayIconManager();
        _notifications = new NotificationService(_tray.NotifyIcon);

        _tray.CheckNowRequested += async (_, _) =>
        {
            await CheckNowAsync();
            WakePollLoop();
        };
        _tray.SettingsRequested += (_, _) => ShowSettings();
        _tray.StartWithWindowsChanged += (_, enabled) => SetStartWithWindows(enabled);
        _tray.ExitRequested += (_, _) => ExitThread();
        _tray.UpdateSettings(_settings, _settings.StartWithWindows);
        _tray.UpdateNextPoll(TimeSpan.FromSeconds(_settings.PollIntervalSeconds), null);

        _ = PollLoopAsync(_shutdown.Token);
    }

    private async Task PollLoopAsync(CancellationToken cancellationToken)
    {
        var snapshots = await CheckNowAsync();

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var nextDelay = GetNextPollDelay(_lastSnapshots);
                _tray.UpdateNextPoll(nextDelay, _lastSnapshots);
                using var waitCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var delayTask = Task.Delay(nextDelay, waitCancellation.Token);
                var wakeTask = _pollWake.WaitAsync(waitCancellation.Token);
                var completedTask = await Task.WhenAny(delayTask, wakeTask);
                waitCancellation.Cancel();

                if (completedTask == wakeTask)
                {
                    continue;
                }

                snapshots = await CheckNowAsync();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task<IReadOnlyList<BuildSnapshot>> CheckNowAsync()
    {
        if (_isExiting || !await _checkLock.WaitAsync(0))
        {
            return _lastSnapshots;
        }

        try
        {
            var snapshots = await _statusService.GetSnapshotsAsync(_settings.Clone(), _shutdown.Token);
            _tray.UpdateSnapshots(snapshots);
            foreach (var snapshot in snapshots)
            {
                MaybeNotify(snapshot);
            }
            _lastSnapshots = snapshots;
            return snapshots;
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
            return _lastSnapshots;
        }
        catch (Exception ex) when (!_shutdown.IsCancellationRequested)
        {
            var fallback = _settings.Repos.Count > 0
                ? _settings.Repos.Select(slug => new BuildSnapshot(
                    BuildState.Unknown, null, "Unknown", slug,
                    null, null, null, null, null,
                    DateTimeOffset.Now,
                    !string.IsNullOrWhiteSpace(_settings.GitHubToken),
                    null, null, null, ex.Message)).ToList()
                : [new BuildSnapshot(
                    BuildState.Unknown, null, "Unknown", "Not configured",
                    null, null, null, null, null,
                    DateTimeOffset.Now,
                    !string.IsNullOrWhiteSpace(_settings.GitHubToken),
                    null, null, null, ex.Message)];

            _tray.UpdateSnapshots(fallback);
            _lastSnapshots = fallback;
            return fallback;
        }
        finally
        {
            _checkLock.Release();
        }
    }

    private TimeSpan GetNextPollDelay(IReadOnlyList<BuildSnapshot> snapshots)
    {
        var configuredIdleDelay = TimeSpan.FromSeconds(_settings.PollIntervalSeconds);
        var runningSnapshot = snapshots.FirstOrDefault(s => s.IsRunning);
        return runningSnapshot is null ? configuredIdleDelay : GetRateSafeRunningDelay(runningSnapshot);
    }

    private TimeSpan GetRateSafeRunningDelay(BuildSnapshot snapshot)
    {
        var desiredDelay = TimeSpan.FromSeconds(_settings.RunningPollIntervalSeconds);
        var absoluteMinimumDelay = TimeSpan.FromSeconds(5);

        if (snapshot.RateLimitRemaining is not { } remaining || snapshot.RateLimitReset is not { } reset)
        {
            return _settings.GitHubToken is null
                ? TimeSpan.FromSeconds(Math.Max(60, desiredDelay.TotalSeconds))
                : desiredDelay;
        }

        var reserve = _settings.GitHubToken is null ? 5 : 100;
        var usableRequests = remaining - reserve;
        var secondsUntilReset = Math.Max(1, (reset - DateTimeOffset.Now).TotalSeconds);

        if (usableRequests <= 0)
        {
            return TimeSpan.FromSeconds(Math.Min(secondsUntilReset + 5, 3600));
        }

        var safeSeconds = Math.Ceiling(secondsUntilReset / usableRequests) + 1;
        var boundedSeconds = Math.Clamp(safeSeconds, absoluteMinimumDelay.TotalSeconds, 300);
        boundedSeconds = Math.Max(boundedSeconds, desiredDelay.TotalSeconds);
        return TimeSpan.FromSeconds(boundedSeconds);
    }

    private void WakePollLoop()
    {
        if (_pollWake.CurrentCount == 0)
        {
            _pollWake.Release();
        }
    }

    private void MaybeNotify(BuildSnapshot snapshot)
    {
        if (snapshot.RunId is null) return;

        var repo = snapshot.Repository;
        if (!_notificationTrackers.TryGetValue(repo, out var tracker))
        {
            tracker = new RepoNotificationTracker();
            _notificationTrackers[repo] = tracker;
        }

        var runId = snapshot.RunId.Value;

        if (!tracker.HasObservedRun)
        {
            tracker.HasObservedRun = true;
            tracker.LastKnownRunId = runId;
            if (snapshot.IsRunning)
            {
                tracker.RunningRunsObserved.Add(runId);
            }
            return;
        }

        if (_settings.NotifyBuildStarted
            && snapshot.IsRunning
            && tracker.LastKnownRunId != runId
            && tracker.StartedRunsNotified.Add(runId))
        {
            _notifications.BuildStarted(snapshot.Repository);
        }

        if (snapshot.IsRunning)
        {
            tracker.RunningRunsObserved.Add(runId);
        }

        if (_settings.NotifyBuildCompleted
            && tracker.RunningRunsObserved.Contains(runId)
            && snapshot.IsCompleted
            && tracker.CompletedRunsNotified.Add(runId))
        {
            if (snapshot.State == BuildState.Success)
            {
                _notifications.BuildSucceeded(snapshot.Repository);
            }
            else
            {
                _notifications.BuildFailed(snapshot.Repository);
            }
        }

        tracker.LastKnownRunId = runId;
    }

    private void ShowSettings()
    {
        using var form = new SettingsForm(_settings);
        if (form.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        _settings = form.Settings;
        SetStartWithWindows(_settings.StartWithWindows);
        _settings.StartWithWindows = _startupService.IsEnabled();
        _settingsService.Save(_settings);
        _tray.UpdateSettings(_settings, _settings.StartWithWindows);
        _tray.UpdateNextPoll(GetNextPollDelay(_lastSnapshots), _lastSnapshots);
        _ = CheckNowAsync();
        WakePollLoop();
    }

    private void SetStartWithWindows(bool enabled)
    {
        try
        {
            _startupService.SetEnabled(enabled);
            _settings.StartWithWindows = enabled;
            _settingsService.Save(_settings);
            _tray.UpdateSettings(_settings, enabled);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            MessageBox.Show("BuildCat could not update Windows startup settings.", "BuildCat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _settings.StartWithWindows = _startupService.IsEnabled();
            _tray.UpdateSettings(_settings, _settings.StartWithWindows);
        }
    }

    protected override void ExitThreadCore()
    {
        _isExiting = true;
        _shutdown.Cancel();
        _tray.Dispose();
        _gitHubClient.Dispose();
        _pollWake.Dispose();
        _shutdown.Dispose();
        base.ExitThreadCore();
    }

    private sealed class RepoNotificationTracker
    {
        public long? LastKnownRunId;
        public bool HasObservedRun;
        public readonly HashSet<long> RunningRunsObserved = [];
        public readonly HashSet<long> StartedRunsNotified = [];
        public readonly HashSet<long> CompletedRunsNotified = [];
    }
}
