using System;
using System.IO;
using DotNetEnv;

namespace AppLauncher
{
    public sealed class AppConfig
    {
        public string AppRoot { get; }
        public string EnvFilePath { get; }
        public string VersionFilePath { get; }
        public string BackupVersionFilePath { get; }
        public string LogFilePath { get; }
        public string ComposeFilePath { get; }
        public string EnvExamplePath { get; }

        public string GhcrOrg { get; private set; } = "clinix-rk";
        public string GhcrUsername { get; private set; } = "";
        public string GhcrToken { get; private set; } = "";

        public string GitHubReportRepo { get; private set; } = "clinix-rk/launcher";
        public string GitHubReportToken { get; private set; } = "";
        public string GitHubReportLabels { get; private set; } = "crash-report";

        public bool AutoUpdateEnabled { get; set; } = true;
        public int AutoUpdateIntervalHours { get; private set; } = 6;

        public string AppUrl { get; } = "http://localhost";
        public string ForgeHealthUrl { get; } = "http://localhost:8080/api/v1/actuator/health";
        public string LensHealthUrl { get; } = "http://localhost/";

        public AppConfig(string? appRoot = null)
        {
            AppRoot = appRoot ?? AppDomain.CurrentDomain.BaseDirectory;
            EnvFilePath = Path.Combine(AppRoot, ".env");
            VersionFilePath = Path.Combine(AppRoot, "current_version.txt");
            BackupVersionFilePath = Path.Combine(AppRoot, "backup_version.txt");
            LogFilePath = Path.Combine(AppRoot, "launcher.log");
            ComposeFilePath = Path.Combine(AppRoot, "docker-compose.yml");
            EnvExamplePath = Path.Combine(AppRoot, ".env.example");
        }

        public void Load()
        {
            if (File.Exists(EnvFilePath))
            {
                Env.Load(EnvFilePath);
            }

            GhcrOrg = GetEnv("GHCR_ORG", "clinix-rk");
            GhcrUsername = GetEnv("GHCR_USERNAME", "");
            GhcrToken = GetEnv("GHCR_TOKEN", "");

            GitHubReportRepo = GetEnv("GITHUB_REPORT_REPO", "clinix-rk/launcher");
            GitHubReportToken = GetEnv("GITHUB_REPORT_TOKEN", "");
            GitHubReportLabels = GetEnv("GITHUB_REPORT_LABELS", "crash-report");

            AutoUpdateEnabled = ParseBool(GetEnv("AUTO_UPDATE_ENABLED", "true"), true);
            AutoUpdateIntervalHours = ParseInt(GetEnv("AUTO_UPDATE_INTERVAL_HOURS", "6"), 6);
            if (AutoUpdateIntervalHours < 1)
            {
                AutoUpdateIntervalHours = 6;
            }
        }

        private static string GetEnv(string key, string fallback)
        {
            string? value = Environment.GetEnvironmentVariable(key);
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static bool ParseBool(string value, bool fallback)
        {
            if (bool.TryParse(value, out bool result))
            {
                return result;
            }

            return value.Equals("1", StringComparison.OrdinalIgnoreCase)
                || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
                || fallback && string.IsNullOrWhiteSpace(value);
        }

        private static int ParseInt(string value, int fallback)
        {
            return int.TryParse(value, out int result) ? result : fallback;
        }
    }
}
