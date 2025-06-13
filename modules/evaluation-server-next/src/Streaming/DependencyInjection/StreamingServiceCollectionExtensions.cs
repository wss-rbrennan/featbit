using Domain;
using Infrastructure;
using DataStore.Persistence;
using DataStore.Persistence.MongoDb;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.DependencyInjection;
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

        // system clock
        services.AddSingleton<ISystemClock, SystemClock>();

        // request validator
        services.AddSingleton<IRequestValidator, RequestValidator>();

        if (options.SupportedTypes.Contains(ConnectionType.RelayProxy))
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