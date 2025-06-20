using Application.Services;
using Application.Validation;
using Backplane.Consumers;
using Backplane.Messages;
using Confluent.Kafka;
using Domain;
using Domain.Messages;
using Infrastructure;
using Infrastructure.BackplaneMesssages;
using Infrastructure.MQ;
using Infrastructure.MQ.Kafka;
using Infrastructure.MQ.Postgres;
using Infrastructure.MQ.Redis;
using Infrastructure.Providers;
using Infrastructure.Providers.Redis;
using Infrastructure.Scaling.Handlers;
using Infrastructure.Scaling.Service;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backplane.DependencyInjection
{
    public static class BackplaneBuilderExtensions
    {
        public static IBackplaneBuilder UseMq(this IBackplaneBuilder builder, IConfiguration configuration)
        {
            var services = builder.Services;

            var mqProvider = configuration.GetMqProvider();
            if (mqProvider != MqProvider.None)
            {
                AddConsumers();
            }

            switch (mqProvider)
            {
                case MqProvider.None:
                    AddNone();
                    break;
                case MqProvider.Redis:
                    AddRedis();
                    break;
                case MqProvider.Kafka:
                    AddKafka();
                    break;
                case MqProvider.Postgres:
                    AddPostgres();
                    break;
            }

            return builder;

            void AddConsumers()
            {
                services
                    .AddSingleton<IMessageConsumer, FeatureFlagChangeMessageConsumer>()
                    .AddSingleton<IMessageConsumer, SegmentChangeMessageConsumer>();
            }

            void AddNone()
            {
                builder.Services.AddSingleton<IMessageProducer, NoneMessageProducer>();
            }

            void AddRedis()
            {

                services.AddSingleton<IMessageProducer, RedisMessageProducer>();
                services.AddHostedService<RedisMessageConsumer>();
            }

            void AddKafka()
            {
                var producerConfigDictionary = new Dictionary<string, string>();
                configuration.GetSection("Kafka:Producer").Bind(producerConfigDictionary);
                var producerConfig = new ProducerConfig(producerConfigDictionary);
                services.AddSingleton(producerConfig);

                var consumerConfigDictionary = new Dictionary<string, string>();
                configuration.GetSection("Kafka:Consumer").Bind(consumerConfigDictionary);
                var consumerConfig = new ConsumerConfig(consumerConfigDictionary);
                services.AddSingleton(consumerConfig);

                services.AddSingleton<IMessageProducer, KafkaMessageProducer>();
                services.AddHostedService<KafkaMessageConsumer>();
            }

            void AddPostgres()
            {
                services.TryAddPostgres(configuration);

                services.AddSingleton<IMessageProducer, PostgresMessageProducer>();
                services.AddHostedService<PostgresMessageConsumer>();
            }
        }
    }
}
