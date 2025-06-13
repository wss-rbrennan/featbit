using Backplane.Messages;
using Backplane.EdgeConnections;
using Backplane.Services;
using Backplane.Validators;
using Domain;
using Infrastructure;
using Infrastructure.Providers;
using DataStore.Persistence;
using DataStore.Persistence.MongoDb;
using Microsoft.Extensions.Internal;
using Npgsql;
using DataStore.Caches.Redis;
using IRedisClient = DataStore.Caches.Redis.IRedisClient;
using Infrastructure.BackplaneMesssages;
using Microsoft.Extensions.Configuration;
using Infrastructure.Scaling.Handlers;

namespace Backplane.DependencyInjection;

public static class BackplaneServiceCollectionExtensions
{
    public static IBackplaneBuilder AddBackplane(
        this IServiceCollection services,
        Action<BackplaneOptions>? configureOptions = null)
    {
        // add streaming options
        var options = new BackplaneOptions();
        configureOptions?.Invoke(options);
        services.AddSingleton(options);

        // system clock
        services.AddSingleton<ISystemClock, SystemClock>();

        // request validator
        services.AddSingleton<IRequestValidator, RequestValidator>();

        // services
        services
            .AddEvaluator();

        if (options.SupportedTypes.Contains(ConnectionType.RelayProxy))
        {
            services.AddTransient<IRelayProxyService, RelayProxyService>();
        }

        // Add Redis client if not already added
        var configuration = services.BuildServiceProvider().GetRequiredService<IConfiguration>();
        services.TryAddRedis(configuration);

        services
            .AddSingleton<IChannelProducer, Infrastructure.Providers.Redis.RedisChannelProducer>();
        // message handlers
        services
            .AddSingleton<IMessageHandler, DataSyncMessageHandler>()
            .AddSingleton<Infrastructure.Channels.IChannelPublisher, Infrastructure.Providers.Redis.RedisChannelPublisher>();

        return new BackplaneBuilder(services);
    }

    public static void TryAddRedis(this IServiceCollection services, IConfiguration configuration)
    {
        if (services.Any(service => service.ServiceType == typeof(IRedisClient)))
        {
            return;
        }

        services.AddOptionsWithValidateOnStart<RedisOptions>()
            .Bind(configuration.GetSection(RedisOptions.Redis))
            .ValidateDataAnnotations();

        services.AddSingleton<IRedisClient, RedisClient>();
    }

    public static void TryAddMongoDb(this IServiceCollection services, IConfiguration configuration)
    {
        if (services.Any(service => service.ServiceType == typeof(IMongoDbClient)))
        {
            return;
        }

        services.AddOptionsWithValidateOnStart<MongoDbOptions>()
            .Bind(configuration.GetSection(MongoDbOptions.MongoDb))
            .ValidateDataAnnotations();

        services.AddSingleton<IMongoDbClient, MongoDbClient>();
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