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
}