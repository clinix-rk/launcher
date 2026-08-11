using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using AppLauncher.Services;
using AppLauncher.Theme;

namespace AppLauncher
{
    public partial class MainForm : Form
    {
        private readonly AppConfig _config;
        private readonly LogService _log = null!;
        private readonly WslService _wsl = null!;
        private readonly HealthCheckService _health = null!;
        private readonly DockerComposeService _compose = null!;
        private readonly SetupService _setup = null!;
        private readonly UpdateService _updates = null!;
        private readonly CrashReportService _crash = null!;

        private System.Windows.Forms.Timer? _statusTimer;
        private System.Windows.Forms.Timer? _autoUpdateTimer;
        private bool _busy;
        private bool _setupReady;
        private bool _updateAvailable;

        public MainForm()
        {
            _config = new AppConfig();
            _config.Load();

            _log = new LogService(_config.LogFilePath);
            _wsl = new WslService(_config.AppRoot, _log);
            _health = new HealthCheckService(_config, _log);
            _compose = new DockerComposeService(_config, _wsl, _health, _log);
            _setup = new SetupService(_config, _wsl, _compose, _log);
            _updates = new UpdateService(_config, _wsl, _compose, _health, _log);
            _crash = new CrashReportService(_config, _log, _wsl, _compose, _updates);

            InitializeComponent();
            AppTheme.ApplyFormChrome(this);
            Paint += MainForm_Paint;
        }

        private async void MainForm_Load(object? sender, EventArgs e)
        {
            _log.LogAppended += OnLogAppended;
            chkAutoUpdate.Checked = _config.AutoUpdateEnabled;
            lblVersion.Text = $"Version: {_updates.CurrentVersion}";
            btnRollback.Enabled = _updates.HasBackupVersion();
            SetOverallStatus("Checking", AppTheme.Warning);
            LayoutResponsive();

            _log.Info("Clinix Launcher started");
            await RunSetupAsync(repair: false);

            if (_setupReady)
            {
                await RefreshStatusAsync();
                await SafeCheckUpdatesAsync(promptIfAvailable: false);
                StartTimers();
            }
        }

        private void MainForm_Paint(object? sender, PaintEventArgs e)
        {
            e.Graphics.Clear(AppTheme.BgDeep);
        }

        private void HeaderPanel_Paint(object? sender, PaintEventArgs e)
        {
            AppTheme.PaintHeaderGradient(e.Graphics, headerPanel.ClientRectangle);
        }

        private void FooterPanel_Paint(object? sender, PaintEventArgs e)
        {
            using var brush = new SolidBrush(AppTheme.BgPanel);
            e.Graphics.FillRectangle(brush, footerPanel.ClientRectangle);
            using var pen = new Pen(AppTheme.Border);
            e.Graphics.DrawLine(pen, 0, 0, footerPanel.Width, 0);
        }

        private void MainForm_Resize(object? sender, EventArgs e) => LayoutResponsive();

        private void LayoutResponsive()
        {
            int contentWidth = Math.Max(640, ClientSize.Width - 56);
            servicesPanel.Width = contentWidth;
            actionsPanel.Width = contentWidth;
            updatesPanel.Width = contentWidth;
            logsPanel.Width = contentWidth;
            logsPanel.Height = Math.Max(160, ClientSize.Height - logsPanel.Top - footerPanel.Height - 16);
            txtLogs.Width = contentWidth;
            txtLogs.Height = Math.Max(120, logsPanel.Height - 32);

            int pillWidth = Math.Max(180, (contentWidth - 40) / 3);
            pillPostgres.Width = pillWidth;
            pillForge.Width = pillWidth;
            pillLens.Width = pillWidth;
            pillForge.Left = pillWidth + 20;
            pillLens.Left = (pillWidth + 20) * 2;

            statusBadge.Left = Math.Max(400, headerPanel.Width - statusBadge.Width - 28);
            btnClearLogs.Left = contentWidth - 120;
            btnCopyLogs.Left = contentWidth - 58;
        }

        private void OnLogAppended(LogEntry entry)
        {
            if (IsDisposed) return;

            void Append()
            {
                Color color = entry.Level switch
                {
                    LogLevel.Success => AppTheme.LogSuccess,
                    LogLevel.Warning => AppTheme.LogWarning,
                    LogLevel.Error => AppTheme.LogError,
                    LogLevel.Command => AppTheme.LogCommand,
                    _ => AppTheme.LogInfo
                };

                txtLogs.SelectionStart = txtLogs.TextLength;
                txtLogs.SelectionLength = 0;
                txtLogs.SelectionColor = color;
                txtLogs.AppendText(entry + Environment.NewLine);
                txtLogs.SelectionStart = txtLogs.TextLength;
                txtLogs.ScrollToCaret();
            }

            if (txtLogs.InvokeRequired)
            {
                try { txtLogs.BeginInvoke(Append); }
                catch { /* Form closing */ }
            }
            else
            {
                Append();
            }
        }

