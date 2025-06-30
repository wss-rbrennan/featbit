namespace Infrastructure.Scaling.Metrics;

public interface IApplicationLifecycleMetrics
{
    void ApplicationStarted();
    void ApplicationStopping();
    void ApplicationStopped(double uptimeSeconds);
    void UnexpectedShutdown(string reason);
    void ProcessExiting(double uptimeSeconds);
    void UnhandledException();
    void CancelKeyPressed(string specialKey);
} 