using System.Diagnostics;

namespace BuildCat;

internal sealed class TrayIconManager : IDisposable
{
    private readonly ToolStripMenuItem _statusItem = new("Current status: Unknown") { Enabled = false };
    private readonly ToolStripMenuItem _checkedItem = new("Last checked: Never") { Enabled = false };
    private readonly ToolStripMenuItem _repositoryItem = new("Repository: Not configured") { Enabled = false };
    private readonly ToolStripMenuItem _authItem = new("GitHub auth: Unknown") { Enabled = false };
    private readonly ToolStripMenuItem _pollingItem = new("Polling: Green 30s, Yellow 10s") { Enabled = false };
    private readonly ToolStripMenuItem _nextPollItem = new("Next auto-check: Unknown") { Enabled = false };
    private readonly ToolStripMenuItem _workflowItem = new("Latest workflow: Unknown") { Enabled = false };
    private readonly ToolStripMenuItem _openRunItem = new("Open latest run in browser");
    private readonly ToolStripMenuItem _checkNowItem = new("Check now");
    private readonly ToolStripMenuItem _settingsItem = new("Settings");
    private readonly ToolStripMenuItem _startWithWindowsItem = new("Start with Windows") { CheckOnClick = true };
    private readonly ToolStripMenuItem _exitItem = new("Exit");
    private readonly ContextMenuStrip _menu = new();
    private readonly Dictionary<BuildState, Icon> _icons = new();
    private BuildSnapshot? _snapshot;

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
            _statusItem,
            _checkedItem,
            _repositoryItem,
            _authItem,
            _pollingItem,
            _nextPollItem,
            _workflowItem,
            new ToolStripSeparator(),
            _openRunItem,
            _checkNowItem,
            _settingsItem,
            _startWithWindowsItem,
            new ToolStripSeparator(),
            _exitItem
        ]);

        _openRunItem.Enabled = false;
        _openRunItem.Click += (_, _) => OpenLatestRun();
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
        _repositoryItem.Text = $"Repository: {settings.RepositorySlug}";
        _pollingItem.Text = $"Polling: Green {FormatDuration(TimeSpan.FromSeconds(settings.PollIntervalSeconds))}, Yellow {FormatDuration(TimeSpan.FromSeconds(settings.RunningPollIntervalSeconds))}";
        SetStartWithWindowsChecked(startWithWindows);
        if (_snapshot is null)
        {
            var tooltip = string.IsNullOrWhiteSpace(settings.Repo)
                ? "BuildCat: Not configured"
                : $"BuildCat - {settings.Repo}: Unknown";
            NotifyIcon.Text = TrimTooltip(tooltip);
        }
    }

    public void SetStartWithWindowsChecked(bool checkedValue)
    {
        if (_startWithWindowsItem.Checked == checkedValue)
        {
            return;
        }

        _startWithWindowsItem.CheckedChanged -= StartWithWindowsItemOnCheckedChanged;
        _startWithWindowsItem.Checked = checkedValue;
        _startWithWindowsItem.CheckedChanged += StartWithWindowsItemOnCheckedChanged;
    }

    public void UpdateSnapshot(BuildSnapshot snapshot)
    {
        _snapshot = snapshot;
        NotifyIcon.Icon = _icons[snapshot.State];
        NotifyIcon.Text = TrimTooltip(snapshot.Tooltip);

        _statusItem.Text = TrimMenuText(snapshot.ErrorMessage is null
            ? $"Current status: {snapshot.StatusText}"
            : $"Current status: {snapshot.StatusText} - {snapshot.ErrorMessage}");
        _checkedItem.Text = $"Last checked: {snapshot.CheckedAt.LocalDateTime:yyyy-MM-dd HH:mm:ss}";
        _repositoryItem.Text = $"Repository: {snapshot.Repository}";
        _authItem.Text = BuildAuthText(snapshot);
        _workflowItem.Text = snapshot.MenuWorkflowText;
        _openRunItem.Enabled = !string.IsNullOrWhiteSpace(snapshot.HtmlUrl);
    }

    public void UpdateNextPoll(TimeSpan delay, BuildSnapshot? snapshot)
    {
        var state = snapshot?.State switch
        {
            BuildState.Running => "yellow",
            BuildState.Success => "green",
            BuildState.Failed => "red",
            _ => "gray"
        };

        _nextPollItem.Text = $"Next auto-check: ~{FormatDuration(delay)} ({state})";
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

    private void OpenLatestRun()
    {
        if (string.IsNullOrWhiteSpace(_snapshot?.HtmlUrl))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(_snapshot.HtmlUrl)
            {
                UseShellExecute = true
            });
        }
        catch
        {
            // Browser launch failures should not take down the tray app.
        }
    }

    private static string TrimTooltip(string text)
    {
        return text.Length <= 63 ? text : text[..60] + "...";
    }

    private static string TrimMenuText(string text)
    {
        return text.Length <= 180 ? text : text[..177] + "...";
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

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalSeconds < 60)
        {
            return $"{Math.Ceiling(duration.TotalSeconds):0}s";
        }

        if (duration.TotalMinutes < 60)
        {
            return duration.TotalSeconds % 60 == 0
                ? $"{duration.TotalMinutes:0}m"
                : $"{duration.TotalMinutes:0.#}m";
        }

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
