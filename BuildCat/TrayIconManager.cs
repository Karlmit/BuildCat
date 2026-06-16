using System.Diagnostics;

namespace BuildCat;

internal sealed class TrayIconManager : IDisposable
{
    private readonly ToolStripMenuItem _checkedItem = new("Last checked: Never") { Enabled = false };
    private readonly ToolStripMenuItem _authItem = new("GitHub auth: Unknown") { Enabled = false };
    private readonly ToolStripMenuItem _pollingItem = new("Polling: Green 30s, Yellow 10s") { Enabled = false };
    private readonly ToolStripMenuItem _nextPollItem = new("Next auto-check: Unknown") { Enabled = false };
    private readonly ToolStripSeparator _repoSeparator = new();
    private readonly ToolStripSeparator _actionSeparator = new();
    private readonly ToolStripMenuItem _checkNowItem = new("Check now");
    private readonly ToolStripMenuItem _settingsItem = new("Settings");
    private readonly ToolStripMenuItem _startWithWindowsItem = new("Start with Windows") { CheckOnClick = true };
    private readonly ToolStripMenuItem _exitItem = new("Exit");
    private readonly ContextMenuStrip _menu = new();
    private readonly List<ToolStripMenuItem> _repoItems = [];
    private readonly Dictionary<BuildState, Icon> _icons = new();

    public NotifyIcon NotifyIcon { get; }

    public event EventHandler? CheckNowRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler<bool>? StartWithWindowsChanged;
    public event EventHandler? ExitRequested;

    public TrayIconManager()
    {
        _icons[BuildState.Unknown] = CatIconFactory.Create(BuildState.Unknown);
        _icons[BuildState.Running] = CatIconFactory.Create(BuildState.Running);
        _icons[BuildState.Success] = CatIconFactory.Create(BuildState.Success);
        _icons[BuildState.Failed] = CatIconFactory.Create(BuildState.Failed);

        _menu.Items.AddRange([
            _checkedItem,
            _authItem,
            _pollingItem,
            _nextPollItem,
            _repoSeparator,
            // per-repo items inserted dynamically here
            _actionSeparator,
            _checkNowItem,
            _settingsItem,
            _startWithWindowsItem,
            new ToolStripSeparator(),
            _exitItem
        ]);

        _checkNowItem.Click += (_, _) => CheckNowRequested?.Invoke(this, EventArgs.Empty);
        _settingsItem.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        _startWithWindowsItem.CheckedChanged += StartWithWindowsItemOnCheckedChanged;
        _exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        NotifyIcon = new NotifyIcon
        {
            Icon = _icons[BuildState.Unknown],
            Text = "BuildCat: Not configured",
            ContextMenuStrip = _menu,
            Visible = true
        };
        NotifyIcon.MouseUp += NotifyIconOnMouseUp;
    }

    public void UpdateSettings(AppSettings settings, bool startWithWindows)
    {
        _pollingItem.Text = $"Polling: Green {FormatDuration(TimeSpan.FromSeconds(settings.PollIntervalSeconds))}, Yellow {FormatDuration(TimeSpan.FromSeconds(settings.RunningPollIntervalSeconds))}";
        SetStartWithWindowsChecked(startWithWindows);
    }

    public void SetStartWithWindowsChecked(bool checkedValue)
    {
        if (_startWithWindowsItem.Checked == checkedValue) return;

        _startWithWindowsItem.CheckedChanged -= StartWithWindowsItemOnCheckedChanged;
        _startWithWindowsItem.Checked = checkedValue;
        _startWithWindowsItem.CheckedChanged += StartWithWindowsItemOnCheckedChanged;
    }

    public void UpdateSnapshots(IReadOnlyList<BuildSnapshot> snapshots)
    {
        var aggregateState = BuildSnapshot.AggregateState(snapshots);
        NotifyIcon.Icon = _icons[aggregateState];
        NotifyIcon.Text = TrimTooltip(BuildTooltip(snapshots));

        if (snapshots.Count > 0)
        {
            _checkedItem.Text = $"Last checked: {snapshots[0].CheckedAt.LocalDateTime:yyyy-MM-dd HH:mm:ss}";
            _authItem.Text = BuildAuthText(snapshots[0]);
        }

        RebuildRepoMenuItems(snapshots);
    }

