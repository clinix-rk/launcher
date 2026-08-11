using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AppLauncher.Services
{
    public sealed class UpdateCheckResult
    {
        public bool UpdateAvailable { get; init; }
        public string CurrentVersion { get; init; } = "unknown";
        public string RemoteVersion { get; init; } = "";
        public string LensTag { get; init; } = "latest";
        public string ForgeTag { get; init; } = "latest";
        public string Message { get; init; } = "";
    }

    public sealed class UpdateService
    {
        private readonly AppConfig _config;
        private readonly WslService _wsl;
        private readonly DockerComposeService _compose;
        private readonly HealthCheckService _health;
        private readonly LogService _log;
        private static readonly HttpClient Http = new();

        public string CurrentVersion { get; private set; } = "unknown";
        public string LensRemoteTag { get; private set; } = "latest";
        public string ForgeRemoteTag { get; private set; } = "latest";

        public UpdateService(
            AppConfig config,
            WslService wsl,
            DockerComposeService compose,
            HealthCheckService health,
            LogService log)
        {
            _config = config;
            _wsl = wsl;
            _compose = compose;
            _health = health;
            _log = log;
            LoadCurrentVersion();
        }

        public void LoadCurrentVersion()
        {
            if (File.Exists(_config.VersionFilePath))
            {
                CurrentVersion = File.ReadAllText(_config.VersionFilePath).Trim();
            }
            else
            {
                CurrentVersion = "unknown";
            }
        }

        public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
        {
            _log.Info("Checking for updates from GitHub Container Registry...");

            LensRemoteTag = await GetLatestGhcrTagAsync("lens", cancellationToken);
            ForgeRemoteTag = await GetLatestGhcrTagAsync("forge", cancellationToken);

            string remoteVersion = $"lens:{LensRemoteTag}|forge:{ForgeRemoteTag}";
            bool updateAvailable = !string.Equals(CurrentVersion, remoteVersion, StringComparison.Ordinal);

            if (updateAvailable)
            {
                _log.Info($"Updates available — Lens: {LensRemoteTag}, Forge: {ForgeRemoteTag}");
            }
            else
            {
                _log.Success("All services are up to date");
            }

            return new UpdateCheckResult
            {
                UpdateAvailable = updateAvailable,
                CurrentVersion = CurrentVersion,
                RemoteVersion = remoteVersion,
                LensTag = LensRemoteTag,
                ForgeTag = ForgeRemoteTag,
                Message = updateAvailable
                    ? $"Update available: {remoteVersion}"
                    : "Up to date"
            };
        }

        public async Task<bool> ApplyUpdateAsync(CancellationToken cancellationToken = default)
        {
            _compose.StopLogTail();

            try
            {
                File.WriteAllText(_config.BackupVersionFilePath, CurrentVersion);
                _log.Info("Backup version saved");

                _log.Info("Step 1/5: Stopping services...");
                await _compose.StopAsync(cancellationToken);

                _log.Info("Step 2/5: Backing up database...");
                await _compose.BackupDatabaseAsync(cancellationToken);

                _log.Info("Step 3/5: Pulling latest images...");
                bool pullOk = await _compose.PullAsync(cancellationToken);
                if (!pullOk)
                {
                    _log.Error("Image pull failed. Rolling back...");
                    await RollbackAsync(cancellationToken);
                    return false;
                }

                _log.Info("Step 4/5: Starting containers...");
                var up = await _wslComposeUp(cancellationToken);
                if (!up)
                {
                    _log.Error("Failed to start containers after pull. Rolling back...");
                    await RollbackAsync(cancellationToken);
                    return false;
                }

                _log.Info("Step 5/5: Verifying health...");
                bool healthy = await _health.VerifyAppHealthAsync(
                    startupDelaySeconds: 8,
                    timeoutSeconds: 120,
                    cancellationToken: cancellationToken);

                if (!healthy)
                {
                    _log.Error("Health checks failed. Rolling back...");
                    await RollbackAsync(cancellationToken);
                    return false;
                }

                string newVersion = $"lens:{LensRemoteTag}|forge:{ForgeRemoteTag}";
                File.WriteAllText(_config.VersionFilePath, newVersion);
                CurrentVersion = newVersion;
                _log.Success($"Update complete — {newVersion}");
                _compose.StartLogTail();
                return true;
            }
            catch (Exception ex)
            {
                _log.Error($"Critical error during update: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> RollbackAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (!File.Exists(_config.BackupVersionFilePath))
                {
                    _log.Warning("No backup version found");
                    return false;
                }

                string backupVersion = File.ReadAllText(_config.BackupVersionFilePath).Trim();
                _log.Info($"Rolling back to: {backupVersion}");

                await _compose.StopAsync(cancellationToken);

                var parts = backupVersion.Split('|');
                if (parts.Length == 2)
                {
                    string lensTag = parts[0].Replace("lens:", "", StringComparison.OrdinalIgnoreCase);
                    string forgeTag = parts[1].Replace("forge:", "", StringComparison.OrdinalIgnoreCase);
                    string org = _config.GhcrOrg;

                    string pullCmd =
                        $"docker pull ghcr.io/{org}/lens:{lensTag} && " +
                        $"docker pull ghcr.io/{org}/forge:{forgeTag}";

                    var pull = await _wsl.RunBashAsync(
                        pullCmd,
                        timeoutSeconds: 600,
                        cancellationToken: cancellationToken);

                    if (!pull.Success)
                    {
                        _log.Error("Failed to pull rollback images");
                        return false;
                    }
                }

                bool started = await _wslComposeUp(cancellationToken);
                if (!started)
                {
                    return false;
                }

                await _health.VerifyAppHealthAsync(startupDelaySeconds: 5, timeoutSeconds: 90, cancellationToken: cancellationToken);

                CurrentVersion = backupVersion;
                File.WriteAllText(_config.VersionFilePath, backupVersion);
                _log.Success("Rollback complete");
                _compose.StartLogTail();
                return true;
            }
            catch (Exception ex)
            {
                _log.Error($"Rollback failed: {ex.Message}. Manual intervention may be required.");
                return false;
            }
        }

        public bool HasBackupVersion() => File.Exists(_config.BackupVersionFilePath);

        private async Task<bool> _wslComposeUp(CancellationToken cancellationToken)
        {
            var result = await _wsl.RunBashAsync(
                "docker compose up -d",
                timeoutSeconds: 300,
                cancellationToken: cancellationToken);
            return result.Success;
        }

        private async Task<string> GetLatestGhcrTagAsync(string serviceName, CancellationToken cancellationToken)
        {
            try
            {
                string apiUrl = $"https://ghcr.io/v2/{_config.GhcrOrg}/{serviceName}/tags/list";
                using var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                if (!string.IsNullOrWhiteSpace(_config.GhcrToken))
                {
                    string user = string.IsNullOrWhiteSpace(_config.GhcrUsername) ? _config.GhcrOrg : _config.GhcrUsername;
                    string token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{_config.GhcrToken}"));
                    request.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
                }

                using var response = await Http.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    string content = await response.Content.ReadAsStringAsync(cancellationToken);
                    if (content.Contains("\"latest\"", StringComparison.Ordinal))
                    {
                        return "latest";
                    }

                    var tags = ExtractTagsFromJson(content);
                    if (tags.Count > 0)
                    {
                        return tags[0];
                    }
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _log.Warning($"No authentication for GHCR ({serviceName}). Using 'latest'.");
                    return "latest";
                }
            }
            catch (Exception ex)
            {
                _log.Warning($"Failed to fetch tag for {serviceName}: {ex.Message}. Using 'latest'.");
            }

            return "latest";
        }

        private static List<string> ExtractTagsFromJson(string json)
        {
            var tags = new List<string>();
            try
            {
                int tagsIndex = json.IndexOf("\"tags\":[", StringComparison.Ordinal);
                if (tagsIndex < 0)
                {
                    return tags;
                }

                int start = tagsIndex + 8;
                int end = json.IndexOf(']', start);
                if (end < 0)
                {
                    return tags;
                }

                string tagsString = json[start..end];
                foreach (var tag in tagsString.Split(','))
                {
                    string cleanTag = tag.Trim().Trim('"');
                    if (!string.IsNullOrEmpty(cleanTag) && cleanTag != "null")
                    {
                        tags.Add(cleanTag);
                    }
                }
            }
            catch
            {
                // Ignore parse errors.
            }

            return tags;
        }
    }
}
