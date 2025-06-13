using Backplane.Consumers;
using Application.Services;
using Infrastructure.BackplaneMesssages;
using Domain.Messages;
using Infrastructure;
using Infrastructure.Providers;
using Infrastructure.Providers.Redis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Infrastructure.MQ.Redis;
using Infrastructure.Scaling.Service;

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
