namespace BuildCat;

internal sealed class NotificationService
{
    private readonly NotifyIcon _notifyIcon;

    public NotificationService(NotifyIcon notifyIcon)
    {
        _notifyIcon = notifyIcon;
    }

    public void BuildStarted(string repository)
    {
        Show("Build started", $"GitHub is building {repository}.", ToolTipIcon.Info);
    }

    public void BuildSucceeded(string repository)
    {
        Show("Build completed successfully", $"{repository} is ready to update and test.", ToolTipIcon.Info);
    }

    public void BuildFailed(string repository)
    {
        Show("Build failed", $"{repository} failed. Open GitHub Actions to inspect the run.", ToolTipIcon.Error);
    }

    private void Show(string title, string body, ToolTipIcon icon)
    {
        if (!_notifyIcon.Visible)
        {
            return;
        }

        _notifyIcon.BalloonTipTitle = "BuildCat";
        _notifyIcon.BalloonTipText = $"{title}{Environment.NewLine}{body}";
        _notifyIcon.BalloonTipIcon = icon;
        _notifyIcon.ShowBalloonTip(8000);
    }
}
