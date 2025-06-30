using Microsoft.Extensions.Logging;

namespace Infrastructure.Scaling.Service
{
    public partial class ApplicationLifecycleService
    {
        public static partial class Log
        {
            [LoggerMessage(1, LogLevel.Information, "Application lifecycle monitoring service started",
                EventName = "LifecycleServiceStarted")]
            public static partial void LifecycleServiceStarted(ILogger logger);

            [LoggerMessage(2, LogLevel.Information, "Application lifecycle monitoring service stopped",
                EventName = "LifecycleServiceStopped")]
            public static partial void LifecycleServiceStopped(ILogger logger);

            [LoggerMessage(3, LogLevel.Information, "Application started successfully - StartupTime: {StartupTime}ms, ProcessId: {ProcessId}",
                EventName = "ApplicationStarted")]
            public static partial void ApplicationStarted(ILogger logger, double startupTime, int processId);

            [LoggerMessage(4, LogLevel.Information, 
                "Application stopping - Uptime: {Uptime}s, ProcessId: {ProcessId}",
                EventName = "ApplicationStopping")]
            public static partial void ApplicationStopping(ILogger logger, double uptime, int processId);

            [LoggerMessage(5, LogLevel.Information, "Application stopped - Uptime: {Uptime}s, ProcessId: {ProcessId}",
                EventName = "ApplicationStopped")]
            public static partial void ApplicationStopped(ILogger logger, double uptime, int processId);

            [LoggerMessage(6, LogLevel.Critical, 
                "Process exiting - Uptime: {Uptime}s, ProcessId: {ProcessId}",
                EventName = "ProcessExiting")]
            public static partial void ProcessExiting(ILogger logger, double uptime, int processId);

            [LoggerMessage(7, LogLevel.Critical, 
                "Unhandled exception occurred - Uptime: {Uptime}s, ProcessId: {ProcessId}",
                EventName = "UnhandledException")]
            public static partial void UnhandledException(ILogger logger, double uptime, int processId, Exception exception);

            [LoggerMessage(8, LogLevel.Warning, 
                "Cancel key pressed ({SpecialKey}) - Uptime: {Uptime}s, ProcessId: {ProcessId}",
                EventName = "CancelKeyPressed")]
            public static partial void CancelKeyPressed(ILogger logger, double uptime, int processId, string specialKey);

            [LoggerMessage(9, LogLevel.Error, "Error occurred while cleaning up event handlers",
                EventName = "ErrorCleaningUpEventHandlers")]
            public static partial void ErrorCleaningUpEventHandlers(ILogger logger, Exception exception);
        }
    }
} 