using Microsoft.Extensions.Logging;

namespace Infrastructure.Scaling.Service
{
    public partial class ApplicationShutdownService
    {
        public static partial class Log
        {
            [LoggerMessage(1, LogLevel.Information, "Application shutdown monitoring service started",
                EventName = "ShutdownServiceStarted")]
            public static partial void ShutdownServiceStarted(ILogger logger);

            [LoggerMessage(2, LogLevel.Information, "Application shutdown monitoring service stopped",
                EventName = "ShutdownServiceStopped")]
            public static partial void ShutdownServiceStopped(ILogger logger);

            [LoggerMessage(3, LogLevel.Information, "Application started successfully - StartupTime: {StartupTime}ms, ProcessId: {ProcessId}",
                EventName = "ApplicationStarted")]
            public static partial void ApplicationStarted(ILogger logger, double startupTime, int processId);

            [LoggerMessage(4, LogLevel.Warning, 
                "Application stopping - Uptime: {Uptime}s, WorkingSet: {WorkingSet} bytes, TotalMemory: {TotalMemory} bytes, CPU: {CpuUsage}%, ProcessId: {ProcessId}",
                EventName = "ApplicationStopping")]
            public static partial void ApplicationStopping(ILogger logger, double uptime, long workingSet, long totalMemory, double cpuUsage, int processId);

            [LoggerMessage(5, LogLevel.Information, "Application stopped - Uptime: {Uptime}s, ProcessId: {ProcessId}",
                EventName = "ApplicationStopped")]
            public static partial void ApplicationStopped(ILogger logger, double uptime, int processId);

            [LoggerMessage(6, LogLevel.Critical, 
                "Process exiting - Uptime: {Uptime}s, WorkingSet: {WorkingSet} bytes, TotalMemory: {TotalMemory} bytes, CPU: {CpuUsage}%, ProcessId: {ProcessId}",
                EventName = "ProcessExiting")]
            public static partial void ProcessExiting(ILogger logger, double uptime, long workingSet, long totalMemory, double cpuUsage, int processId);

            [LoggerMessage(7, LogLevel.Critical, 
                "Unhandled exception occurred - Uptime: {Uptime}s, WorkingSet: {WorkingSet} bytes, TotalMemory: {TotalMemory} bytes, CPU: {CpuUsage}%, ProcessId: {ProcessId}",
                EventName = "UnhandledException")]
            public static partial void UnhandledException(ILogger logger, double uptime, long workingSet, long totalMemory, double cpuUsage, int processId, Exception exception);

            [LoggerMessage(8, LogLevel.Warning, 
                "Cancel key pressed ({SpecialKey}) - Uptime: {Uptime}s, WorkingSet: {WorkingSet} bytes, TotalMemory: {TotalMemory} bytes, CPU: {CpuUsage}%, ProcessId: {ProcessId}",
                EventName = "CancelKeyPressed")]
            public static partial void CancelKeyPressed(ILogger logger, double uptime, long workingSet, long totalMemory, double cpuUsage, int processId, string specialKey);

            [LoggerMessage(9, LogLevel.Warning, 
                "High memory usage detected - WorkingSet: {WorkingSet} bytes, TotalMemory: {TotalMemory} bytes, ProcessId: {ProcessId}",
                EventName = "HighMemoryUsage")]
            public static partial void HighMemoryUsage(ILogger logger, long workingSet, long totalMemory, int processId);

            [LoggerMessage(10, LogLevel.Warning, "High CPU usage detected - CPU: {CpuUsage}%, ProcessId: {ProcessId}",
                EventName = "HighCpuUsage")]
            public static partial void HighCpuUsage(ILogger logger, double cpuUsage, int processId);

            [LoggerMessage(11, LogLevel.Warning, 
                "High thread pool utilization - WorkerThreads: {WorkerThreadUtilization}%, IOThreads: {IoThreadUtilization}%, Available: {AvailableWorkerThreads}/{AvailableIoThreads}, ProcessId: {ProcessId}",
                EventName = "HighThreadPoolUtilization")]
            public static partial void HighThreadPoolUtilization(ILogger logger, double workerThreadUtilization, double ioThreadUtilization, 
                int availableWorkerThreads, int availableIoThreads, int processId);

            [LoggerMessage(12, LogLevel.Error, "Error occurred while monitoring system conditions",
                EventName = "ErrorMonitoringSystem")]
            public static partial void ErrorMonitoringSystem(ILogger logger, Exception exception);

            [LoggerMessage(13, LogLevel.Warning, "Error occurred while getting system information",
                EventName = "ErrorGettingSystemInfo")]
            public static partial void ErrorGettingSystemInfo(ILogger logger, Exception exception);
        }
    }
} 