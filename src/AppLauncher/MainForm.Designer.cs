using System.Drawing;
using System.Windows.Forms;
using AppLauncher.Controls;
using AppLauncher.Theme;

namespace AppLauncher
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer? components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _statusTimer?.Dispose();
                _autoUpdateTimer?.Dispose();
                _compose?.StopLogTail();
                components?.Dispose();
            }

            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();

            headerPanel = new Panel();
            lblBrand = new Label();
            lblSubtitle = new Label();
            lblVersion = new Label();
            statusBadge = new StatusBadge();

            servicesPanel = new Panel();
            pillPostgres = new ServicePill { ServiceName = "Postgres" };
            pillForge = new ServicePill { ServiceName = "Forge" };
            pillLens = new ServicePill { ServiceName = "Lens" };

            actionsPanel = new Panel();
            btnStart = new FlatButton { Text = "Start", IsPrimary = true };
            btnStop = new FlatButton { Text = "Stop" };
            btnOpen = new FlatButton { Text = "Open App" };
            btnRetrySetup = new FlatButton { Text = "Retry Setup", IsPrimary = true };

            updatesPanel = new Panel();
            lblUpdates = new Label();
            btnCheckUpdate = new FlatButton { Text = "Check Updates" };
            btnUpdate = new FlatButton { Text = "Update" };
            btnRollback = new FlatButton { Text = "Rollback" };
            chkAutoUpdate = new CheckBox();
            lblUpdateBanner = new Label();

            progressPanel = new Panel();
            lblProgressTitle = new Label();
            lblProgressStep = new Label();
            lblProgressHint = new Label();
            progressBar = new ProgressBar();

            logsPanel = new Panel();
            lblLogs = new Label();
            btnClearLogs = new FlatButton { Text = "Clear" };
            btnCopyLogs = new FlatButton { Text = "Copy" };
            txtLogs = new RichTextBox();

            footerPanel = new Panel();
            btnCrashReport = new FlatButton { Text = "Send Crash Report" };
            lblFooter = new Label();

            SuspendLayout();

            // Form
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(860, 700);
            MinimumSize = new Size(860, 700);
            Name = "MainForm";
            Text = "Clinix Launcher";
            DoubleBuffered = true;

            // Header
            headerPanel.Location = new Point(0, 0);
            headerPanel.Size = new Size(860, 110);
            headerPanel.Dock = DockStyle.Top;
            headerPanel.Paint += HeaderPanel_Paint;

            lblBrand.AutoSize = true;
            lblBrand.Font = AppTheme.TitleFont;
            lblBrand.ForeColor = AppTheme.Navy;
            lblBrand.Location = new Point(28, 22);
            lblBrand.Text = "Clinix";
            lblBrand.BackColor = Color.Transparent;

            lblSubtitle.AutoSize = true;
            lblSubtitle.Font = AppTheme.SubtitleFont;
            lblSubtitle.ForeColor = AppTheme.TextSecondary;
            lblSubtitle.Location = new Point(32, 62);
            lblSubtitle.Text = "Clinic stack launcher for Docker on WSL";
            lblSubtitle.BackColor = Color.Transparent;

            lblVersion.AutoSize = true;
            lblVersion.Font = AppTheme.SmallFont;
            lblVersion.ForeColor = AppTheme.TextMuted;
            lblVersion.Location = new Point(32, 82);
            lblVersion.Text = "Version: unknown";
            lblVersion.BackColor = Color.Transparent;

            statusBadge.Location = new Point(620, 40);
            statusBadge.Size = new Size(210, 28);
            statusBadge.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            headerPanel.Controls.Add(lblBrand);
            headerPanel.Controls.Add(lblSubtitle);
            headerPanel.Controls.Add(lblVersion);
            headerPanel.Controls.Add(statusBadge);

            // Services
            servicesPanel.Location = new Point(28, 128);
            servicesPanel.Size = new Size(804, 70);
            servicesPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            servicesPanel.BackColor = Color.Transparent;

            pillPostgres.Location = new Point(0, 8);
            pillPostgres.Size = new Size(250, 56);
            pillForge.Location = new Point(270, 8);
            pillForge.Size = new Size(250, 56);
            pillLens.Location = new Point(540, 8);
            pillLens.Size = new Size(250, 56);

            servicesPanel.Controls.Add(pillPostgres);
            servicesPanel.Controls.Add(pillForge);
            servicesPanel.Controls.Add(pillLens);

            // Actions
            actionsPanel.Location = new Point(28, 210);
            actionsPanel.Size = new Size(804, 44);
            actionsPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            actionsPanel.BackColor = Color.Transparent;

            btnStart.Location = new Point(0, 4);
            btnStart.Size = new Size(120, 36);
            btnStart.Click += BtnStart_Click;

            btnStop.Location = new Point(132, 4);
            btnStop.Size = new Size(120, 36);
            btnStop.Click += BtnStop_Click;

            btnOpen.Location = new Point(264, 4);
            btnOpen.Size = new Size(120, 36);
            btnOpen.Click += BtnOpen_Click;

            btnRetrySetup.Location = new Point(396, 4);
            btnRetrySetup.Size = new Size(140, 36);
            btnRetrySetup.Visible = false;
            btnRetrySetup.Click += BtnRetrySetup_Click;

            actionsPanel.Controls.Add(btnStart);
            actionsPanel.Controls.Add(btnStop);
            actionsPanel.Controls.Add(btnOpen);
            actionsPanel.Controls.Add(btnRetrySetup);

            // Updates
            updatesPanel.Location = new Point(28, 268);
            updatesPanel.Size = new Size(804, 78);
            updatesPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            updatesPanel.BackColor = Color.Transparent;

            lblUpdates.AutoSize = true;
            lblUpdates.Font = AppTheme.SectionFont;
            lblUpdates.ForeColor = AppTheme.TextSecondary;
            lblUpdates.Location = new Point(0, 0);
            lblUpdates.Text = "Updates";

            btnCheckUpdate.Location = new Point(0, 28);
            btnCheckUpdate.Size = new Size(130, 36);
            btnCheckUpdate.Click += BtnCheckUpdate_Click;

            btnUpdate.Location = new Point(142, 28);
            btnUpdate.Size = new Size(110, 36);
            btnUpdate.Enabled = false;
            btnUpdate.Click += BtnUpdate_Click;

            btnRollback.Location = new Point(264, 28);
            btnRollback.Size = new Size(110, 36);
            btnRollback.Click += BtnRollback_Click;

            chkAutoUpdate.AutoSize = true;
            chkAutoUpdate.Text = "Auto-check updates";
            chkAutoUpdate.ForeColor = AppTheme.TextSecondary;
            chkAutoUpdate.Location = new Point(400, 36);
            chkAutoUpdate.BackColor = Color.Transparent;
            chkAutoUpdate.CheckedChanged += ChkAutoUpdate_CheckedChanged;

            lblUpdateBanner.AutoSize = true;
            lblUpdateBanner.Font = AppTheme.SmallFont;
            lblUpdateBanner.ForeColor = AppTheme.Warning;
            lblUpdateBanner.Location = new Point(560, 38);
            lblUpdateBanner.Text = "";
            lblUpdateBanner.BackColor = Color.Transparent;

            updatesPanel.Controls.Add(lblUpdates);
            updatesPanel.Controls.Add(btnCheckUpdate);
            updatesPanel.Controls.Add(btnUpdate);
            updatesPanel.Controls.Add(btnRollback);
            updatesPanel.Controls.Add(chkAutoUpdate);
            updatesPanel.Controls.Add(lblUpdateBanner);

            // Progress (shown during long operations)
            progressPanel.Location = new Point(28, 352);
            progressPanel.Size = new Size(804, 100);
            progressPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            progressPanel.BackColor = AppTheme.BgPanel;
            progressPanel.Visible = false;
            progressPanel.Paint += ProgressPanel_Paint;

            lblProgressTitle.AutoSize = true;
            lblProgressTitle.Font = AppTheme.SectionFont;
            lblProgressTitle.ForeColor = AppTheme.Navy;
            lblProgressTitle.Location = new Point(12, 8);
            lblProgressTitle.Text = "Working…";
            lblProgressTitle.BackColor = Color.Transparent;

            lblProgressStep.AutoSize = false;
            lblProgressStep.Font = AppTheme.BodyFont;
            lblProgressStep.ForeColor = AppTheme.TextPrimary;
            lblProgressStep.Location = new Point(12, 28);
            lblProgressStep.Size = new Size(780, 36);
            lblProgressStep.Text = "";
            lblProgressStep.BackColor = Color.Transparent;

            progressBar.Location = new Point(12, 70);
            progressBar.Size = new Size(560, 16);
            progressBar.Style = ProgressBarStyle.Continuous;
            progressBar.Minimum = 0;
            progressBar.Maximum = 100;
            progressBar.Value = 0;

            lblProgressHint.AutoSize = true;
            lblProgressHint.Font = AppTheme.SmallFont;
            lblProgressHint.ForeColor = AppTheme.TextMuted;
            lblProgressHint.Location = new Point(580, 70);
            lblProgressHint.Text = "This can take several minutes.";
            lblProgressHint.BackColor = Color.Transparent;

            progressPanel.Controls.Add(lblProgressTitle);
            progressPanel.Controls.Add(lblProgressStep);
            progressPanel.Controls.Add(progressBar);
            progressPanel.Controls.Add(lblProgressHint);

            // Logs
            logsPanel.Location = new Point(28, 358);
            logsPanel.Size = new Size(804, 220);
            logsPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            logsPanel.BackColor = Color.Transparent;

            lblLogs.AutoSize = true;
            lblLogs.Font = AppTheme.SectionFont;
            lblLogs.ForeColor = AppTheme.TextSecondary;
            lblLogs.Location = new Point(0, 0);
            lblLogs.Text = "Activity log";

            btnClearLogs.Location = new Point(680, -4);
            btnClearLogs.Size = new Size(54, 28);
            btnClearLogs.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnClearLogs.Click += BtnClearLogs_Click;

            btnCopyLogs.Location = new Point(742, -4);
            btnCopyLogs.Size = new Size(54, 28);
            btnCopyLogs.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnCopyLogs.Click += BtnCopyLogs_Click;

            txtLogs.Location = new Point(0, 28);
            txtLogs.Size = new Size(804, 188);
            txtLogs.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            txtLogs.ReadOnly = true;
            txtLogs.BorderStyle = BorderStyle.FixedSingle;
            txtLogs.BackColor = AppTheme.LogBg;
            txtLogs.ForeColor = AppTheme.LogInfo;
            txtLogs.Font = SafeMonoFont();
            txtLogs.DetectUrls = true;
            txtLogs.ScrollBars = RichTextBoxScrollBars.Vertical;
            txtLogs.HideSelection = false;

            logsPanel.Controls.Add(lblLogs);
            logsPanel.Controls.Add(btnClearLogs);
            logsPanel.Controls.Add(btnCopyLogs);
            logsPanel.Controls.Add(txtLogs);

            // Footer
            footerPanel.Location = new Point(0, 650);
            footerPanel.Size = new Size(860, 50);
            footerPanel.Dock = DockStyle.Bottom;
            footerPanel.BackColor = AppTheme.BgPanel;
            footerPanel.Paint += FooterPanel_Paint;

            btnCrashReport.Location = new Point(28, 8);
            btnCrashReport.Size = new Size(160, 34);
            btnCrashReport.Click += BtnCrashReport_Click;

            lblFooter.AutoSize = true;
            lblFooter.Font = AppTheme.SmallFont;
            lblFooter.ForeColor = AppTheme.TextMuted;
            lblFooter.Location = new Point(210, 16);
            lblFooter.Text = "Reports open a GitHub issue with diagnostics for the Clinix team.";
            lblFooter.BackColor = Color.Transparent;

            footerPanel.Controls.Add(btnCrashReport);
            footerPanel.Controls.Add(lblFooter);

            Controls.Add(logsPanel);
            Controls.Add(progressPanel);
            Controls.Add(updatesPanel);
            Controls.Add(actionsPanel);
            Controls.Add(servicesPanel);
            Controls.Add(footerPanel);
            Controls.Add(headerPanel);

            Load += MainForm_Load;
            Resize += MainForm_Resize;

            ResumeLayout(false);
        }

        private static Font SafeMonoFont()
        {
            try
            {
                return new Font("Cascadia Mono", 9F, FontStyle.Regular);
            }
            catch
            {
                try
                {
                    return new Font("Consolas", 9F, FontStyle.Regular);
                }
                catch
                {
                    return AppTheme.BodyFont;
                }
            }
        }

        private Panel headerPanel = null!;
        private Label lblBrand = null!;
        private Label lblSubtitle = null!;
        private Label lblVersion = null!;
        private StatusBadge statusBadge = null!;

        private Panel servicesPanel = null!;
        private ServicePill pillPostgres = null!;
        private ServicePill pillForge = null!;
        private ServicePill pillLens = null!;

        private Panel actionsPanel = null!;
        private FlatButton btnStart = null!;
        private FlatButton btnStop = null!;
        private FlatButton btnOpen = null!;
        private FlatButton btnRetrySetup = null!;

        private Panel updatesPanel = null!;
        private Label lblUpdates = null!;
        private FlatButton btnCheckUpdate = null!;
        private FlatButton btnUpdate = null!;
        private FlatButton btnRollback = null!;
        private CheckBox chkAutoUpdate = null!;
        private Label lblUpdateBanner = null!;

        private Panel progressPanel = null!;
        private Label lblProgressTitle = null!;
        private Label lblProgressStep = null!;
        private Label lblProgressHint = null!;
        private ProgressBar progressBar = null!;

        private Panel logsPanel = null!;
        private Label lblLogs = null!;
        private FlatButton btnClearLogs = null!;
        private FlatButton btnCopyLogs = null!;
        private RichTextBox txtLogs = null!;

        private Panel footerPanel = null!;
        private FlatButton btnCrashReport = null!;
        private Label lblFooter = null!;
    }
}
