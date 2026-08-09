namespace AppLauncher
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;
        private Label lblCurrentVersion;
        private Button btnCheckUpdate;
        private Button btnUpdate;
        private Button btnRollback;
        private Button btnStartApp;
        private Label lblStatus;
        private TextBox txtLog;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.lblCurrentVersion = new Label();
            this.btnCheckUpdate = new Button();
            this.btnUpdate = new Button();
            this.btnRollback = new Button();
            this.btnStartApp = new Button();
            this.lblStatus = new Label();
            this.txtLog = new TextBox();

            // lblCurrentVersion
            this.lblCurrentVersion.AutoSize = true;
            this.lblCurrentVersion.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblCurrentVersion.Location = new System.Drawing.Point(12, 9);
            this.lblCurrentVersion.Name = "lblCurrentVersion";
            this.lblCurrentVersion.Size = new System.Drawing.Size(200, 19);
            this.lblCurrentVersion.TabIndex = 0;
            this.lblCurrentVersion.Text = "Current Version: unknown";

            // btnCheckUpdate
            this.btnCheckUpdate.Location = new System.Drawing.Point(12, 40);
            this.btnCheckUpdate.Name = "btnCheckUpdate";
            this.btnCheckUpdate.Size = new System.Drawing.Size(110, 35);
            this.btnCheckUpdate.TabIndex = 1;
            this.btnCheckUpdate.Text = "Check Updates";
            this.btnCheckUpdate.UseVisualStyleBackColor = true;
            this.btnCheckUpdate.Click += new System.EventHandler(this.btnCheckUpdate_Click);

            // btnUpdate
            this.btnUpdate.Location = new System.Drawing.Point(128, 40);
            this.btnUpdate.Name = "btnUpdate";
            this.btnUpdate.Size = new System.Drawing.Size(110, 35);
            this.btnUpdate.TabIndex = 2;
            this.btnUpdate.Text = "Update";
            this.btnUpdate.UseVisualStyleBackColor = true;
            this.btnUpdate.Enabled = false;
            this.btnUpdate.Click += new System.EventHandler(this.btnUpdate_Click);

            // btnRollback
            this.btnRollback.Location = new System.Drawing.Point(244, 40);
            this.btnRollback.Name = "btnRollback";
            this.btnRollback.Size = new System.Drawing.Size(110, 35);
            this.btnRollback.TabIndex = 3;
            this.btnRollback.Text = "Rollback";
            this.btnRollback.UseVisualStyleBackColor = true;
            this.btnRollback.Enabled = false;
            this.btnRollback.Click += new System.EventHandler(this.btnRollback_Click);

            // btnStartApp
            this.btnStartApp.Location = new System.Drawing.Point(360, 40);
            this.btnStartApp.Name = "btnStartApp";
            this.btnStartApp.Size = new System.Drawing.Size(110, 35);
            this.btnStartApp.TabIndex = 4;
            this.btnStartApp.Text = "Start App";
            this.btnStartApp.UseVisualStyleBackColor = true;
            this.btnStartApp.Click += new System.EventHandler(this.btnStartApp_Click);

            // lblStatus
            this.lblStatus.AutoSize = true;
            this.lblStatus.Location = new System.Drawing.Point(12, 85);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(50, 15);
            this.lblStatus.TabIndex = 5;
            this.lblStatus.Text = "Ready.";

            // txtLog
            this.txtLog.Location = new System.Drawing.Point(12, 110);
            this.txtLog.Multiline = true;
            this.txtLog.Name = "txtLog";
            this.txtLog.ReadOnly = true;
            this.txtLog.ScrollBars = ScrollBars.Vertical;
            this.txtLog.Size = new System.Drawing.Size(458, 200);
            this.txtLog.TabIndex = 6;

            // MainForm
            this.ClientSize = new System.Drawing.Size(480, 330);
            this.Controls.Add(this.lblCurrentVersion);
            this.Controls.Add(this.btnCheckUpdate);
            this.Controls.Add(this.btnUpdate);
            this.Controls.Add(this.btnRollback);
            this.Controls.Add(this.btnStartApp);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.txtLog);
            this.Name = "MainForm";
            this.Text = "Application Launcher";
            this.Load += new System.EventHandler(this.MainForm_Load);
        }
    }
}
