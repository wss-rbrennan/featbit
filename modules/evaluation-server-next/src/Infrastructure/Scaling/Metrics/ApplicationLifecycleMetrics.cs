using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Scaling.Metrics;

public class ApplicationLifecycleMetrics : IApplicationLifecycleMetrics
{
    private readonly Meter _meter;
    private readonly ILogger<ApplicationLifecycleMetrics> _logger;

    // Lifecycle counters
    private readonly Counter<long> _applicationStarts;
    private readonly Counter<long> _applicationStops;
    private readonly Counter<long> _unexpectedShutdowns;
    private readonly Counter<long> _processExits;
    private readonly Counter<long> _unhandledExceptions;
    private readonly Counter<long> _cancelKeyPresses;

    // Uptime tracking
    private readonly Histogram<double> _applicationUptime;

    public ApplicationLifecycleMetrics(ILogger<ApplicationLifecycleMetrics> logger, IMeterFactory meterFactory)
    {
        _logger = logger;
        _meter = meterFactory.Create("FeatBit.Application.Lifecycle");

        // Initialize counters
        _applicationStarts = _meter.CreateCounter<long>(
            "application.starts.total",
            description: "Total number of application starts"
        );

        _applicationStops = _meter.CreateCounter<long>(
            "application.stops.total",
            description: "Total number of application stops"
        );

        _unexpectedShutdowns = _meter.CreateCounter<long>(
            "application.shutdowns.unexpected",
            description: "Total number of unexpected application shutdowns"
        );

        _processExits = _meter.CreateCounter<long>(
            "application.process.exits",
            description: "Total number of process exits"
        );

        _unhandledExceptions = _meter.CreateCounter<long>(
            "application.exceptions.unhandled",
            description: "Total number of unhandled exceptions"
        );

        _cancelKeyPresses = _meter.CreateCounter<long>(
            "application.cancel.keypresses",
            description: "Total number of cancel key presses (Ctrl+C, etc.)"
        );

        _applicationUptime = _meter.CreateHistogram<double>(
            "application.uptime.seconds",
            unit: "s",
            description: "Application uptime when stopping"
        );

        _logger.LogInformation("ApplicationLifecycleMetrics initialized with meter name: {MeterName}", _meter.Name);
    }

    public void ApplicationStarted()
    {
        _applicationStarts.Add(1);
        _logger.LogDebug("Recorded application start");
    }

    public void ApplicationStopping()
    {
        _logger.LogDebug("Application stopping event recorded");
    }

    public void ApplicationStopped(double uptimeSeconds)
    {
        _applicationStops.Add(1);
        _applicationUptime.Record(uptimeSeconds);
        _logger.LogDebug("Recorded application stop with uptime: {UptimeSeconds}s", uptimeSeconds);
    }

    public void UnexpectedShutdown(string reason)
    {
        _unexpectedShutdowns.Add(1, new KeyValuePair<string, object?>("reason", reason));
        _logger.LogDebug("Recorded unexpected shutdown: {Reason}", reason);
    }

    public void ProcessExiting(double uptimeSeconds)
    {
        _processExits.Add(1);
        _applicationUptime.Record(uptimeSeconds);
        _logger.LogDebug("Recorded process exit with uptime: {UptimeSeconds}s", uptimeSeconds);
    }

    public void UnhandledException()
    {
        _unhandledExceptions.Add(1);
        _logger.LogDebug("Recorded unhandled exception");
    }

    public void CancelKeyPressed(string specialKey)
    {
        _cancelKeyPresses.Add(1, new KeyValuePair<string, object?>("key", specialKey));
        _logger.LogDebug("Recorded cancel key press: {SpecialKey}", specialKey);
    }
} 