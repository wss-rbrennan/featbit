using Infrastructure;
using Infrastructure.Channels;
using Infrastructure.Providers.Redis;
using Serilog;
using Streaming.DependencyInjection;
using Streaming.Metrics;
using System.Diagnostics.Metrics;
using DataStore.Caches;
using DataStore.DependencyInjection;
using System.Runtime;
using Infrastructure.Scaling.Service;

namespace Edge.Setup;

public static class ServicesRegister
{
    public static WebApplicationBuilder RegisterServices(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        var configuration = builder.Configuration;

        // Configure threading and GC for high-load scenarios
        ConfigurePerformanceSettings(configuration);

        services.AddControllers();

        // serilog
        builder.Services.AddSerilog((_, lc) => ConfigureSerilog.Configure(lc, builder.Configuration));

        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        // health check dependencies
        services.AddHealthChecks().AddReadinessChecks(configuration);

        // cors
        builder.Services.AddCors(options => options.AddDefaultPolicy(policyBuilder =>
        {
            policyBuilder
                .AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        }));

        // add bounded memory cache
        services.AddSingleton<BoundedMemoryCache>();

        // Add DataStore services
        services
            .AddDataStore()
            .UseStore(configuration);

        // add meter factory
        services.AddSingleton<IStreamingMetrics, StreamingMetrics>();

        // Add Redis client
        services.TryAddRedis(configuration);

        services.AddSingleton<IChannelPublisher, RedisChannelPublisher>();

        // Performance monitoring
        services.AddHostedService<PerformanceMonitoringService>();

        // streaming services
        services
            .AddStreamingCore(configuration)
            .UseScaling();
            //.UseStore(configuration)
            //.UseMq(configuration);

        return builder;
    }

    private static void ConfigurePerformanceSettings(IConfiguration configuration)
    {
        var performanceSection = configuration.GetSection("Performance");
        
        // Configure thread pool for high-load scenarios
        var minWorkerThreads = Math.Max(Environment.ProcessorCount * 8, 100);
        var minCompletionPortThreads = Math.Max(Environment.ProcessorCount * 8, 100);
        var maxWorkerThreads = Math.Max(Environment.ProcessorCount * 32, 1000);
        var maxCompletionPortThreads = Math.Max(Environment.ProcessorCount * 32, 1000);
        
        ThreadPool.SetMinThreads(minWorkerThreads, minCompletionPortThreads);
        ThreadPool.SetMaxThreads(maxWorkerThreads, maxCompletionPortThreads);

        // Configure GC for server workloads
        var gcMode = performanceSection.GetValue<string>("GCCollectionMode");
        if (gcMode == "Optimized")
        {
            GCSettings.LatencyMode = GCLatencyMode.Batch;
        }

        // Configure process priority for better performance
        try
        {
            using var process = System.Diagnostics.Process.GetCurrentProcess();
            process.PriorityClass = System.Diagnostics.ProcessPriorityClass.High;
        }
        catch
        {
            // Ignore if we can't set priority
        }
    }
}