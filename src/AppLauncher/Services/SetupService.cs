using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AppLauncher.Services
{
    public enum SetupState
    {
        Checking,
        NeedsWslInstall,
        NeedsDistro,
        NeedsDocker,
        NeedsEnvFile,
        NeedsComposeFile,
        Ready,
        Failed
    }

    public sealed class SetupResult
    {
        public SetupState State { get; init; }
        public string Message { get; init; } = "";
        public bool IsReady => State == SetupState.Ready;
    }

    public sealed class SetupService
    {
        private readonly AppConfig _config;
        private readonly WslService _wsl;
        private readonly DockerComposeService _compose;
        private readonly LogService _log;

        public SetupService(
            AppConfig config,
            WslService wsl,
            DockerComposeService compose,
            LogService log)
        {
            _config = config;
            _wsl = wsl;
            _compose = compose;
            _log = log;
        }

        public async Task<SetupResult> AssessAsync(CancellationToken cancellationToken = default)
        {
            _log.Info("Checking environment prerequisites...");

            if (!File.Exists(_config.ComposeFilePath))
            {
                return new SetupResult
                {
                    State = SetupState.NeedsComposeFile,
                    Message = "docker-compose.yml is missing next to the launcher."
                };
            }

            if (!File.Exists(_config.EnvFilePath))
            {
                return new SetupResult
                {
                    State = SetupState.NeedsEnvFile,
                    Message = ".env file is missing. Copy from .env.example and configure secrets."
                };
            }

            bool wslAvailable = await _wsl.IsWslAvailableAsync(cancellationToken);
            if (!wslAvailable)
            {
                return new SetupResult
                {
                    State = SetupState.NeedsWslInstall,
                    Message = "WSL is not available. Install WSL 2 to continue."
                };
            }

            var distros = await _wsl.RunWslExeAsync(
                "-l -q",
                timeoutSeconds: 20,
                logCommand: false,
                streamOutput: false,
                cancellationToken);

            string distroList = (distros.StdOut + distros.StdErr)
                .Replace("\0", "")
                .Trim();

            if (string.IsNullOrWhiteSpace(distroList) || distroList.Contains("has no installed distributions", StringComparison.OrdinalIgnoreCase))
            {
                return new SetupResult
                {
                    State = SetupState.NeedsDistro,
                    Message = "No WSL distribution installed. Ubuntu will be installed."
                };
            }

            if (!await _compose.IsDockerReadyAsync(cancellationToken))
            {
                // Try starting daemon first.
                await _compose.EnsureDockerDaemonAsync(cancellationToken);
                if (!await _compose.IsDockerReadyAsync(cancellationToken))
                {
                    return new SetupResult
                    {
                        State = SetupState.NeedsDocker,
                        Message = "Docker Engine is not ready inside WSL."
                    };
                }
            }

            _log.Success("Environment ready");
            return new SetupResult
            {
                State = SetupState.Ready,
                Message = "WSL and Docker are ready."
            };
        }

        public async Task<SetupResult> RepairAsync(CancellationToken cancellationToken = default)
        {
            var current = await AssessAsync(cancellationToken);
            if (current.IsReady)
            {
                return current;
            }

            switch (current.State)
            {
                case SetupState.NeedsComposeFile:
                    _log.Error("Place docker-compose.yml beside AppLauncher.exe and retry.");
                    return current;

                case SetupState.NeedsEnvFile:
                    EnsureEnvFile();
                    return await AssessAsync(cancellationToken);

                case SetupState.NeedsWslInstall:
                    await InstallWslAsync(cancellationToken);
                    return await AssessAsync(cancellationToken);

                case SetupState.NeedsDistro:
                    await InstallUbuntuAsync(cancellationToken);
                    return await AssessAsync(cancellationToken);

                case SetupState.NeedsDocker:
                    await InstallDockerInWslAsync(cancellationToken);
                    return await AssessAsync(cancellationToken);

                default:
                    return current;
            }
        }

        public void EnsureEnvFile()
        {
            if (File.Exists(_config.EnvFilePath))
            {
                return;
            }

            if (File.Exists(_config.EnvExamplePath))
            {
                File.Copy(_config.EnvExamplePath, _config.EnvFilePath);
                _log.Warning("Created .env from .env.example — update secrets before production use.");
            }
            else
            {
                File.WriteAllText(_config.EnvFilePath, DefaultEnvContents());
                _log.Warning("Created a default .env — update secrets before production use.");
            }
        }

        private async Task InstallWslAsync(CancellationToken cancellationToken)
        {
            _log.Info("Installing WSL (administrator approval required)...");
            string script = @"
$ErrorActionPreference = 'Stop'
wsl --install --no-distribution
wsl --set-default-version 2
Write-Output 'WSL install command completed. A reboot may be required.'
";
            var result = await _wsl.RunPowerShellElevatedAsync(script, timeoutSeconds: 900, cancellationToken);
            if (result.Success)
            {
                _log.Success("WSL install triggered. Reboot Windows if prompted, then open Clinix Launcher again.");
            }
            else
            {
                _log.Error("WSL install failed or was cancelled. Install WSL manually from Microsoft Store / 'wsl --install'.");
            }
        }

        private async Task InstallUbuntuAsync(CancellationToken cancellationToken)
        {
            _log.Info("Installing Ubuntu WSL distribution...");
            var result = await _wsl.RunWslExeAsync(
                "--install -d Ubuntu",
                timeoutSeconds: 900,
                cancellationToken: cancellationToken);

            if (!result.Success)
            {
                _log.Info("Retrying Ubuntu install via elevated PowerShell...");
                string script = "wsl --install -d Ubuntu";
                await _wsl.RunPowerShellElevatedAsync(script, timeoutSeconds: 900, cancellationToken);
            }

            _log.Warning("Complete Ubuntu first-boot user setup in the Ubuntu window if it appears, then retry setup.");
        }

        private async Task InstallDockerInWslAsync(CancellationToken cancellationToken)
        {
            _log.Info("Installing Docker Engine inside WSL (may prompt for your Linux password)...");

            // Official-ish Docker CE install for Debian/Ubuntu-based distros.
            const string installScript = """
set -e
if command -v docker >/dev/null 2>&1; then
  echo 'Docker binary already present'
else
  sudo apt-get update
  sudo apt-get install -y ca-certificates curl gnupg
  sudo install -m 0755 -d /etc/apt/keyrings
  if [ ! -f /etc/apt/keyrings/docker.gpg ]; then
    curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
    sudo chmod a+r /etc/apt/keyrings/docker.gpg
  fi
  . /etc/os-release
  echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu ${VERSION_CODENAME} stable" | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null
  sudo apt-get update
  sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
fi
sudo service docker start || sudo systemctl enable --now docker || true
sudo usermod -aG docker "$USER" || true
docker --version
docker compose version
docker info >/dev/null
""";

            // Write script into WSL temp and execute to avoid escaping hell.
            string wslRoot = _wsl.ToWslPath(_config.AppRoot);
            string scriptPath = Path.Combine(_config.AppRoot, "install-docker-wsl.sh");
            await File.WriteAllTextAsync(scriptPath, installScript.Replace("\r\n", "\n"), cancellationToken);

            var result = await _wsl.RunBashAsync(
                $"bash '{wslRoot}/install-docker-wsl.sh'",
                timeoutSeconds: 900,
                cancellationToken: cancellationToken);

            try
            {
                if (File.Exists(scriptPath))
                {
                    File.Delete(scriptPath);
                }
            }
            catch
            {
                // Ignore cleanup failures.
            }

            if (result.Success)
            {
                _log.Success("Docker Engine installed in WSL. If group membership was updated, restart WSL once (`wsl --shutdown`).");
            }
            else
            {
                _log.Error("Docker installation failed. Ensure your WSL distro can use sudo and has network access.");
            }
        }

        private static string DefaultEnvContents() =>
            """
POSTGRES_DB=clinix_datastore
POSTGRES_USER=clinix_application
POSTGRES_PASSWORD=changeme
SPRING_PROFILES_ACTIVE=prod
APP_NAME=Clinix
APP_ENV=production
REACT_APP_ENV=production
GHCR_ORG=clinix-rk
GITHUB_REPORT_REPO=clinix-rk/launcher
GITHUB_REPORT_TOKEN=
GITHUB_REPORT_LABELS=crash-report
AUTO_UPDATE_ENABLED=true
AUTO_UPDATE_INTERVAL_HOURS=6
""";
    }
}
