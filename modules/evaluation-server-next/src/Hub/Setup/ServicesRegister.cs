using Backplane.DependencyInjection;
using Infrastructure.Connections;
using DataStore.DependencyInjection;
using Infrastructure;
using Infrastructure.MQ.Redis;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using DataStore.Caches.Redis;
using Infrastructure.MQ;
using Domain.Messages;
using Backplane.Consumers;
using Infrastructure.Channels;
using Application.Services;
using Infrastructure.Providers.Redis;
using Infrastructure.BackplaneMesssages;
using Backplane.Messages;
using Infrastructure.Scaling.Handlers;

namespace Api.Setup;

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

        // Add backplane services
        services
            .AddRelayProxySupport(options =>
            {
                options.SupportedTypes = supportedTypes;
            }).UseBackplane(configuration);

        services.AddSingleton<IChannelPublisher, RedisChannelPublisher>();
        services.AddSingleton<IDataSyncService, DataSyncService>();


        services
            .AddSingleton<IMessageConsumer, FeatureFlagChangeMessageConsumer>()
            .AddSingleton<IMessageConsumer, SegmentChangeMessageConsumer>();

        services
            .AddHostedService<RedisMessageConsumer>();
        return builder;
    }
}