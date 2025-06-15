using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime;

namespace Infrastructure.Scaling.Service;

public class PerformanceMonitoringService : BackgroundService
{
    private readonly ILogger<PerformanceMonitoringService> _logger;
    private readonly Process _currentProcess;
    private long _lastGcCollectionCount;
    private long _lastBytesAllocated;
    private DateTime _lastCpuTime;
    private TimeSpan _lastTotalProcessorTime;

    public PerformanceMonitoringService(ILogger<PerformanceMonitoringService> logger)
    {
        _logger = logger;
        _currentProcess = Process.GetCurrentProcess();
        
        _lastGcCollectionCount = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);
        _lastBytesAllocated = GC.GetTotalMemory(false);
        _lastCpuTime = DateTime.UtcNow;
        _lastTotalProcessorTime = _currentProcess.TotalProcessorTime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Performance monitoring service started");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await MonitorPerformance();
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in performance monitoring");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }

    private Task MonitorPerformance()
    {
        try
        {
            // Memory metrics
            var workingSet = _currentProcess.WorkingSet64;
            var totalMemory = GC.GetTotalMemory(false);
            var gen0Collections = GC.CollectionCount(0);
            var gen1Collections = GC.CollectionCount(1);
            var gen2Collections = GC.CollectionCount(2);
            var totalCollections = gen0Collections + gen1Collections + gen2Collections;
            
            // Thread pool metrics
            ThreadPool.GetAvailableThreads(out var availableWorkerThreads, out var availableCompletionPortThreads);
            ThreadPool.GetMaxThreads(out var maxWorkerThreads, out var maxCompletionPortThreads);
            ThreadPool.GetMinThreads(out var minWorkerThreads, out var minCompletionPortThreads);
            
            var workerThreadUtilization = (double)(maxWorkerThreads - availableWorkerThreads) / maxWorkerThreads * 100;
            var ioThreadUtilization = (double)(maxCompletionPortThreads - availableCompletionPortThreads) / maxCompletionPortThreads * 100;
            
            // CPU usage calculation
            var currentTime = DateTime.UtcNow;
            var currentTotalProcessorTime = _currentProcess.TotalProcessorTime;
            var cpuUsedMs = (currentTotalProcessorTime - _lastTotalProcessorTime).TotalMilliseconds;
            var totalMsPassed = (currentTime - _lastCpuTime).TotalMilliseconds;
            var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed) * 100;
            
            _lastCpuTime = currentTime;
            _lastTotalProcessorTime = currentTotalProcessorTime;
            
            // GC pressure
            var gcCollectionsSinceLastCheck = totalCollections - _lastGcCollectionCount;
            var bytesAllocatedSinceLastCheck = totalMemory - _lastBytesAllocated;
            
            _lastGcCollectionCount = totalCollections;
            _lastBytesAllocated = totalMemory;

            // Log performance metrics
            _logger.LogInformation(
                "Performance Metrics - " +
                "WorkingSet: {WorkingSetMB}MB, " +
                "TotalMemory: {TotalMemoryMB}MB, " +
                "CPU: {CpuUsage:F1}%, " +
                "WorkerThreads: {WorkerThreadUtilization:F1}% ({AvailableWorkerThreads}/{MaxWorkerThreads}), " +
                "IOThreads: {IoThreadUtilization:F1}% ({AvailableCompletionPortThreads}/{MaxCompletionPortThreads}), " +
                "GC Collections: {GcCollections} (Gen0: {Gen0}, Gen1: {Gen1}, Gen2: {Gen2}), " +
                "Bytes Allocated: {BytesAllocatedMB:F1}MB",
                workingSet / 1024 / 1024,
                totalMemory / 1024 / 1024,
                cpuUsageTotal,
                workerThreadUtilization,
                availableWorkerThreads,
                maxWorkerThreads,
                ioThreadUtilization,
                availableCompletionPortThreads,
                maxCompletionPortThreads,
                gcCollectionsSinceLastCheck,
                gen0Collections,
                gen1Collections,
                gen2Collections,
                bytesAllocatedSinceLastCheck / 1024.0 / 1024.0
            );

            // Alert on high resource usage
            if (workingSet > 3L * 1024 * 1024 * 1024) // 3GB
            {
                _logger.LogWarning("High memory usage detected: {WorkingSetGB:F1}GB", workingSet / 1024.0 / 1024.0 / 1024.0);
            }

            if (workerThreadUtilization > 90 || ioThreadUtilization > 90)
            {
                _logger.LogWarning("High thread pool utilization - Worker: {WorkerThreadUtilization:F1}%, IO: {IoThreadUtilization:F1}%", 
                    workerThreadUtilization, ioThreadUtilization);
            }

            if (gcCollectionsSinceLastCheck > 10)
            {
                _logger.LogWarning("High GC pressure detected: {GcCollections} collections in last 30 seconds", gcCollectionsSinceLastCheck);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting performance metrics");
        }
        
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _currentProcess?.Dispose();
        base.Dispose();
    }
} 