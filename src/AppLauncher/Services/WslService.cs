using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AppLauncher.Services
{
    public sealed class CommandResult
    {
        public int ExitCode { get; init; }
        public string StdOut { get; init; } = "";
        public string StdErr { get; init; } = "";
        public bool TimedOut { get; init; }
        public bool Success => ExitCode == 0 && !TimedOut;
    }

    public sealed class WslService
    {
        private readonly string _appRoot;
        private readonly LogService _log;

        public WslService(string appRoot, LogService log)
        {
            _appRoot = appRoot;
            _log = log;
        }

        public string ToWslPath(string windowsPath)
        {
            string full = Path.GetFullPath(windowsPath);
            if (full.Length >= 2 && full[1] == ':')
            {
                char drive = char.ToLowerInvariant(full[0]);
                string rest = full[2..].Replace('\\', '/');
                return $"/mnt/{drive}{rest}";
            }

            return windowsPath.Replace('\\', '/');
        }

        public async Task<CommandResult> RunBashAsync(
            string command,
            int timeoutSeconds = 120,
            bool logCommand = true,
            bool streamOutput = true,
            CancellationToken cancellationToken = default)
        {
            string wslRoot = ToWslPath(_appRoot);
            string wrapped = $"cd '{EscapeSingleQuotes(wslRoot)}' && {command}";
            return await RunProcessAsync(
                "wsl.exe",
                $"-e bash -lc \"{EscapeForProcessArgs(wrapped)}\"",
                timeoutSeconds,
                logCommand ? $"wsl bash: {command}" : null,
                streamOutput,
                cancellationToken);
        }

        public async Task<CommandResult> RunWslExeAsync(
            string arguments,
            int timeoutSeconds = 120,
            bool logCommand = true,
            bool streamOutput = true,
            CancellationToken cancellationToken = default)
        {
            return await RunProcessAsync(
                "wsl.exe",
                arguments,
                timeoutSeconds,
                logCommand ? $"wsl {arguments}" : null,
                streamOutput,
                cancellationToken);
        }

        public async Task<CommandResult> RunPowerShellElevatedAsync(
            string script,
            int timeoutSeconds = 600,
            CancellationToken cancellationToken = default)
        {
            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            return await RunProcessAsync(
                "powershell.exe",
                $"-NoProfile -ExecutionPolicy Bypass -Command \"Start-Process powershell -Verb RunAs -Wait -ArgumentList '-NoProfile -ExecutionPolicy Bypass -EncodedCommand {encoded}'\"",
                timeoutSeconds,
                "Elevated PowerShell (WSL/feature install)",
                streamOutput: true,
                cancellationToken);
        }

        public async Task<CommandResult> RunPowerShellAsync(
            string script,
            int timeoutSeconds = 120,
            CancellationToken cancellationToken = default)
        {
            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            return await RunProcessAsync(
                "powershell.exe",
                $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {encoded}",
                timeoutSeconds,
                "PowerShell",
                streamOutput: true,
                cancellationToken);
        }

        public async Task<bool> IsWslAvailableAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await RunProcessAsync(
                    "wsl.exe",
                    "--status",
                    timeoutSeconds: 15,
                    logLabel: null,
                    streamOutput: false,
                    cancellationToken);
                return result.ExitCode == 0 || !string.IsNullOrWhiteSpace(result.StdOut + result.StdErr);
            }
            catch
            {
                return false;
            }
        }

        private async Task<CommandResult> RunProcessAsync(
            string fileName,
            string arguments,
            int timeoutSeconds,
            string? logLabel,
            bool streamOutput,
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(logLabel))
            {
                _log.Command(logLabel);
            }

            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = _appRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();

            using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                stdout.AppendLine(e.Data);
                if (streamOutput)
                {
                    _log.Info(e.Data);
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                stderr.AppendLine(e.Data);
                if (streamOutput)
                {
                    _log.Warning(e.Data);
                }
            };

            process.Exited += (_, _) => tcs.TrySetResult(process.ExitCode);

            try
            {
                if (!process.Start())
                {
                    _log.Error($"Failed to start process: {fileName}");
                    return new CommandResult { ExitCode = -1, StdErr = "Failed to start process" };
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

                try
                {
                    await using (timeoutCts.Token.Register(() => tcs.TrySetCanceled(timeoutCts.Token)))
                    {
                        int exitCode = await tcs.Task.ConfigureAwait(false);
                        return new CommandResult
                        {
                            ExitCode = exitCode,
                            StdOut = stdout.ToString(),
                            StdErr = stderr.ToString()
                        };
                    }
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    TryKill(process);
                    _log.Error($"Command timed out after {timeoutSeconds}s");
                    return new CommandResult
                    {
                        ExitCode = -1,
                        StdOut = stdout.ToString(),
                        StdErr = stderr.ToString(),
                        TimedOut = true
                    };
                }
                catch (OperationCanceledException)
                {
                    TryKill(process);
                    throw;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.Error($"Process error: {ex.Message}");
                return new CommandResult { ExitCode = -1, StdErr = ex.Message };
            }
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Ignore kill failures.
            }
        }

        private static string EscapeSingleQuotes(string value) => value.Replace("'", "'\\''");

        private static string EscapeForProcessArgs(string value) =>
            value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
