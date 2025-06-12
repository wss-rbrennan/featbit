using DataStore.Caches;
using DataStore.Caches.Redis;
using DataStore.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DataStore;

public static class HealthCheckBuilderExtensions
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    public const string ReadinessTag = "Readiness";

    public static IHealthChecksBuilder AddReadinessChecks(
        this IHealthChecksBuilder builder,
        IConfiguration configuration)
    {
        var tags = new[] { ReadinessTag };

        var dbProvider = configuration.GetDbProvider();
        switch (dbProvider.Name)
        {
            case DbProvider.MongoDb:
                builder.AddMongoDb(
                    serviceProvider =>
                    {
                        var mongoClient = new MongoDB.Driver.MongoClient(dbProvider.ConnectionString);
                        return mongoClient;
                    },
                    tags: tags,
                    timeout: Timeout
                );
                break;
            case DbProvider.Postgres:
                builder.AddNpgSql(tags: tags);
                break;
        }

        var cacheProvider = configuration.GetCacheProvider();
        if (cacheProvider == CacheProvider.Redis)
        {
            builder.AddRedis(
                serviceProvider => serviceProvider.GetRequiredService<IRedisClient>().Connection,
                tags: tags,
                timeout: Timeout
            );
        }

        return builder;
    }
}