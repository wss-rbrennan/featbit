using Domain.Shared;
using Infrastructure;
using DataStore.Caches;
using Infrastructure.BackplaneMesssages;
using Infrastructure.Providers.Redis;
using Streaming.Scaling.Service;
using Streaming.Scaling.Manager;
using Infrastructure.Scaling.Service;

namespace Streaming.DependencyInjection;

public static class StreamingBuilderExtensions
{
    //public static IStreamingBuilder UseMq(this IStreamingBuilder builder, IConfiguration configuration)
    //{
    //    var services = builder.Services;

    //    var mqProvider = configuration.GetMqProvider();
    //    if (mqProvider != MqProvider.None)
    //    {
    //        AddConsumers();
    //    }

    //    switch (mqProvider)
    //    {
    //        case MqProvider.None:
    //            AddNone();
    //            break;
    //        case MqProvider.Redis:
    //            AddRedis();
    //            break;
    //        case MqProvider.Kafka:
    //            AddKafka();
    //            break;
    //        case MqProvider.Postgres:
    //            AddPostgres();
    //            break;
    //    }

    //    return builder;

    //    void AddConsumers()
    //    {
    //        services
    //            .AddSingleton<IMessageConsumer, FeatureFlagChangeMessageConsumer>()
    //            .AddSingleton<IMessageConsumer, SegmentChangeMessageConsumer>();
    //    }

    //    void AddNone()
    //    {
    //        builder.Services.AddSingleton<IMessageProducer, NoneMessageProducer>();
    //    }

    //    void AddRedis()
    //    {
    //        services.TryAddRedis(configuration);

    //        services.AddSingleton<IMessageProducer, RedisMessageProducer>();
    //        services.AddHostedService<RedisMessageConsumer>();
    //    }

    //    void AddKafka()
    //    {
    //        var producerConfigDictionary = new Dictionary<string, string>();
    //        configuration.GetSection("Kafka:Producer").Bind(producerConfigDictionary);
    //        var producerConfig = new ProducerConfig(producerConfigDictionary);
    //        services.AddSingleton(producerConfig);

    //        var consumerConfigDictionary = new Dictionary<string, string>();
    //        configuration.GetSection("Kafka:Consumer").Bind(consumerConfigDictionary);
    //        var consumerConfig = new ConsumerConfig(consumerConfigDictionary);
    //        services.AddSingleton(consumerConfig);

    //        services.AddSingleton<IMessageProducer, KafkaMessageProducer>();
    //        services.AddHostedService<KafkaMessageConsumer>();
    //    }

    //    void AddPostgres()
    //    {
    //        services.TryAddPostgres(configuration);

    //        services.AddSingleton<IMessageProducer, PostgresMessageProducer>();
    //        services.AddHostedService<PostgresMessageConsumer>();
    //    }
    //}

    //public static IStreamingBuilder UseStore<TStoreType>(this IStreamingBuilder builder) where TStoreType : IStore
    //{
    //    builder.Services.AddSingleton(typeof(IStore), typeof(TStoreType));

    //    return builder;
    //}

    public static IStreamingBuilder UseScaling(this IStreamingBuilder builder)
    {
        var services = builder.Services;

        services.AddSingleton<IServiceIdentityProvider, ServiceIdentityProvider>();
        services.AddSingleton<IMessageFactory, MessageFactory>();
        services.AddSingleton<IBackplaneManager, RedisManager>();
        services.AddSingleton<ISubscriptionService, SubscriptionService>();
        services.AddSingleton<IWebSocketService, WebSocketService>();
        services.AddHostedService<WebSocketService>();
        services.AddHostedService<WebSocketShutdownService>();
        services.AddApplicationShutdownMonitoring();

        return builder;
    }

}