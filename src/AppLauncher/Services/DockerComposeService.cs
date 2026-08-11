using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AppLauncher.Services
{
    public enum ServiceHealth
    {
        Unknown,
        Down,
        Starting,
        Healthy
    }

    public sealed class StackStatus
    {
        public ServiceHealth Postgres { get; init; } = ServiceHealth.Unknown;
        public ServiceHealth Forge { get; init; } = ServiceHealth.Unknown;
        public ServiceHealth Lens { get; init; } = ServiceHealth.Unknown;
        public bool AnyRunning =>
            Postgres != ServiceHealth.Down && Postgres != ServiceHealth.Unknown
            || Forge != ServiceHealth.Down && Forge != ServiceHealth.Unknown
            || Lens != ServiceHealth.Down && Lens != ServiceHealth.Unknown;
        public bool AllHealthy =>
            Postgres == ServiceHealth.Healthy
            && Forge == ServiceHealth.Healthy
            && Lens == ServiceHealth.Healthy;
    }

    public sealed class DockerComposeService
    {
        private readonly AppConfig _config;
        private readonly WslService _wsl;
        private readonly HealthCheckService _health;
        private readonly LogService _log;
        private CancellationTokenSource? _logsCts;

        public DockerComposeService(
            AppConfig config,
            WslService wsl,
            HealthCheckService health,
            LogService log)
        {
            _config = config;
            _wsl = wsl;
            _health = health;
            _log = log;
        }

        public async Task<bool> IsDockerReadyAsync(CancellationToken cancellationToken = default)
        {
            var version = await _wsl.RunBashAsync(
                "docker --version && docker compose version",
                timeoutSeconds: 30,
                logCommand: false,
                streamOutput: false,
                cancellationToken);

            if (!version.Success)
            {
                return false;
            }

            var info = await _wsl.RunBashAsync(
                "docker info >/dev/null 2>&1",
                timeoutSeconds: 30,
                logCommand: false,
                streamOutput: false,
                cancellationToken);

            return info.Success;
        }

        public async Task EnsureDockerDaemonAsync(CancellationToken cancellationToken = default)
        {
            if (await IsDockerReadyAsync(cancellationToken))
            {
                return;
            }

            _log.Info("Attempting to start Docker daemon in WSL...");
            await _wsl.RunBashAsync(
                "(sudo service docker start || sudo systemctl start docker || true)",
                timeoutSeconds: 60,
                cancellationToken: cancellationToken);
        }

        public async Task<bool> StartAsync(CancellationToken cancellationToken = default)
        {
            await EnsureDockerDaemonAsync(cancellationToken);
            _log.Info("Starting Clinix services...");

            var result = await _wsl.RunBashAsync(
                "docker compose up -d",
                timeoutSeconds: 300,
                cancellationToken: cancellationToken);

            if (!result.Success)
            {
                _log.Error("Failed to start containers");
                return false;
            }

            bool healthy = await _health.VerifyAppHealthAsync(
                startupDelaySeconds: 5,
                timeoutSeconds: 90,
                cancellationToken: cancellationToken);

            if (healthy)
            {
                _log.Success($"Application running at {_config.AppUrl}");
                StartLogTail();
            }

            return healthy;
        }

        public async Task<bool> StopAsync(CancellationToken cancellationToken = default)
        {
            StopLogTail();
            _log.Info("Stopping Clinix services...");

            var result = await _wsl.RunBashAsync(
                "docker compose down",
                timeoutSeconds: 180,
                cancellationToken: cancellationToken);

            if (result.Success)
            {
                _log.Success("Services stopped");
            }
            else
            {
                _log.Error("Failed to stop services cleanly");
            }

            return result.Success;
        }

        public async Task<bool> PullAsync(CancellationToken cancellationToken = default)
        {
            _log.Info("Pulling latest images via docker compose...");
            var result = await _wsl.RunBashAsync(
                "docker compose pull",
                timeoutSeconds: 600,
                cancellationToken: cancellationToken);
            return result.Success;
        }

        public Task OpenAppAsync()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _config.AppUrl,
                    UseShellExecute = true
                });
                _log.Info($"Opened {_config.AppUrl}");
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to open browser: {ex.Message}");
            }

            return Task.CompletedTask;
        }

        public async Task<StackStatus> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            var result = await _wsl.RunBashAsync(
                "docker compose ps --format json",
                timeoutSeconds: 30,
                logCommand: false,
                streamOutput: false,
                cancellationToken);

            var map = new Dictionary<string, ServiceHealth>(StringComparer.OrdinalIgnoreCase)
            {
                ["postgres-db"] = ServiceHealth.Down,
                ["forge"] = ServiceHealth.Down,
                ["lens"] = ServiceHealth.Down
            };

            if (!result.Success || string.IsNullOrWhiteSpace(result.StdOut))
            {
                // Fallback: probe HTTP endpoints when compose ps fails.
                bool forge = await _health.IsForgeHealthyAsync(cancellationToken);
                bool lens = await _health.IsLensHealthyAsync(cancellationToken);
                return new StackStatus
                {
                    Postgres = ServiceHealth.Unknown,
                    Forge = forge ? ServiceHealth.Healthy : ServiceHealth.Down,
                    Lens = lens ? ServiceHealth.Healthy : ServiceHealth.Down
                };
            }

            foreach (string line in result.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                try
                {
                    using var doc = JsonDocument.Parse(line);
                    string service = GetJsonString(doc.RootElement, "Service")
                        ?? GetJsonString(doc.RootElement, "Name")
                        ?? "";
                    string state = GetJsonString(doc.RootElement, "State") ?? "";
                    string health = GetJsonString(doc.RootElement, "Health") ?? "";

                    string key = NormalizeServiceKey(service);
                    if (string.IsNullOrEmpty(key) || !map.ContainsKey(key))
                    {
                        continue;
                    }

                    map[key] = MapHealth(state, health);
                }
                catch
                {
                    // Skip malformed lines.
                }
            }

            // Prefer HTTP for forge/lens when containers report running.
            if (map["forge"] != ServiceHealth.Down)
            {
                map["forge"] = await _health.IsForgeHealthyAsync(cancellationToken)
                    ? ServiceHealth.Healthy
                    : ServiceHealth.Starting;
            }

            if (map["lens"] != ServiceHealth.Down)
            {
                map["lens"] = await _health.IsLensHealthyAsync(cancellationToken)
                    ? ServiceHealth.Healthy
                    : ServiceHealth.Starting;
            }

            return new StackStatus
            {
                Postgres = map["postgres-db"],
                Forge = map["forge"],
                Lens = map["lens"]
            };
        }

        public async Task<string> GetComposePsAsync(CancellationToken cancellationToken = default)
        {
            var result = await _wsl.RunBashAsync(
                "docker compose ps",
                timeoutSeconds: 30,
                logCommand: false,
                streamOutput: false,
                cancellationToken);
            return result.StdOut + result.StdErr;
        }

        public async Task<string> GetRecentContainerLogsAsync(int lines = 200, CancellationToken cancellationToken = default)
        {
            var result = await _wsl.RunBashAsync(
                $"docker compose logs --tail {lines}",
                timeoutSeconds: 60,
                logCommand: false,
                streamOutput: false,
                cancellationToken);
            return result.StdOut + result.StdErr;
        }

        public async Task BackupDatabaseAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string wslRoot = _wsl.ToWslPath(_config.AppRoot);
                string command =
                    $"docker run --rm -v clinix_postgres_data:/data -v '{wslRoot}':/backup " +
                    $"alpine tar czf /backup/db_backup_{timestamp}.tar.gz /data";

                // Volume name may be prefixed by project directory; try compose project volume first.
                var named = await _wsl.RunBashAsync(
                    $"docker volume ls --format '{{{{.Name}}}}' | grep -E 'postgres_data$' | head -n 1",
                    timeoutSeconds: 30,
                    logCommand: false,
                    streamOutput: false,
                    cancellationToken);

                string volume = named.StdOut.Trim();
                if (string.IsNullOrWhiteSpace(volume))
                {
                    volume = "postgres_data";
                }

                command =
                    $"docker run --rm -v {volume}:/data -v '{wslRoot}':/backup " +
                    $"alpine tar czf /backup/db_backup_{timestamp}.tar.gz /data";

                var result = await _wsl.RunBashAsync(command, timeoutSeconds: 300, cancellationToken: cancellationToken);
                if (result.Success)
                {
                    _log.Success($"Database backed up to db_backup_{timestamp}.tar.gz");
                }
                else
                {
                    _log.Warning("Database backup did not complete successfully");
                }
            }
            catch (Exception ex)
            {
                _log.Warning($"Database backup warning: {ex.Message}");
            }
        }

        public void StartLogTail()
        {
            StopLogTail();
            _logsCts = new CancellationTokenSource();
            var token = _logsCts.Token;
            _ = Task.Run(async () =>
            {
                try
                {
                    await _wsl.RunBashAsync(
                        "docker compose logs --tail 50 -f",
                        timeoutSeconds: 60 * 60 * 24,
                        logCommand: false,
                        streamOutput: true,
                        cancellationToken: token);
                }
                catch (OperationCanceledException)
                {
                    // Expected on stop.
                }
                catch (Exception ex)
                {
                    _log.Warning($"Log tail ended: {ex.Message}");
                }
            }, token);
        }

        public void StopLogTail()
        {
            try
            {
                _logsCts?.Cancel();
                _logsCts?.Dispose();
            }
            catch
            {
                // Ignore.
            }
            finally
            {
                _logsCts = null;
            }
        }

        private static string? GetJsonString(JsonElement element, string property)
        {
            if (element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }

            return null;
        }

        private static string NormalizeServiceKey(string service)
        {
            string s = service.Trim().ToLowerInvariant();
            if (s.Contains("postgres") || s.Contains("clinix-db")) return "postgres-db";
            if (s.Contains("forge") || s.Contains("clinix-forge")) return "forge";
            if (s.Contains("lens") || s.Contains("clinix-lens")) return "lens";
            return s;
        }

        private static ServiceHealth MapHealth(string state, string health)
        {
            string st = state.ToLowerInvariant();
            string h = health.ToLowerInvariant();

            if (st.Contains("exit") || st.Contains("dead") || st == "stopped")
            {
                return ServiceHealth.Down;
            }

            if (h == "healthy")
            {
                return ServiceHealth.Healthy;
            }

            if (h == "unhealthy")
            {
                return ServiceHealth.Down;
            }

            if (st.Contains("running") || st.Contains("up") || h == "starting")
            {
                return string.IsNullOrEmpty(h) || h == "starting" ? ServiceHealth.Starting : ServiceHealth.Healthy;
            }

            return ServiceHealth.Down;
        }
    }
}
