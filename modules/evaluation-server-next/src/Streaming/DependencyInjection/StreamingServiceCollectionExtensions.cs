using Domain;
using Infrastructure;
using DataStore.Persistence;
using DataStore.Persistence.MongoDb;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using Streaming.Connections;
using Streaming.Messages;
using Application.Services;
using DataStore.Caches.Redis;
using IRedisClient = DataStore.Caches.Redis.IRedisClient;
using Infrastructure.Connections;
using Infrastructure.Protocol;
using Application.Validation;

namespace Streaming.DependencyInjection;

public static class StreamingServiceCollectionExtensions
{
    public static IStreamingBuilder AddStreamingCore(
        this IServiceCollection services,
        Action<StreamingOptions>? configureOptions = null)
    {
        // add streaming options
        var options = new StreamingOptions();
        configureOptions?.Invoke(options);
        services.AddSingleton(options);

        return new StreamingBuilder(services);
    }

    public static IStreamingBuilder AddStreamingCore(
        this IServiceCollection services,
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

        // system clock
        services.AddSingleton<ISystemClock, SystemClock>();

        // request validator
        services.AddSingleton<IRequestValidator, RequestValidator>();

        // Get options to check supported types
        var optionsBuilder = configuration.GetSection(StreamingOptions.Streaming);
        var configuredOptions = new StreamingOptions();
        optionsBuilder.Bind(configuredOptions);
        configureOptions?.Invoke(configuredOptions);

        if (configuredOptions.SupportedTypes.Contains(ConnectionType.RelayProxy))
        {
            services.AddTransient<IRelayProxyService, RelayProxyService>();
        }

        // connection
        services.AddSingleton<IConnectionManager, ConnectionManager>();

        // message handlers
        services
            .AddSingleton<MessageDispatcher>()
            .AddTransient<IMessageHandler, PingMessageHandler>();

        return new StreamingBuilder(services);
    }




}