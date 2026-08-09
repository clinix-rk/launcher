using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using DotNetEnv;

namespace AppLauncher
{
    public partial class MainForm : Form
    {
        private const string BRANCH = "release";
        private const string APP_URL = "http://localhost";
        private const int HEALTH_CHECK_TIMEOUT_SEC = 30;
        private const int CONTAINER_STARTUP_DELAY_SEC = 5;
        
        private readonly string appRoot = AppDomain.CurrentDomain.BaseDirectory;
        private readonly string versionFile;
        private readonly string backupVersionFile;
        private readonly string logFile;
        private readonly string envFilePath;

        private string currentVersion = "unknown";
        private string lensRemoteTag = "";
        private string forgeRemoteTag = "";

        public MainForm()
        {
            InitializeComponent();
            
            versionFile = Path.Combine(appRoot, "current_version.txt");
            backupVersionFile = Path.Combine(appRoot, "backup_version.txt");
            logFile = Path.Combine(appRoot, "launcher.log");
            envFilePath = Path.Combine(appRoot, ".env");
        }

        private async void MainForm_Load(object sender, EventArgs e)
        {
            LoadEnvironmentVariables();
            LoadCurrentVersion();
            lblCurrentVersion.Text = $"Current Version: {currentVersion}";
            LogMessage("Launcher started");
            await CheckForUpdatesAsync();
        }

        private void LoadEnvironmentVariables()
        {
            if (File.Exists(envFilePath))
            {
                try
                {
                    Env.Load(envFilePath);
                    LogMessage(".env file loaded successfully");
                }
                catch (Exception ex)
                {
                    LogMessage($"Warning: Failed to load .env file: {ex.Message}");
                }
            }
            else
            {
                LogMessage("Warning: .env file not found. Using defaults or system environment variables.");
            }
        }

        private void LoadCurrentVersion()
        {
            if (File.Exists(versionFile))
            {
                currentVersion = File.ReadAllText(versionFile).Trim();
            }
        }

        private void LogMessage(string message)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string logEntry = $"[{timestamp}] {message}";
            
            try
            {
                File.AppendAllText(logFile, logEntry + Environment.NewLine);
            }
            catch { /* Silently fail if log write fails */ }
            
            lblStatus.Text = message;
        }

        private async void btnCheckUpdate_Click(object sender, EventArgs e)
        {
            await CheckForUpdatesAsync();
        }

        private async Task CheckForUpdatesAsync()
        {
            LogMessage("Checking for updates from GitHub Container Registry...");
            btnUpdate.Enabled = false;

            try
            {
                bool updateAvailable = await Task.Run(async () =>
                {
                    // Fetch latest tags from GHCR
                    lensRemoteTag = await GetLatestGhcrTag("lens");
                    forgeRemoteTag = await GetLatestGhcrTag("forge");

                    if (string.IsNullOrEmpty(lensRemoteTag) || string.IsNullOrEmpty(forgeRemoteTag))
                    {
                        LogMessage("Failed to fetch remote tags from GHCR");
                        return false;
                    }

                    string remoteVersion = $"lens:{lensRemoteTag}|forge:{forgeRemoteTag}";
                    bool updateAvailable = currentVersion != remoteVersion;

                    if (updateAvailable)
                    {
                        LogMessage($"Updates available - Lens: {lensRemoteTag}, Forge: {forgeRemoteTag}");
                    }

                    return updateAvailable;
                });

                if (updateAvailable)
                {
                    LogMessage("Click 'Update' to deploy the latest versions");
                    btnUpdate.Enabled = true;
                }
                else
                {
                    LogMessage("All services are up to date");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error checking updates: {ex.Message}");
            }
        }

        private async Task<string> GetLatestGhcrTag(string serviceName)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    // Try to fetch latest tag from image
                    string apiUrl = $"https://ghcr.io/v2/{GetOrgFromRegistry()}/{serviceName}/tags/list";

                    var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
                    request.Headers.Add("Accept", "application/json");

                    var response = await client.SendAsync(request);

                    if (response.IsSuccessStatusCode)
                    {
                        string content = await response.Content.ReadAsStringAsync();

                        // Parse JSON response to get tags
                        // Example response: {"name":"forge","tags":["latest","v1.0.0","abc1234"]}
                        if (content.Contains("\"latest\""))
                        {
                            // If "latest" tag exists, use it (updated most recently)
                            return "latest";
                        }

                        // Fallback: extract all tags and find most recent
                        var tags = ExtractTagsFromJson(content);
                        if (tags.Count > 0)
                        {
                            // Return first tag (should be latest if sorted by API)
                            return tags[0];
                        }
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        LogMessage($"Warning: No authentication for GHCR. Using 'latest' tag.");
                        return "latest";
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Failed to fetch tag for {serviceName}: {ex.Message}. Using 'latest'");
            }

            return "latest"; // Default fallback
        }

