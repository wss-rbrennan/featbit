using Infrastructure;
using Infrastructure.Channels;
using Infrastructure.Providers.Redis;
using Serilog;
using Streaming.DependencyInjection;
using Streaming.Metrics;
using System.Diagnostics.Metrics;
using DataStore.Caches;
using DataStore.DependencyInjection;

namespace Edge.Setup;

public static class ServicesRegister
{
    public static WebApplicationBuilder RegisterServices(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        var configuration = builder.Configuration;

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

        // streaming services
        services
            .AddStreamingCore()
            .UseScaling();
            //.UseStore(configuration)
            //.UseMq(configuration);

        return builder;
    }
}