        private async Task RunSetupAsync(bool repair)
        {
            SetBusy(true);
            SetOverallStatus(repair ? "Repairing" : "Checking", AppTheme.Warning);

            try
            {
                _setup.EnsureEnvFile();
                _config.Load();

                SetupResult result = repair
                    ? await _setup.RepairAsync()
                    : await _setup.AssessAsync();

                // Multi-step repair: keep repairing while actionable.
                int guard = 0;
                while (repair && !result.IsReady && result.State != SetupState.Failed && result.State != SetupState.NeedsComposeFile && guard < 4)
                {
                    guard++;
                    result = await _setup.RepairAsync();
                }

                _setupReady = result.IsReady;
                btnRetrySetup.Visible = !_setupReady;

                if (_setupReady)
                {
                    SetOverallStatus("Ready", AppTheme.Success);
                    _log.Success(result.Message);
                }
                else
                {
                    SetOverallStatus("Setup needed", AppTheme.Warning);
                    _log.Warning(result.Message);
                }
            }
            catch (Exception ex)
            {
                _setupReady = false;
                btnRetrySetup.Visible = true;
                SetOverallStatus("Setup failed", AppTheme.Danger);
                _log.Error(ex.Message);
            }
            finally
            {
                SetBusy(false);
                UpdateActionStates();
            }
        }

        private async void BtnRetrySetup_Click(object? sender, EventArgs e)
        {
            await RunSetupAsync(repair: true);
            if (_setupReady)
            {
                await RefreshStatusAsync();
                await SafeCheckUpdatesAsync(promptIfAvailable: false);
                StartTimers();
            }
        }

        private async void BtnStart_Click(object? sender, EventArgs e)
        {
            if (!_setupReady || _busy) return;
            SetBusy(true);
            SetOverallStatus("Starting", AppTheme.Warning);
            try
            {
                bool ok = await _compose.StartAsync();
                SetOverallStatus(ok ? "Running" : "Degraded", ok ? AppTheme.Success : AppTheme.Danger);
            }
            finally
            {
                SetBusy(false);
                await RefreshStatusAsync();
            }
        }

        private async void BtnStop_Click(object? sender, EventArgs e)
        {
            if (!_setupReady || _busy) return;
            SetBusy(true);
            SetOverallStatus("Stopping", AppTheme.Warning);
            try
            {
                await _compose.StopAsync();
                SetOverallStatus("Stopped", AppTheme.TextMuted);
            }
            finally
            {
                SetBusy(false);
                await RefreshStatusAsync();
            }
        }

        private async void BtnOpen_Click(object? sender, EventArgs e)
        {
            await _compose.OpenAppAsync();
        }

        private async void BtnCheckUpdate_Click(object? sender, EventArgs e)
        {
            await SafeCheckUpdatesAsync(promptIfAvailable: true);
        }

