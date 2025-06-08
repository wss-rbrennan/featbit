using Backplane.Consumer;
using Backplane.Consumers;
using Backplane.Providers;
using Domain.BackplaneMesssages;
using Domain.Messages;
using Infrastructure;
using Infrastructure.Backplane.Redis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
                services
                    .AddSingleton<IChannelConsumer, EnvironmentRequestConsumer>();
            }

            void AddNone()
            {
                builder.Services.AddSingleton<IChannelProducer, NoneChannelProducer>();
            }

            void AddRedis(IConfiguration config)
            {
                //var connectionString = config.GetRedisConnectionString();
                                               

                services.AddSingleton<IChannelProducer, RedisChannelProducer>();
                services.AddHostedService<RedisChannelConsumer>();
            }

        }
    }
}