        private string GetOrgFromRegistry()
        {
            return "clinix-rk";
        }
        
        private List<string> ExtractTagsFromJson(string json)
        {
            var tags = new List<string>();
            try
            {
                // Simple JSON parsing for tags array
                int tagsIndex = json.IndexOf("\"tags\":[");
                if (tagsIndex > 0)
                {
                    int start = tagsIndex + 8;
                    int end = json.IndexOf("]", start);
                    string tagsString = json.Substring(start, end - start);
                    
                    foreach (var tag in tagsString.Split(','))
                    {
                        string cleanTag = tag.Trim().Trim('"');
                        if (!string.IsNullOrEmpty(cleanTag) && cleanTag != "null")
                        {
                            tags.Add(cleanTag);
                        }
                    }
                }
            }
            catch { /* Silently fail */ }
            
            return tags;
        }

        private async void btnUpdate_Click(object sender, EventArgs e)
        {
            DialogResult confirm = MessageBox.Show(
                $"Update to:\n- Lens: {lensRemoteTag}\n- Forge: {forgeRemoteTag}\n\nContinue?",
                "Confirm Update",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (confirm != DialogResult.Yes) return;

            btnUpdate.Enabled = false;
            btnStartApp.Enabled = false;
            btnRollback.Enabled = false;

            try
            {
                // Save current version for rollback
                File.WriteAllText(backupVersionFile, currentVersion);
                LogMessage("Backup version saved");

                LogMessage("Step 1/5: Stopping services...");
                await Task.Run(() => RunWslCommand("docker compose down"));

                LogMessage("Step 2/5: Backing up database...");
                await BackupDatabase();

                LogMessage("Step 3/5: Pulling latest images from GHCR...");
                bool pullSuccess = await PullLatestImages();
                if (!pullSuccess)
                {
                    LogMessage("Failed to pull images. Rolling back...");
                    await RollbackToPreviousVersion();
                    MessageBox.Show("Image pull failed. Rolled back to previous version.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                LogMessage("Step 4/5: Starting containers...");
                await Task.Run(() => RunWslCommand("docker compose up -d"));

                LogMessage("Step 5/5: Verifying health...");
                bool isHealthy = await VerifyAppHealthAsync();

                if (!isHealthy)
                {
                    LogMessage("Health check failed. Rolling back...");
                    await RollbackToPreviousVersion();
                    MessageBox.Show("Health checks failed. Rolled back to previous version.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Record successful update
                string newVersion = $"lens:{lensRemoteTag}|forge:{forgeRemoteTag}";
                File.WriteAllText(versionFile, newVersion);
                currentVersion = newVersion;
                lblCurrentVersion.Text = $"Current Version: {currentVersion}";

                LogMessage($"✓ Update complete. Running Lens: {lensRemoteTag}, Forge: {forgeRemoteTag}");
                btnStartApp.Enabled = true;
                btnRollback.Enabled = true;
                MessageBox.Show($"Successfully updated!\nLens: {lensRemoteTag}\nForge: {forgeRemoteTag}", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                LogMessage($"Critical error during update: {ex.Message}");
                MessageBox.Show($"Update failed:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task<bool> PullLatestImages()
        {
            try
            {
                // Pull Lens image
                string lensImage = $"ghcr.io/YOUR_ORG/lens:{lensRemoteTag}";
                string pullLensCmd = $"docker pull {lensImage}";
                await RunWslCommandWithTimeoutAsync(pullLensCmd, timeoutSeconds: 300);

                // Pull Forge image
                string forgeImage = $"ghcr.io/YOUR_ORG/forge:{forgeRemoteTag}";
                string pullForgeCmd = $"docker pull {forgeImage}";
                await RunWslCommandWithTimeoutAsync(pullForgeCmd, timeoutSeconds: 300);

                return true;
            }
            catch (Exception ex)
            {
                LogMessage($"Image pull failed: {ex.Message}");
                return false;
            }
        }

        private async Task BackupDatabase()
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupCommand = $"docker run --rm -v postgres_data:/data -v {appRoot}:/backup " +
                    $"alpine tar czf /backup/db_backup_{timestamp}.tar.gz /data";

                await Task.Run(() => RunWslCommand(backupCommand));
                LogMessage($"Database backed up to db_backup_{timestamp}.tar.gz");
            }
            catch (Exception ex)
            {
                LogMessage($"Database backup warning: {ex.Message}");
            }
        }

        private async Task<bool> VerifyAppHealthAsync()
        {
            await Task.Delay(CONTAINER_STARTUP_DELAY_SEC * 1000);

            using (HttpClient client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(HEALTH_CHECK_TIMEOUT_SEC);
                
                try
                {
                    // Check Forge (Backend)
                    LogMessage("Checking backend health...");
                    var forgeResponse = await client.GetAsync($"{APP_URL}/api/health");
                    if (!forgeResponse.IsSuccessStatusCode)
                    {
                        LogMessage("Backend health check failed");
                        return false;
                    }

                    // Check Lens (Frontend)
                    LogMessage("Checking frontend health...");
                    var lensResponse = await client.GetAsync($"{APP_URL}/");
                    if (!lensResponse.IsSuccessStatusCode)
                    {
                        LogMessage("Frontend health check failed");
                        return false;
                    }

                    LogMessage("✓ All health checks passed");
                    return true;
                }
                catch (Exception ex)
                {
                    LogMessage($"Health check exception: {ex.Message}");
                    return false;
                }
            }
        }

        private async Task RollbackToPreviousVersion()
        {
            try
            {
                if (!File.Exists(backupVersionFile))
                {
                    LogMessage("No backup version found");
                    return;
                }

                string backupVersion = File.ReadAllText(backupVersionFile).Trim();
                LogMessage($"Rolling back to: {backupVersion}");

                await Task.Run(() =>
                {
                    RunWslCommand("docker compose down");
                });

                // Extract image tags from backup version
                var parts = backupVersion.Split('|');
                if (parts.Length == 2)
                {
                    string lensTag = parts[0].Replace("lens:", "");
                    string forgeTag = parts[1].Replace("forge:", "");

                    string pullCmd = $"docker pull ghcr.io/YOUR_ORG/lens:{lensTag} && " +
                                    $"docker pull ghcr.io/YOUR_ORG/forge:{forgeTag}";
                    await RunWslCommandWithTimeoutAsync(pullCmd, timeoutSeconds: 300);

                    await Task.Run(() => RunWslCommand("docker compose up -d"));
                }

                await Task.Delay(CONTAINER_STARTUP_DELAY_SEC * 1000);
                await VerifyAppHealthAsync();
                LogMessage("✓ Rollback complete");
            }
            catch (Exception ex)
            {
                LogMessage($"Rollback failed: {ex.Message}. Manual intervention required.");
            }
        }

        private async void btnRollback_Click(object sender, EventArgs e)
        {
            DialogResult confirm = MessageBox.Show(
                "Rollback to previous version?",
                "Confirm Rollback",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (confirm != DialogResult.Yes) return;

            if (File.Exists(backupVersionFile))
            {
                LogMessage("Initiating rollback...");
                btnRollback.Enabled = false;
                btnUpdate.Enabled = false;
                btnStartApp.Enabled = false;

                await RollbackToPreviousVersion();

                string backupVersion = File.ReadAllText(backupVersionFile).Trim();
                currentVersion = backupVersion;
                lblCurrentVersion.Text = $"Current Version: {currentVersion}";

                btnStartApp.Enabled = true;
                btnRollback.Enabled = true;
                MessageBox.Show("Rollback complete.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("No previous version found for rollback.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async void btnStartApp_Click(object sender, EventArgs e)
        {
            LogMessage("Ensuring containers are running...");

            await Task.Run(() => RunWslCommand("docker compose up -d"));
            await Task.Delay(3000);

            bool isHealthy = await VerifyAppHealthAsync();
            if (!isHealthy)
            {
                MessageBox.Show("Application failed to start. Check Docker logs.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                LogMessage("Application startup health check failed");
                return;
            }

            LogMessage("Opening application in browser...");
            Process.Start(new ProcessStartInfo
            {
                FileName = APP_URL,
                UseShellExecute = true
            });

            LogMessage($"✓ Application running at {APP_URL}");
        }

        private async Task<bool> RunWslCommandWithTimeoutAsync(string command, int timeoutSeconds)
        {
            return await Task.Run(() =>
            {
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = "wsl.exe",
                        Arguments = command,
                        WorkingDirectory = appRoot,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using (Process p = Process.Start(psi))
                    {
                        if (!p.WaitForExit(timeoutSeconds * 1000))
                        {
                            p.Kill();
                            LogMessage($"Command timed out after {timeoutSeconds} seconds");
                            return false;
                        }

                        if (p.ExitCode != 0)
                        {
                            string error = p.StandardError.ReadToEnd();
                            LogMessage($"Command failed: {error}");
                            return false;
                        }

                        return true;
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"Command execution failed: {ex.Message}");
                    return false;
                }
            });
        }

        private string RunWslCommand(string command)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "wsl.exe",
                    Arguments = command,
                    WorkingDirectory = appRoot,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process p = Process.Start(psi))
                {
                    string output = p.StandardOutput.ReadToEnd();
                    string error = p.StandardError.ReadToEnd();
                    p.WaitForExit();

                    if (p.ExitCode != 0 && !string.IsNullOrEmpty(error))
                    {
                        LogMessage($"WSL Command error: {error}");
                    }

                    return output;
                }
            }
            catch (Exception ex)
            {
                LogMessage($"WSL exception: {ex.Message}");
                return $"Exception: {ex.Message}";
            }
        }
    }
}
