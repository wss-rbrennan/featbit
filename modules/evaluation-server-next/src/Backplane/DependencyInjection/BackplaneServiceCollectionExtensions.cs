using Application.Services;
using Application.Validation;
using Backplane;
using Backplane.Messages;
using Confluent.Kafka;
using DataStore.Caches.Redis;
using DataStore.Persistence;
using DataStore.Persistence.MongoDb;
using Domain;
using Domain.Messages;
using Infrastructure;
using Infrastructure.BackplaneMesssages;
using Infrastructure.Connections;
using Infrastructure.MQ.Redis;
using Infrastructure.Protocol;
using Infrastructure.Providers;
using Infrastructure.Providers.Redis;
using Infrastructure.Scaling.Handlers;
using Infrastructure.Scaling.Service;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Options;
using Npgsql;
using IRedisClient = DataStore.Caches.Redis.IRedisClient;

namespace Backplane.DependencyInjection;

public static class BackplaneServiceCollectionExtensions
{

    public static IBackplaneBuilder AddBackplane(
        this IServiceCollection services,
        IConfiguration config,
        Action<StreamingOptions>? configureOptions = null)
    {
        // add streaming options


        services.AddOptionsWithValidateOnStart<StreamingOptions>()
            .Bind(config.GetSection(StreamingOptions.Streaming))
            .PostConfigure(configureOptions ?? (_ => { }));

        // Register as singleton for dependency injection
        services.AddSingleton<StreamingOptions>(provider =>
            provider.GetRequiredService<IOptions<StreamingOptions>>().Value);

        var backplaneProvider = config.GetBackplaneProvider();
        if (backplaneProvider != BackplaneProvider.None)
        {
            AddConsumers();
        }

        switch (backplaneProvider)
        {
            case BackplaneProvider.None:
                AddNone();
                break;
            case BackplaneProvider.Redis:
                AddRedis(config);
                break;
        }

        // system clock
        services.AddSingleton<ISystemClock, SystemClock>();

        // request validator
        services.AddSingleton<IRequestValidator, RequestValidator>();

        services
        .AddEvaluator();

        services
        .AddSingleton<IChannelProducer, Infrastructure.Providers.Redis.RedisChannelProducer>();
        // message handlers
        services
            .AddSingleton<IMessageHandler, DataSyncMessageHandler>()
            .AddSingleton<Infrastructure.Channels.IChannelPublisher, Infrastructure.Providers.Redis.RedisChannelPublisher>();

        return new BackplaneBuilder(services);

        void AddConsumers()
        {
            // Add message correlation services
            services.AddSingleton<IServiceIdentityProvider, ServiceIdentityProvider>();
            services.AddSingleton<IMessageFactory, MessageFactory>();

            services.AddSingleton<IDataSyncService, DataSyncService>();
        }

        void AddNone()
        {
            services.AddSingleton<IChannelProducer, NoneChannelProducer>();
        }

        void AddRedis(IConfiguration config)
        {
            services.AddSingleton<IChannelProducer, RedisChannelProducer>();
            services.AddSingleton<IMessageProducer, RedisMessageProducer>();
            services.AddHostedService<RedisChannelConsumer>();
        }
        
    }


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

    public static void TryAddPostgres(this IServiceCollection services, IConfiguration configuration)
    {
        if (services.Any(service => service.ServiceType == typeof(NpgsqlDataSource)))
        {
            return;
        }

        services.AddOptionsWithValidateOnStart<PostgresOptions>()
            .Bind(configuration.GetSection(PostgresOptions.Postgres))
            .ValidateDataAnnotations();

        services.AddNpgsqlDataSource(configuration.GetPostgresConnectionString());
    }
}