    public void UpdateNextPoll(TimeSpan delay, IReadOnlyList<BuildSnapshot>? snapshots)
    {
        var state = BuildSnapshot.AggregateState(snapshots ?? []) switch
        {
            BuildState.Running => "yellow",
            BuildState.Success => "green",
            BuildState.Failed => "red",
            _ => "gray"
        };
        _nextPollItem.Text = $"Next auto-check: ~{FormatDuration(delay)} ({state})";
    }

    private void RebuildRepoMenuItems(IReadOnlyList<BuildSnapshot> snapshots)
    {
        foreach (var item in _repoItems)
        {
            _menu.Items.Remove(item);
            item.Dispose();
        }
        _repoItems.Clear();

        var insertIndex = _menu.Items.IndexOf(_repoSeparator) + 1;
        foreach (var snapshot in snapshots)
        {
            var label = FormatRepoMenuItem(snapshot);
            var item = new ToolStripMenuItem(label);
            var url = snapshot.HtmlUrl;
            item.Enabled = !string.IsNullOrWhiteSpace(url);
            if (!string.IsNullOrWhiteSpace(url))
            {
                item.Click += (_, _) => OpenUrl(url);
            }

            _menu.Items.Insert(insertIndex, item);
            _repoItems.Add(item);
            insertIndex++;
        }
    }

    private static string FormatRepoMenuItem(BuildSnapshot snapshot)
    {
        var status = snapshot.State switch
        {
            BuildState.Success => "Success",
            BuildState.Running => "Building...",
            BuildState.Failed  => snapshot.ErrorMessage is null
                ? snapshot.StatusText
                : $"{snapshot.StatusText}",
            _ => snapshot.ErrorMessage is null ? "Unknown" : "Error"
        };
        return TrimMenuText($"{snapshot.Repository}  ·  {status}");
    }

    private static string BuildTooltip(IReadOnlyList<BuildSnapshot> snapshots)
    {
        if (snapshots.Count == 0)
            return "BuildCat: Not configured";
        if (snapshots.Count == 1)
            return snapshots[0].Tooltip;

        var groups = snapshots.GroupBy(s => s.State).ToDictionary(g => g.Key, g => g.Count());
        var parts = new List<string>();
        if (groups.TryGetValue(BuildState.Failed, out var f)) parts.Add($"{f} failed");
        if (groups.TryGetValue(BuildState.Running, out var r)) parts.Add($"{r} building");
        if (groups.TryGetValue(BuildState.Success, out var s)) parts.Add($"{s} green");
        if (groups.TryGetValue(BuildState.Unknown, out var u)) parts.Add($"{u} unknown");
        return TrimTooltip($"BuildCat: {string.Join(", ", parts)}");
    }

    private void StartWithWindowsItemOnCheckedChanged(object? sender, EventArgs e)
    {
        StartWithWindowsChanged?.Invoke(this, _startWithWindowsItem.Checked);
    }

    private void NotifyIconOnMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            CheckNowRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private static void OpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new ProcessStartInfo(url)
            {
                UseShellExecute = true
            });
        }
        catch
        {
            // Browser launch failures should not take down the tray app.
        }
    }

    private static string BuildAuthText(BuildSnapshot snapshot)
    {
        var auth = snapshot.TokenConfigured
            ? snapshot.LooksAuthenticated ? "Token active" : "Token configured"
            : "No token";

        if (snapshot.RateLimitLimit is null || snapshot.RateLimitRemaining is null)
        {
            return $"GitHub auth: {auth}";
        }

        var resetText = snapshot.RateLimitReset is null
            ? string.Empty
            : $", resets {snapshot.RateLimitReset.Value.LocalDateTime:HH:mm}";
        return $"GitHub auth: {auth} ({snapshot.RateLimitRemaining}/{snapshot.RateLimitLimit} left{resetText})";
    }

    private static string TrimTooltip(string text) =>
        text.Length <= 63 ? text : text[..60] + "...";

    private static string TrimMenuText(string text) =>
        text.Length <= 180 ? text : text[..177] + "...";

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalSeconds < 60)
            return $"{Math.Ceiling(duration.TotalSeconds):0}s";

        if (duration.TotalMinutes < 60)
            return duration.TotalSeconds % 60 == 0
                ? $"{duration.TotalMinutes:0}m"
                : $"{duration.TotalMinutes:0.#}m";

        return $"{duration.TotalHours:0.#}h";
    }

    public void Dispose()
    {
        NotifyIcon.Visible = false;
        NotifyIcon.Dispose();
        _menu.Dispose();
        foreach (var icon in _icons.Values)
        {
            icon.Dispose();
        }
    }
}
