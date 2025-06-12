using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.WebSockets;

namespace Streaming.Scaling.Service
{
    /// <summary>
    /// Service responsible for coordinating graceful shutdown of WebSocket connections
    /// </summary>
    public class WebSocketShutdownService : IHostedService
    {
        private readonly ISubscriptionService _subscriptionService;
        private readonly IHostApplicationLifetime _applicationLifetime;
        private readonly ILogger<WebSocketShutdownService> _logger;

        public WebSocketShutdownService(
            ISubscriptionService subscriptionService,
            IHostApplicationLifetime applicationLifetime,
            ILogger<WebSocketShutdownService> logger)
        {
            _subscriptionService = subscriptionService;
            _applicationLifetime = applicationLifetime;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Register for application shutdown events
            _applicationLifetime.ApplicationStopping.Register(OnApplicationStopping);
            _logger.LogInformation("WebSocket shutdown service started and registered for application shutdown events");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("WebSocket shutdown service stopped");
            return Task.CompletedTask;
        }

        private void OnApplicationStopping()
        {
            _logger.LogInformation("Application stopping signal received - initiating WebSocket shutdown");
            
            // Start the shutdown process in a background task to avoid blocking the shutdown
            _ = Task.Run(async () =>
            {
                try
                {
                    await _subscriptionService.DisconnectAllAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Server is shutting down"
                    );
                    _logger.LogInformation("WebSocket shutdown completed successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during WebSocket shutdown");
                }
            });
        }

        /// <summary>
        /// Manually trigger WebSocket disconnection (useful for testing or manual shutdown)
        /// </summary>
        public async Task DisconnectAllWebSocketsAsync(
            WebSocketCloseStatus closeStatus = WebSocketCloseStatus.NormalClosure,
            string statusDescription = "Manual disconnect")
        {
            _logger.LogInformation("Manual WebSocket disconnection requested");
            await _subscriptionService.DisconnectAllAsync(closeStatus, statusDescription);
        }
    }
} 