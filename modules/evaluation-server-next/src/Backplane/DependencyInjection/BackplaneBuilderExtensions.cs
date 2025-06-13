using Application.Services;
using Application.Validation;
using Backplane.Consumers;
using Domain.Messages;
using Infrastructure;
using Infrastructure.BackplaneMesssages;
using Infrastructure.MQ.Redis;
using Infrastructure.Providers;
using Infrastructure.Providers.Redis;
using Infrastructure.Scaling.Service;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Internal;
using Domain;
using Infrastructure.Scaling.Handlers;
using Backplane.Messages;

namespace Backplane.DependencyInjection
{
    public static class BackplaneBuilderExtensions
    {
        public static IBackplaneBuilder UseBackplane(this IBackplaneBuilder builder, IConfiguration config)
        {

            var services = builder.Services;

            var backplaneProvider = config.GetBackplaneProvider();
            if (backplaneProvider != BackplaneProvider.None)
            {
                AddConsumers();
            }

            switch(backplaneProvider)
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

            return builder;

            void AddConsumers()
            {
                // Add message correlation services
                services.AddSingleton<IServiceIdentityProvider, ServiceIdentityProvider>();
                services.AddSingleton<IMessageFactory, MessageFactory>();

                services.AddSingleton<IDataSyncService, DataSyncService>();
            }

            void AddNone()
            {
                builder.Services.AddSingleton<IChannelProducer, NoneChannelProducer>();
            }

            void AddRedis(IConfiguration config)
            {
                services.AddSingleton<IChannelProducer, RedisChannelProducer>();
                services.AddSingleton<IMessageProducer, RedisMessageProducer>();
                services.AddHostedService<RedisChannelConsumer>();
            }

        }
    }
}
