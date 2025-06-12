using Domain;
using DataStore;
using DataStore.Caches.Redis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Application.Connections;
using Application.Messages;
using DataStore.Persistence.MongoDb;
using DataStore.Persistence;

namespace Application.DependencyInjection;

public static class StreamingServiceCollectionExtensions
{
    public static IStreamingBuilder AddStreamingCore(
        this IServiceCollection services)
    {

        // message handlers
        //services
        //    .AddTransient<IMessageHandler, DataSyncMessageHandler>();

        return new StreamingBuilder(services);
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