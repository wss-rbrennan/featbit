using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Infrastructure.Scaling.Service
{
    /// <summary>
    /// Service responsible for capturing and logging application shutdown events,
    /// including abnormal shutdowns under heavy load
    /// </summary>
    public partial class ApplicationShutdownService : IHostedService, IDisposable
    {
        private readonly IHostApplicationLifetime _applicationLifetime;
        private readonly ILogger<ApplicationShutdownService> _logger;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly Process _currentProcess;
        private readonly DateTime _startTime;
        private readonly int _processId;

        public ApplicationShutdownService(
            IHostApplicationLifetime applicationLifetime,
            ILogger<ApplicationShutdownService> logger)
        {
            _applicationLifetime = applicationLifetime;
            _logger = logger;
            _currentProcess = Process.GetCurrentProcess();
            _processId = _currentProcess.Id; // Store process ID at startup when it's safe
            _startTime = DateTime.UtcNow;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Register for application lifecycle events
            _applicationLifetime.ApplicationStarted.Register(OnApplicationStarted);
            _applicationLifetime.ApplicationStopping.Register(OnApplicationStopping);
            _applicationLifetime.ApplicationStopped.Register(OnApplicationStopped);

            // Register for process exit events
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            // Register for console cancellation (Ctrl+C, etc.)
            Console.CancelKeyPress += OnCancelKeyPress;

            // Start monitoring task for abnormal conditions
            _ = Task.Run(MonitorSystemConditions, _cancellationTokenSource.Token);

            Log.ShutdownServiceStarted(_logger);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource.Cancel();
            Log.ShutdownServiceStopped(_logger);
            return Task.CompletedTask;
        }

        private void OnApplicationStarted()
        {
            var uptime = DateTime.UtcNow - _startTime;
            Log.ApplicationStarted(_logger, uptime.TotalMilliseconds, _processId);
        }

        private void OnApplicationStopping()
        {
            var uptime = DateTime.UtcNow - _startTime;
            var systemInfo = GetSystemInfo();
            
            Log.ApplicationStopping(_logger, uptime.TotalSeconds, systemInfo.workingSet, 
                systemInfo.totalMemory, systemInfo.cpuUsage, systemInfo.processId);
        }

        private void OnApplicationStopped()
        {
            var uptime = DateTime.UtcNow - _startTime;
            var systemInfo = GetSystemInfo();
            Log.ApplicationStopped(_logger, uptime.TotalSeconds, systemInfo.processId);
        }

        private void OnProcessExit(object? sender, EventArgs e)
        {
            var uptime = DateTime.UtcNow - _startTime;
            var totalMemory = GC.GetTotalMemory(false);
            
            // Don't try to get process-specific metrics during ProcessExit as the process is being disposed
            Log.ProcessExiting(_logger, uptime.TotalSeconds, 0, totalMemory, 0, _processId);
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var uptime = DateTime.UtcNow - _startTime;
            var systemInfo = GetSystemInfo();
            
            Log.UnhandledException(_logger, uptime.TotalSeconds, systemInfo.workingSet,
                systemInfo.totalMemory, systemInfo.cpuUsage, systemInfo.processId, 
                e.ExceptionObject as Exception ?? new Exception("Unknown exception"));
        }

        private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            var uptime = DateTime.UtcNow - _startTime;
            var systemInfo = GetSystemInfo();
            
            Log.CancelKeyPressed(_logger, uptime.TotalSeconds, systemInfo.workingSet,
                systemInfo.totalMemory, systemInfo.cpuUsage, systemInfo.processId, e.SpecialKey.ToString());
        }

        private async Task MonitorSystemConditions()
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    var systemInfo = GetSystemInfo();
                    
                    // Check for concerning conditions that might lead to shutdown
                    if (systemInfo.workingSet > 2_000_000_000) // 2GB
                    {
                        Log.HighMemoryUsage(_logger, systemInfo.workingSet, systemInfo.totalMemory, systemInfo.processId);
                    }
                    
                    if (systemInfo.cpuUsage > 90.0) // 90%
                    {
                        Log.HighCpuUsage(_logger, systemInfo.cpuUsage, systemInfo.processId);
                    }

                    // Check for thread pool exhaustion
                    ThreadPool.GetAvailableThreads(out var workerThreads, out var completionPortThreads);
                    ThreadPool.GetMaxThreads(out var maxWorkerThreads, out var maxCompletionPortThreads);
                    
                    var workerThreadUtilization = (double)(maxWorkerThreads - workerThreads) / maxWorkerThreads * 100;
                    var ioThreadUtilization = (double)(maxCompletionPortThreads - completionPortThreads) / maxCompletionPortThreads * 100;
                    
                    if (workerThreadUtilization > 80.0 || ioThreadUtilization > 80.0)
                    {
                        Log.HighThreadPoolUtilization(_logger, workerThreadUtilization, ioThreadUtilization, 
                            workerThreads, completionPortThreads, systemInfo.processId);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(10), _cancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation token is triggered
                    break;
                }
                catch (Exception ex)
                {
                    Log.ErrorMonitoringSystem(_logger, ex);
                    await Task.Delay(TimeSpan.FromSeconds(30), _cancellationTokenSource.Token);
                }
            }
        }

        private (long workingSet, long totalMemory, double cpuUsage, int processId) GetSystemInfo()
        {
            var totalMemory = GC.GetTotalMemory(false);
            
            try
            {
                // Try to get process information, but don't rely on it during shutdown
                _currentProcess.Refresh();
                
                var workingSet = _currentProcess.WorkingSet64;
                
                // Simple CPU usage calculation - this is approximate
                var cpuUsage = _currentProcess.TotalProcessorTime.TotalMilliseconds / Environment.TickCount * 100.0;
                
                return (workingSet, totalMemory, Math.Min(cpuUsage, 100.0), _processId);
            }
            catch (Exception ex)
            {
                // During shutdown, process properties may not be accessible
                // Return safe values but still include memory and process ID
                Log.ErrorGettingSystemInfo(_logger, ex);
                return (0, totalMemory, 0, _processId);
            }
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Dispose();
            _currentProcess?.Dispose();
        }
    }
} 