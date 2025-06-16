using Application.Services;
using Application.Validation;
using DataStore.Caches.Redis;
using DataStore.DependencyInjection;
using Domain;
using Domain.Messages;
using Infrastructure;
using Infrastructure.BackplaneMesssages;
using Infrastructure.Channels;
using Infrastructure.Connections;
using Infrastructure.MQ;
using Infrastructure.MQ.Redis;
using Infrastructure.Protocol;
using Infrastructure.Providers.Redis;
using Infrastructure.Scaling.Handlers;
using Infrastructure.Scaling.Service;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Options;
using Serilog;

namespace Web.Setup;

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

        var supportedTypes = new[] { ConnectionType.RelayProxy, ConnectionType.Server };

        // Add DataStore services
        services
            .AddDataStore()
            .UseStore(configuration);

        services.AddSingleton<ISystemClock, SystemClock>();

        // Fix: Correctly call AddStreamingOptions extension method
        services.AddStreamingOptions(configuration, options =>
        {
            options.SupportedTypes = supportedTypes;
        });

        services.AddEvaluator();

        services.AddSingleton<IRequestValidator, RequestValidator>();
        services.AddSingleton<IChannelPublisher, RedisChannelPublisher>();
        services.AddSingleton<IDataSyncService, DataSyncService>();

        // Add message producer for InsightController
        services.AddSingleton<IMessageProducer, RedisMessageProducer>();

        services
            .AddHostedService<RedisMessageConsumer>();

        // Add application shutdown monitoring
        services.AddApplicationShutdownMonitoring();

        return builder;
    }

    public static IServiceCollection AddStreamingOptions(this IServiceCollection services,
        IConfiguration configuration,
        Action<StreamingOptions>? configureOptions = null)
    {
        // Configure StreamingOptions from configuration
        services.AddOptionsWithValidateOnStart<StreamingOptions>()
            .Bind(configuration.GetSection(StreamingOptions.Streaming))
            .PostConfigure(configureOptions ?? (_ => { }));

        // Register as singleton for dependency injection
        services.AddSingleton<StreamingOptions>(provider =>
            provider.GetRequiredService<IOptions<StreamingOptions>>().Value);

        // Get options to check supported types
        var optionsBuilder = configuration.GetSection(StreamingOptions.Streaming);
        var configuredOptions = new StreamingOptions();
        optionsBuilder.Bind(configuredOptions);
        configureOptions?.Invoke(configuredOptions);

        return services;
    }
}