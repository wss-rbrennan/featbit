using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Infrastructure.Scaling.Metrics;

namespace Infrastructure.Scaling.Service
{
    /// <summary>
    /// Service responsible for capturing application lifecycle events and critical shutdown scenarios
    /// </summary>
    public partial class ApplicationShutdownService : IHostedService, IDisposable
    {
        private readonly IHostApplicationLifetime _applicationLifetime;
        private readonly ILogger<ApplicationShutdownService> _logger;
        private readonly IApplicationLifecycleMetrics _lifecycleMetrics;
        private readonly DateTime _startTime;
        private readonly int _processId;

        public ApplicationShutdownService(
            IHostApplicationLifetime applicationLifetime,
            ILogger<ApplicationShutdownService> logger,
            IApplicationLifecycleMetrics lifecycleMetrics)
        {
            _applicationLifetime = applicationLifetime;
            _logger = logger;
            _lifecycleMetrics = lifecycleMetrics;
            _startTime = DateTime.UtcNow;
            _processId = Environment.ProcessId;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Register for application lifecycle events
            _applicationLifetime.ApplicationStarted.Register(OnApplicationStarted);
            _applicationLifetime.ApplicationStopping.Register(OnApplicationStopping);
            _applicationLifetime.ApplicationStopped.Register(OnApplicationStopped);

            // Register for critical process events
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            Console.CancelKeyPress += OnCancelKeyPress;

            Log.ShutdownServiceStarted(_logger);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Log.ShutdownServiceStopped(_logger);
            return Task.CompletedTask;
        }

        private void OnApplicationStarted()
        {
            var uptime = DateTime.UtcNow - _startTime;
            _lifecycleMetrics.ApplicationStarted();
            Log.ApplicationStarted(_logger, uptime.TotalMilliseconds, _processId);
        }

        private void OnApplicationStopping()
        {
            var uptime = DateTime.UtcNow - _startTime;
            _lifecycleMetrics.ApplicationStopping();
            Log.ApplicationStopping(_logger, uptime.TotalSeconds, _processId);
        }

        private void OnApplicationStopped()
        {
            var uptime = DateTime.UtcNow - _startTime;
            _lifecycleMetrics.ApplicationStopped(uptime.TotalSeconds);
            Log.ApplicationStopped(_logger, uptime.TotalSeconds, _processId);
        }

        private void OnProcessExit(object? sender, EventArgs e)
        {
            var uptime = DateTime.UtcNow - _startTime;
            _lifecycleMetrics.ProcessExiting(uptime.TotalSeconds);
            Log.ProcessExiting(_logger, uptime.TotalSeconds, _processId);
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var uptime = DateTime.UtcNow - _startTime;
            var exception = e.ExceptionObject as Exception ?? new Exception("Unknown exception");
            
            _lifecycleMetrics.UnhandledException();
            Log.UnhandledException(_logger, uptime.TotalSeconds, _processId, exception);
        }

        private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            var uptime = DateTime.UtcNow - _startTime;
            var specialKey = e.SpecialKey.ToString();
            
            _lifecycleMetrics.CancelKeyPressed(specialKey);
            Log.CancelKeyPressed(_logger, uptime.TotalSeconds, _processId, specialKey);
        }

        public void Dispose()
        {
            // Clean up event handlers
            try
            {
                AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
                AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
                Console.CancelKeyPress -= OnCancelKeyPress;
            }
            catch (Exception ex)
            {
                Log.ErrorCleaningUpEventHandlers(_logger, ex);
            }
        }
    }
} 