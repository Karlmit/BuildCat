namespace BuildCat;

internal sealed class SettingsForm : Form
{
    private readonly TextBox _ownerTextBox = new();
    private readonly TextBox _repoTextBox = new();
    private readonly TextBox _tokenTextBox = new();
    private readonly NumericUpDown _pollIntervalInput = new();
    private readonly NumericUpDown _runningPollIntervalInput = new();
    private readonly CheckBox _notifyStartedCheckBox = new();
    private readonly CheckBox _notifyCompletedCheckBox = new();
    private readonly CheckBox _startWithWindowsCheckBox = new();

    public AppSettings Settings { get; private set; }

    public SettingsForm(AppSettings settings)
    {
        Settings = settings.Clone();
        Text = "BuildCat Settings";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        MinimizeBox = false;
        MinimumSize = new Size(640, 500);
        ShowIcon = false;
        ShowInTaskbar = false;
        ClientSize = new Size(640, 500);
        Font = new Font("Segoe UI", 9F);
        AutoScaleMode = AutoScaleMode.Dpi;

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            ColumnCount = 2,
            RowCount = 9
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        AddLabel(table, "GitHub owner", 0);
        ConfigureTextBox(_ownerTextBox, Settings.Owner);
        table.Controls.Add(_ownerTextBox, 1, 0);

        AddLabel(table, "GitHub repo", 1);
        ConfigureTextBox(_repoTextBox, Settings.Repo);
        table.Controls.Add(_repoTextBox, 1, 1);

        AddLabel(table, "GitHub token", 2);
        ConfigureTextBox(_tokenTextBox, Settings.GitHubToken ?? string.Empty);
        _tokenTextBox.UseSystemPasswordChar = true;
        table.Controls.Add(_tokenTextBox, 1, 2);

        AddLabel(table, "Poll interval when not building", 3);
        _pollIntervalInput.Minimum = 10;
        _pollIntervalInput.Maximum = 3600;
        _pollIntervalInput.Value = Math.Clamp(Settings.PollIntervalSeconds, 10, 3600);
        ConfigureNumericInput(_pollIntervalInput);
        table.Controls.Add(_pollIntervalInput, 1, 3);

        AddLabel(table, "Poll interval while building", 4);
        _runningPollIntervalInput.Minimum = 5;
        _runningPollIntervalInput.Maximum = 3600;
        _runningPollIntervalInput.Value = Math.Clamp(Settings.RunningPollIntervalSeconds, 5, 3600);
        ConfigureNumericInput(_runningPollIntervalInput);
        table.Controls.Add(_runningPollIntervalInput, 1, 4);

        ConfigureCheckBox(_notifyStartedCheckBox, "Notify when build starts", Settings.NotifyBuildStarted);
        table.Controls.Add(_notifyStartedCheckBox, 1, 5);

        ConfigureCheckBox(_notifyCompletedCheckBox, "Notify when build completes", Settings.NotifyBuildCompleted);
        table.Controls.Add(_notifyCompletedCheckBox, 1, 6);

        ConfigureCheckBox(_startWithWindowsCheckBox, "Start with Windows", Settings.StartWithWindows);
        table.Controls.Add(_startWithWindowsCheckBox, 1, 7);

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Bottom,
            Height = 44,
            Padding = new Padding(0, 8, 0, 0)
        };
        var saveButton = new Button { Text = "Save", DialogResult = DialogResult.OK, Width = 96, Height = 32 };
        var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 96, Height = 32 };
        saveButton.Click += SaveButtonOnClick;
        buttons.Controls.Add(saveButton);
        buttons.Controls.Add(cancelButton);
        table.Controls.Add(buttons, 0, 8);
        table.SetColumnSpan(buttons, 2);

        Controls.Add(table);
        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    private static void AddLabel(TableLayoutPanel table, string text, int row)
    {
        table.Controls.Add(new Label
        {
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft,
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            Margin = new Padding(0, 4, 8, 4)
        }, 0, row);
    }

    private static void ConfigureTextBox(TextBox textBox, string text)
    {
        textBox.Text = text;
        textBox.Dock = DockStyle.Fill;
        textBox.Margin = new Padding(3, 6, 3, 6);
    }

    private static void ConfigureNumericInput(NumericUpDown input)
    {
        input.Dock = DockStyle.Left;
        input.Width = 110;
        input.Margin = new Padding(3, 6, 3, 6);
    }

    private static void ConfigureCheckBox(CheckBox checkBox, string text, bool isChecked)
    {
        checkBox.Text = text;
        checkBox.Checked = isChecked;
        checkBox.Dock = DockStyle.Fill;
        checkBox.AutoSize = false;
        checkBox.Margin = new Padding(3, 4, 3, 4);
    }

    private void SaveButtonOnClick(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_ownerTextBox.Text) || string.IsNullOrWhiteSpace(_repoTextBox.Text))
        {
            MessageBox.Show(this, "GitHub owner and repo are required.", "BuildCat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        Settings = new AppSettings
        {
            Owner = _ownerTextBox.Text,
            Repo = _repoTextBox.Text,
            GitHubToken = _tokenTextBox.Text,
            PollIntervalSeconds = (int)_pollIntervalInput.Value,
            RunningPollIntervalSeconds = (int)_runningPollIntervalInput.Value,
            NotifyBuildStarted = _notifyStartedCheckBox.Checked,
            NotifyBuildCompleted = _notifyCompletedCheckBox.Checked,
            StartWithWindows = _startWithWindowsCheckBox.Checked
        };
        Settings.Normalize();
    }
}