        private async void BtnUpdate_Click(object? sender, EventArgs e)
        {
            if (!_setupReady || _busy || !_updateAvailable) return;

            var confirm = MessageBox.Show(
                this,
                $"Update to:\n- Lens: {_updates.LensRemoteTag}\n- Forge: {_updates.ForgeRemoteTag}\n\nServices will restart. Continue?",
                "Confirm Update",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            await RunUpdateAsync();
        }

        private async Task RunUpdateAsync()
        {
            SetBusy(true);
            SetOverallStatus("Updating", AppTheme.Warning);
            try
            {
                bool ok = await _updates.ApplyUpdateAsync();
                lblVersion.Text = $"Version: {_updates.CurrentVersion}";
                btnRollback.Enabled = _updates.HasBackupVersion();
                _updateAvailable = !ok;
                btnUpdate.Enabled = _updateAvailable;
                lblUpdateBanner.Text = ok ? "" : "Update failed — see log";
                SetOverallStatus(ok ? "Running" : "Update failed", ok ? AppTheme.Success : AppTheme.Danger);

                if (ok)
                {
                    MessageBox.Show(this, "Update completed successfully.", "Clinix", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(this, "Update failed. Check the activity log or send a crash report.", "Clinix", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            finally
            {
                SetBusy(false);
                await RefreshStatusAsync();
            }
        }

        private async void BtnRollback_Click(object? sender, EventArgs e)
        {
            if (!_setupReady || _busy || !_updates.HasBackupVersion()) return;

            var confirm = MessageBox.Show(
                this,
                "Rollback to the previous version?",
                "Confirm Rollback",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes) return;

            SetBusy(true);
            SetOverallStatus("Rolling back", AppTheme.Warning);
            try
            {
                bool ok = await _updates.RollbackAsync();
                lblVersion.Text = $"Version: {_updates.CurrentVersion}";
                SetOverallStatus(ok ? "Running" : "Rollback failed", ok ? AppTheme.Success : AppTheme.Danger);
                MessageBox.Show(
                    this,
                    ok ? "Rollback complete." : "Rollback failed. See the activity log.",
                    "Clinix",
                    MessageBoxButtons.OK,
                    ok ? MessageBoxIcon.Information : MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
                await RefreshStatusAsync();
            }
        }

        private async void BtnCrashReport_Click(object? sender, EventArgs e)
        {
            if (_busy) return;
            SetBusy(true);
            try
            {
                var result = await _crash.SendAsync();
                if (result.Success)
                {
                    var open = MessageBox.Show(
                        this,
                        result.IssueUrl != null
                            ? $"Crash report submitted.\n\nOpen issue?\n{result.IssueUrl}"
                            : "Crash report submitted.",
                        "Clinix",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information);

                    if (open == DialogResult.Yes && !string.IsNullOrWhiteSpace(result.IssueUrl))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = result.IssueUrl,
                            UseShellExecute = true
                        });
                    }
                }
                else
                {
                    MessageBox.Show(this, result.Message, "Crash report failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void BtnClearLogs_Click(object? sender, EventArgs e)
        {
            txtLogs.Clear();
            _log.ClearBuffer();
        }

        private void BtnCopyLogs_Click(object? sender, EventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(txtLogs.Text))
                {
                    Clipboard.SetText(txtLogs.Text);
                    _log.Info("Log copied to clipboard");
                }
            }
            catch (Exception ex)
            {
                _log.Warning($"Clipboard copy failed: {ex.Message}");
            }
        }

        private void ChkAutoUpdate_CheckedChanged(object? sender, EventArgs e)
        {
            _config.AutoUpdateEnabled = chkAutoUpdate.Checked;
            if (_autoUpdateTimer != null)
            {
                _autoUpdateTimer.Enabled = _config.AutoUpdateEnabled && _setupReady;
            }
        }

        private void StartTimers()
        {
            _statusTimer ??= new System.Windows.Forms.Timer { Interval = 5000 };
            _statusTimer.Tick -= StatusTimer_Tick;
            _statusTimer.Tick += StatusTimer_Tick;
            _statusTimer.Start();

            int hours = Math.Max(1, _config.AutoUpdateIntervalHours);
            _autoUpdateTimer ??= new System.Windows.Forms.Timer();
            _autoUpdateTimer.Interval = hours * 60 * 60 * 1000;
            _autoUpdateTimer.Tick -= AutoUpdateTimer_Tick;
            _autoUpdateTimer.Tick += AutoUpdateTimer_Tick;
            _autoUpdateTimer.Enabled = _config.AutoUpdateEnabled;
        }

        private async void StatusTimer_Tick(object? sender, EventArgs e)
        {
            if (_busy || !_setupReady) return;
            await RefreshStatusAsync();
        }

        private async void AutoUpdateTimer_Tick(object? sender, EventArgs e)
        {
            if (_busy || !_setupReady || !_config.AutoUpdateEnabled) return;
            await SafeCheckUpdatesAsync(promptIfAvailable: true);
        }

        private async Task SafeCheckUpdatesAsync(bool promptIfAvailable)
        {
            if (!_setupReady) return;

            try
            {
                var result = await _updates.CheckForUpdatesAsync();
                _updateAvailable = result.UpdateAvailable;
                btnUpdate.Enabled = _updateAvailable && !_busy;
                lblUpdateBanner.Text = _updateAvailable ? "Update available" : "";

                if (_updateAvailable && promptIfAvailable)
                {
                    var apply = MessageBox.Show(
                        this,
                        $"{result.Message}\n\nApply update now?",
                        "Update available",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information);

                    if (apply == DialogResult.Yes)
                    {
                        await RunUpdateAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Warning($"Update check failed: {ex.Message}");
            }
        }

        private async Task RefreshStatusAsync()
        {
            if (!_setupReady) return;

            try
            {
                var status = await _compose.GetStatusAsync();
                void Apply()
                {
                    pillPostgres.Health = status.Postgres;
                    pillForge.Health = status.Forge;
                    pillLens.Health = status.Lens;

                    if (!_busy)
                    {
                        if (status.AllHealthy)
                        {
                            SetOverallStatus("Running", AppTheme.Success);
                        }
                        else if (status.AnyRunning)
                        {
                            SetOverallStatus("Degraded", AppTheme.Warning);
                        }
                        else
                        {
                            SetOverallStatus("Stopped", AppTheme.TextMuted);
                        }
                    }

                    UpdateActionStates(status);
                }

                if (InvokeRequired) BeginInvoke(Apply);
                else Apply();
            }
            catch
            {
                // Ignore transient status errors.
            }
        }

        private void SetOverallStatus(string text, Color color)
        {
            statusBadge.SetStatus(text, color);
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            UseWaitCursor = busy;
            UpdateActionStates();
        }

        private void UpdateActionStates(StackStatus? status = null)
        {
            bool canOperate = _setupReady && !_busy;
            btnStart.Enabled = canOperate;
            btnStop.Enabled = canOperate;
            btnOpen.Enabled = canOperate;
            btnCheckUpdate.Enabled = canOperate;
            btnUpdate.Enabled = canOperate && _updateAvailable;
            btnRollback.Enabled = canOperate && _updates.HasBackupVersion();
            btnRetrySetup.Enabled = !_busy;
            btnCrashReport.Enabled = !_busy;
            chkAutoUpdate.Enabled = !_busy;

            if (status != null)
            {
                btnOpen.Enabled = canOperate && status.Lens == ServiceHealth.Healthy;
            }
        }
    }
}
