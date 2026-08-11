using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace AppLauncher.Services
{
    public sealed class HealthCheckService
    {
        private readonly AppConfig _config;
        private readonly LogService _log;
        private static readonly HttpClient Http = CreateClient();

        public HealthCheckService(AppConfig config, LogService log)
        {
            _config = config;
            _log = log;
        }

        public async Task<bool> VerifyAppHealthAsync(
            int startupDelaySeconds = 5,
            int timeoutSeconds = 45,
            CancellationToken cancellationToken = default)
        {
            if (startupDelaySeconds > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(startupDelaySeconds), cancellationToken);
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            try
            {
                _log.Info("Checking Forge (backend) health...");
                using (var forgeResponse = await Http.GetAsync(_config.ForgeHealthUrl, timeoutCts.Token))
                {
                    if (!forgeResponse.IsSuccessStatusCode)
                    {
                        _log.Error($"Backend health check failed: {(int)forgeResponse.StatusCode}");
                        return false;
                    }
                }

                _log.Info("Checking Lens (frontend) health...");
                using (var lensResponse = await Http.GetAsync(_config.LensHealthUrl, timeoutCts.Token))
                {
                    if (!lensResponse.IsSuccessStatusCode)
                    {
                        _log.Error($"Frontend health check failed: {(int)lensResponse.StatusCode}");
                        return false;
                    }
                }

                _log.Success("All health checks passed");
                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _log.Error($"Health check exception: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> IsForgeHealthyAsync(CancellationToken cancellationToken = default)
        {
            return await IsUrlHealthyAsync(_config.ForgeHealthUrl, cancellationToken);
        }

        public async Task<bool> IsLensHealthyAsync(CancellationToken cancellationToken = default)
        {
            return await IsUrlHealthyAsync(_config.LensHealthUrl, cancellationToken);
        }

        private static async Task<bool> IsUrlHealthyAsync(string url, CancellationToken cancellationToken)
        {
            try
            {
                using var response = await Http.GetAsync(url, cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            client.DefaultRequestHeaders.ConnectionClose = true;
            return client;
        }
    }
}
