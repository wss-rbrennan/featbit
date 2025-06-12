using Domain.Messages;
using Domain.Shared;
using DataStore;
using DataStore.Caches;
using DataStore.Fakes;
using DataStore.Persistence;
using DataStore.Store;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
//using Application.Consumers;

namespace DataStore.DependencyInjection;

public static class DataStoreBuilderExtensions
{
    
    public static IDataStoreBuilder UseStore<TStoreType>(this IDataStoreBuilder builder) where TStoreType : IStore
    {
        builder.Services.AddSingleton(typeof(IStore), typeof(TStoreType));

        return builder;
    }

    public static IDataStoreBuilder UseStore(this IDataStoreBuilder builder, IConfiguration configuration)
    {
        var services = builder.Services;

        var dbProvider = configuration.GetDbProvider();
        switch (dbProvider.Name)
        {
            case DbProvider.Fake:
                AddFake();
                break;
            case DbProvider.MongoDb:
                AddMongoDb();
                break;
            case DbProvider.Postgres:
                AddPostgres();
                break;
        }

        var cacheProvider = configuration.GetCacheProvider();
        switch (cacheProvider)
        {
            case CacheProvider.Redis:
                AddRedis();
                break;

            case CacheProvider.None:
                // use db store if no cache provider is specified
                services.AddSingleton<IStore>(x => x.GetRequiredService<IDbStore>());
                break;
        }

        return builder;

        void AddFake()
        {
            services.AddSingleton<IDbStore, FakeStore>();
        }

        void AddMongoDb()
        {
            services.TryAddMongoDb(configuration);
            services.AddTransient<IDbStore, MongoDbStore>();
        }

        void AddPostgres()
        {
            services.TryAddPostgres(configuration);
            services.AddTransient<IDbStore, PostgresStore>();
        }

        void AddRedis()
        {
            services.TryAddRedis(configuration);
            services.AddTransient<IDbStore, RedisStore>();

            // use hybrid store if we use Redis cache
            services.AddSingleton<IStore, HybridStore>();
            services.AddHostedService<StoreAvailableSentinel>();
        }
    }
}