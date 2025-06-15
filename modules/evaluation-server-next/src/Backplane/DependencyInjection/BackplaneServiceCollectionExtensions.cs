using Backplane.Messages;
using Backplane;
using Application.Services;
using Application.Validation;
using Domain;
using Infrastructure;
using Infrastructure.Providers;
using DataStore.Persistence;
using DataStore.Persistence.MongoDb;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Options;
using Npgsql;
using DataStore.Caches.Redis;
using IRedisClient = DataStore.Caches.Redis.IRedisClient;
using Infrastructure.BackplaneMesssages;
using Microsoft.Extensions.Configuration;
using Infrastructure.Scaling.Handlers;
using Infrastructure.Protocol;
using Infrastructure.Connections;

namespace Backplane.DependencyInjection;

public static class BackplaneServiceCollectionExtensions
{
    public static IBackplaneBuilder AddRelayProxySupport(
        this IServiceCollection services,
        Action<StreamingOptions>? configureOptions = null)
    {
        // add streaming options
        var options = new StreamingOptions();
        configureOptions?.Invoke(options);
        services.AddSingleton(options);

        if (options.SupportedTypes.Contains(ConnectionType.RelayProxy))
        {
            services.AddTransient<IRelayProxyService, RelayProxyService>();
        }

        return new BackplaneBuilder(services);
    }

    public static IBackplaneBuilder AddRelayProxySupport(
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

        // Get options to check supported types
        var optionsBuilder = configuration.GetSection(StreamingOptions.Streaming);
        var configuredOptions = new StreamingOptions();
        optionsBuilder.Bind(configuredOptions);
        configureOptions?.Invoke(configuredOptions);

        if (configuredOptions.SupportedTypes.Contains(ConnectionType.RelayProxy))
        {
            services.AddTransient<IRelayProxyService, RelayProxyService>();
        }

        return new BackplaneBuilder(services);
    }
}