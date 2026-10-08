using HyperXBatteryTray.Updates;

namespace HyperXBatteryTray.Settings;

public sealed partial class SettingsForm
{
    private const int AboutUpdatesCardLogicalHeight = 382;
    private Label? _updatesStatusLabel;
    private Button? _updatesCheckButton;
    private Button? _updatesDownloadButton;
    private Button? _updatesInstallButton;
    private Button? _updatesLaterButton;
    private ProgressBar? _updatesProgress;
    private bool _installLaunchInProgress;

    private void BuildAboutUpdatesCard()
    {
        bool dark = EffectiveTheme == AppTheme.Dark;
        Color foreground = dark ? Color.WhiteSmoke : LightText;
        Color secondary = dark ? DarkSecondary : LightSecondary;
        bool store = _updateCoordinator?.IsStoreManaged ?? WindowsPackageDetector.IsPackaged();

        int height = store ? 128 : AboutUpdatesCardLogicalHeight;
        RoundedPanel card = CreateResponsiveCard(height);
        AddResponsiveRow(card, height + AboutSectionBottomMarginLogicalHeight,
            AboutSectionBottomMarginLogicalHeight);
        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            Padding = ScaleUiPadding(14, 10, 14, 10),
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        card.Controls.Add(layout);

        TableLayoutPanel header = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ScaleUi(30)));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.Controls.Add(CreateTableIcon("notification", StandardUiIconLogicalSize), 0, 0);
        header.Controls.Add(CreateUpdateLabel("UpdatesTitle", 9.5f, foreground, true), 1, 0);
        AddUpdatesRow(layout, header, 30);
        AddUpdatesRow(layout, CreateUpdateLabel("UpdatesDescription", 8f, secondary), 31);

        if (store)
        {
            AddUpdatesRow(layout, CreateUpdateLabel("UpdatesStoreManaged", 8.8f, secondary), 38);
            return;
        }

        AddUpdatesRow(layout, CreateUpdateLabel("UpdatesChannel", 8.8f, foreground, true), 22);
        TableLayoutPanel channelRow = CreateUpdatesOptionsRow();
        RadioButton stable = CreateUpdateRadio("UpdatesStable", _pendingUpdateChannel == UpdateChannel.Stable, foreground);
        RadioButton beta = CreateUpdateRadio("UpdatesBeta", _pendingUpdateChannel == UpdateChannel.Beta, foreground);
        channelRow.Controls.Add(stable, 0, 0);
        channelRow.Controls.Add(beta, 1, 0);
        stable.CheckedChanged += (_, _) => { if (stable.Checked) { _pendingUpdateChannel = UpdateChannel.Stable; RefreshUpdatesUi(); } };
        beta.CheckedChanged += (_, _) => { if (beta.Checked) { _pendingUpdateChannel = UpdateChannel.Beta; RefreshUpdatesUi(); } };
        AddUpdatesRow(layout, channelRow, 32);

        AddUpdatesRow(layout, CreateUpdateLabel("UpdatesPreference", 8.8f, foreground, true), 22);
        RadioButton automatic = CreateUpdateRadio("UpdatesAutomatic", _pendingUpdateDownloadMode == UpdateDownloadMode.DownloadAutomatically, foreground);
        RadioButton notify = CreateUpdateRadio("UpdatesNotify", _pendingUpdateDownloadMode == UpdateDownloadMode.NotifyBeforeDownload, foreground);
        // Separate rows require a common radio container to preserve mutual exclusion.
        Panel modeGroup = new() { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        TableLayoutPanel modes = new() { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, BackColor = Color.Transparent };
        modes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        modes.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        modes.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        modes.Controls.Add(automatic, 0, 0);
        modes.Controls.Add(notify, 0, 1);
        modeGroup.Controls.Add(modes);
        automatic.CheckedChanged += (_, _) => { if (automatic.Checked) { _pendingUpdateDownloadMode = UpdateDownloadMode.DownloadAutomatically; RefreshUpdatesUi(); } };
        notify.CheckedChanged += (_, _) => { if (notify.Checked) { _pendingUpdateDownloadMode = UpdateDownloadMode.NotifyBeforeDownload; RefreshUpdatesUi(); } };
        AddUpdatesRow(layout, modeGroup, 77);

        _updatesStatusLabel = CreateUpdateLabel("UpdatesIdle", 8.5f, secondary);
        AddUpdatesRow(layout, _updatesStatusLabel, 48);
        _updatesProgress = new ProgressBar
        {
            Dock = DockStyle.Fill,
            Maximum = 100,
            Visible = false,
            Style = ProgressBarStyle.Continuous,
            Margin = ScaleUiPadding(0, 3, 0, 5)
        };
        AddUpdatesRow(layout, _updatesProgress, 16);

        TableLayoutPanel actions = CreateUpdatesOptionsRow();
        _updatesCheckButton = CreateUpdateButton("UpdatesCheck", dark);
        _updatesDownloadButton = CreateUpdateButton("UpdatesDownload", dark);
        actions.Controls.Add(_updatesCheckButton, 0, 0);
        actions.Controls.Add(_updatesDownloadButton, 1, 0);
        _updatesCheckButton.Click += async (_, _) =>
        {
            if (_updateCoordinator is not null)
                await _updateCoordinator.CheckAsync(true);
        };
        _updatesDownloadButton.Click += async (_, _) =>
        {
            if (_updateCoordinator is not null)
                await _updateCoordinator.DownloadAvailableAsync();
        };
        AddUpdatesRow(layout, actions, 42);

        TableLayoutPanel installActions = CreateUpdatesOptionsRow();
        _updatesInstallButton = CreateUpdateButton("UpdatesInstall", dark);
        _updatesLaterButton = CreateUpdateButton("UpdatesLater", dark);
        installActions.Controls.Add(_updatesInstallButton, 0, 0);
        installActions.Controls.Add(_updatesLaterButton, 1, 0);
        _updatesInstallButton.Click += async (_, _) => await InstallUpdateFromAboutAsync();
        _updatesLaterButton.Click += (_, _) =>
        {
            _updateCoordinator?.RemindLater();
            RefreshUpdatesUi();
        };
        AddUpdatesRow(layout, installActions, 42);

        RefreshUpdatesUi();
    }

    private async Task InstallUpdateFromAboutAsync()
    {
        if (ApplicationRuntime.IsTestMode || _installLaunchInProgress ||
            _installDownloadedUpdateAsync is null ||
            _updateCoordinator is null || _updateCoordinator.IsStoreManaged ||
            _pendingUpdateChannel != _settings.UpdateChannel ||
            _pendingUpdateDownloadMode != _settings.UpdateDownloadMode ||
            _updateCoordinator.Snapshot.Status is not (UpdateStatus.Downloaded or UpdateStatus.InstallationFailed))
            return;

        string version = _updateCoordinator.Snapshot.Version ?? string.Empty;
        if (MessageBox.Show(this, string.Format(L("UpdatesInstallConfirm"), version),
                Application.ProductName, MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
        {
            _updateCoordinator.RemindLater();
            RefreshUpdatesUi();
            return;
        }

        _installLaunchInProgress = true;
        RefreshUpdatesUi();
        bool started;
        try
        {
            started = await _installDownloadedUpdateAsync();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            started = false;
        }
        if (started)
            return; // The application context has already initiated a clean exit.

        _installLaunchInProgress = false;
        if (IsDisposed || Disposing) return;
        RefreshUpdatesUi();
        MessageBox.Show(this, L("UpdatesInstallStartError"), Application.ProductName,
            MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void AddUpdatesRow(TableLayoutPanel layout, Control child, int logicalHeight)
    {
        int index = layout.RowCount++;
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, ScaleUi(logicalHeight)));
        child.Margin = new Padding(0);
        layout.Controls.Add(child, 0, index);
    }

    private static TableLayoutPanel CreateUpdatesOptionsRow()
    {
        TableLayoutPanel row = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        return row;
    }

    private Label CreateUpdateLabel(string key, float size, Color color, bool strong = false) => new()
    {
        Text = L(key),
        Dock = DockStyle.Fill,
        Font = new Font(strong ? "Segoe UI Semibold" : "Segoe UI", size),
        ForeColor = color,
        BackColor = Color.Transparent,
        TextAlign = ContentAlignment.MiddleLeft,
        UseCompatibleTextRendering = false,
        AutoEllipsis = false
    };

    private RadioButton CreateUpdateRadio(string key, bool selected, Color color) => new()
    {
        Text = L(key),
        Checked = selected,
        Dock = DockStyle.Fill,
        AutoSize = false,
        ForeColor = color,
        BackColor = Color.Transparent,
        Font = new Font("Segoe UI", 8.5f),
        TextAlign = ContentAlignment.MiddleLeft,
        UseCompatibleTextRendering = false
    };

    private Button CreateUpdateButton(string key, bool dark)
    {
        Button button = new()
        {
            Text = L(key),
            Dock = DockStyle.Fill,
            Margin = ScaleUiPadding(2, 3, 6, 3),
            FlatStyle = FlatStyle.Flat,
            BackColor = dark ? Color.FromArgb(55, 60, 66) : Color.FromArgb(238, 243, 249),
            ForeColor = dark ? Color.WhiteSmoke : LightText,
            Font = new Font("Segoe UI", 8.3f),
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderColor = dark ? DarkBorder : LightBorder;
        return button;
    }

    private void OnUpdateStatusChanged(UpdateSnapshot snapshot)
    {
        if (IsDisposed || Disposing || !IsHandleCreated)
            return;
        try
        {
            BeginInvoke((MethodInvoker)(() =>
            {
                if (!IsDisposed && !Disposing && _currentPage == "About")
                    RefreshUpdatesUi();
            }));
        }
        catch (InvalidOperationException) { }
    }

    private void RefreshUpdatesUi()
    {
        if (_updatesStatusLabel is null || _updatesStatusLabel.IsDisposed || _updateCoordinator is null ||
            _currentPage != "About")
            return;

        UpdateSnapshot snapshot = _updateCoordinator.Snapshot;
        bool pendingChanged = _pendingUpdateChannel != _settings.UpdateChannel ||
            _pendingUpdateDownloadMode != _settings.UpdateDownloadMode;
        string key = pendingChanged ? "UpdatesApplyChanges" :
            snapshot.Status == UpdateStatus.Downloaded && _updateCoordinator.IsReminderScheduled
                ? "UpdatesReminderScheduled" : snapshot.Status switch
        {
            UpdateStatus.Checking => "UpdatesChecking",
            UpdateStatus.UpToDate => "UpdatesCurrent",
            UpdateStatus.Available => "UpdatesAvailable",
            UpdateStatus.Downloading => "UpdatesDownloading",
            UpdateStatus.Downloaded => "UpdatesDownloaded",
            UpdateStatus.InstallationFailed => "UpdatesInstallFailed",
            UpdateStatus.Error => "UpdatesError",
            UpdateStatus.StoreManaged => "UpdatesStoreManaged",
            _ => "UpdatesIdle"
        };
        _updatesStatusLabel.Text = ApplicationRuntime.TestIndicator +
            string.Format(L(key), snapshot.Version ?? string.Empty, snapshot.Percent);
        bool busy = snapshot.Status is UpdateStatus.Checking or UpdateStatus.Downloading;
        if (_updatesCheckButton is { IsDisposed: false })
            _updatesCheckButton.Enabled = !busy && !pendingChanged && !_installLaunchInProgress;
        if (_updatesDownloadButton is { IsDisposed: false })
        {
            _updatesDownloadButton.Visible = snapshot.Status == UpdateStatus.Available;
            _updatesDownloadButton.Enabled = !pendingChanged && !_installLaunchInProgress;
        }
        if (_updatesProgress is { IsDisposed: false })
        {
            _updatesProgress.Visible = snapshot.Status == UpdateStatus.Downloading;
            _updatesProgress.Value = Math.Clamp(snapshot.Percent, 0, 100);
        }
        if (_updatesInstallButton is { IsDisposed: false })
        {
            _updatesInstallButton.Visible = snapshot.Status is UpdateStatus.Downloaded or UpdateStatus.InstallationFailed;
            _updatesInstallButton.Enabled = !ApplicationRuntime.IsTestMode &&
                !pendingChanged && !_installLaunchInProgress &&
                _installDownloadedUpdateAsync is not null;
        }
        if (_updatesLaterButton is { IsDisposed: false })
        {
            _updatesLaterButton.Visible = snapshot.Status is UpdateStatus.Downloaded or UpdateStatus.InstallationFailed;
            _updatesLaterButton.Enabled = !pendingChanged && !_installLaunchInProgress;
        }
    }
}
