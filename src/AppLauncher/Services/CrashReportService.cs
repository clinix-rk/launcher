using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AppLauncher.Services
{
    public sealed class CrashReportResult
    {
        public bool Success { get; init; }
        public string? IssueUrl { get; init; }
        public string Message { get; init; } = "";
    }

    public sealed class CrashReportService
    {
        private readonly AppConfig _config;
        private readonly LogService _log;
        private readonly WslService _wsl;
        private readonly DockerComposeService _compose;
        private readonly UpdateService _updates;
        private static readonly HttpClient Http = new();
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public CrashReportService(
            AppConfig config,
            LogService log,
            WslService wsl,
            DockerComposeService compose,
            UpdateService updates)
        {
            _config = config;
            _log = log;
            _wsl = wsl;
            _compose = compose;
            _updates = updates;
        }

        public async Task<CrashReportResult> SendAsync(CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_config.GitHubReportToken))
            {
                return new CrashReportResult
                {
                    Success = false,
                    Message = "GITHUB_REPORT_TOKEN is not set in .env"
                };
            }

            if (string.IsNullOrWhiteSpace(_config.GitHubReportRepo) || !_config.GitHubReportRepo.Contains('/'))
            {
                return new CrashReportResult
                {
                    Success = false,
                    Message = "GITHUB_REPORT_REPO must be in owner/repo form"
                };
            }

            _log.Info("Collecting diagnostics for crash report...");

            string composePs = "";
            string composeLogs = "";
            string dockerVersion = "";
            string wslStatus = "";

            try
            {
                composePs = await _compose.GetComposePsAsync(cancellationToken);
                composeLogs = await _compose.GetRecentContainerLogsAsync(200, cancellationToken);

                var docker = await _wsl.RunBashAsync(
                    "docker --version; docker compose version",
                    timeoutSeconds: 20,
                    logCommand: false,
                    streamOutput: false,
                    cancellationToken);
                dockerVersion = docker.StdOut;

                var wslInfo = await _wsl.RunWslExeAsync(
                    "--status",
                    timeoutSeconds: 15,
                    logCommand: false,
                    streamOutput: false,
                    cancellationToken);
                wslStatus = (wslInfo.StdOut + wslInfo.StdErr).Replace("\0", "");
            }
            catch (Exception ex)
            {
                _log.Warning($"Partial diagnostics only: {ex.Message}");
            }

            string launcherLog = _log.ReadLogFileTail(40000);
            string body = BuildIssueBody(composePs, composeLogs, dockerVersion, wslStatus, launcherLog);
            if (body.Length > 60000)
            {
                body = body[..60000] + "\n\n_(truncated to fit GitHub issue body limit)_";
            }

            string title =
                $"[Crash] Clinix launcher {_updates.CurrentVersion} — {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC";

            try
            {
                string[] parts = _config.GitHubReportRepo.Split('/', 2);
                string url = $"https://api.github.com/repos/{parts[0]}/{parts[1]}/issues";
                string[] labels = SplitLabels(_config.GitHubReportLabels);

                var (ok, responseBody, statusCode) = await PostIssueAsync(url, title, body, labels, cancellationToken);
                if (!ok && labels.Length > 0)
                {
                    _log.Warning("Retrying crash report without labels...");
                    (ok, responseBody, statusCode) = await PostIssueAsync(
                        url, title, body, Array.Empty<string>(), cancellationToken);
                }

                if (!ok)
                {
                    _log.Error($"GitHub issue create failed: {statusCode} {responseBody}");
                    return new CrashReportResult
                    {
                        Success = false,
                        Message = $"GitHub API error ({statusCode}). Check token permissions."
                    };
                }

                using var doc = JsonDocument.Parse(responseBody);
                string? htmlUrl = doc.RootElement.TryGetProperty("html_url", out var u)
                    ? u.GetString()
                    : null;

                _log.Success(htmlUrl != null ? $"Crash report filed: {htmlUrl}" : "Crash report filed");
                return new CrashReportResult
                {
                    Success = true,
                    IssueUrl = htmlUrl,
                    Message = "Crash report submitted"
                };
            }
            catch (Exception ex)
            {
                _log.Error($"Crash report failed: {ex.Message}");
                return new CrashReportResult
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }

        private async Task<(bool Ok, string Body, int StatusCode)> PostIssueAsync(
            string url,
            string title,
            string body,
            string[] labels,
            CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.UserAgent.ParseAdd("Clinix-Launcher");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.GitHubReportToken);
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

            var payload = new IssuePayload
            {
                Title = title,
                Body = body,
                Labels = labels
            };

            request.Content = new StringContent(
                JsonSerializer.Serialize(payload, JsonOptions),
                Encoding.UTF8,
                "application/json");

            using var response = await Http.SendAsync(request, cancellationToken);
            string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            return (response.IsSuccessStatusCode, responseBody, (int)response.StatusCode);
        }

        private string BuildIssueBody(
            string composePs,
            string composeLogs,
            string dockerVersion,
            string wslStatus,
            string launcherLog)
        {
            var sb = new StringBuilder();
            sb.AppendLine("## Clinix Launcher crash report");
            sb.AppendLine();
            sb.AppendLine($"- **Launcher version file:** `{_updates.CurrentVersion}`");
            sb.AppendLine($"- **Reported (UTC):** `{DateTime.UtcNow:O}`");
            sb.AppendLine($"- **OS:** `{Environment.OSVersion}`");
            sb.AppendLine($"- **Machine:** `{Environment.MachineName}`");
            sb.AppendLine($"- **User:** `{Environment.UserName}`");
            sb.AppendLine();
            sb.AppendLine("### Docker");
            sb.AppendLine("```");
            sb.AppendLine(Truncate(dockerVersion, 2000));
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("### WSL status");
            sb.AppendLine("```");
            sb.AppendLine(Truncate(wslStatus, 2000));
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("### docker compose ps");
            sb.AppendLine("```");
            sb.AppendLine(Truncate(composePs, 4000));
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("### Recent container logs");
            sb.AppendLine("```");
            sb.AppendLine(Truncate(composeLogs, 12000));
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("### Launcher log (tail)");
            sb.AppendLine("```");
            sb.AppendLine(Truncate(launcherLog, 20000));
            sb.AppendLine("```");
            return sb.ToString();
        }

        private static string Truncate(string value, int max)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "(empty)";
            }

            value = value.Trim();
            return value.Length <= max ? value : value[^max..];
        }

        private static string[] SplitLabels(string labels)
        {
            if (string.IsNullOrWhiteSpace(labels))
            {
                return Array.Empty<string>();
            }

            return labels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        private sealed class IssuePayload
        {
            public string Title { get; set; } = "";
            public string Body { get; set; } = "";
            public string[] Labels { get; set; } = Array.Empty<string>();
        }
    }
}
