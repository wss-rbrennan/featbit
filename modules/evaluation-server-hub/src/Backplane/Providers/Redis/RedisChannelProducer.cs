using DataStore.Caches.Redis;
using Domain.BackplaneMesssages;
using Domain.Shared;
//using Infrastructure.Caches.Redis;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Backplane.Providers.Redis
{
    public partial class RedisChannelProducer(IRedisClient redisClient, ILogger<RedisChannelProducer> logger) 
        : IChannelProducer
    {
        
        public async Task PublishAsync<TMessage>(string channel, TMessage? message) where TMessage : class
        {
            try
            {
                var jsonMessage = JsonSerializer.Serialize(message, ReusableJsonSerializerOptions.Web);

                await redisClient.GetDatabase().PublishAsync(channel, jsonMessage);

                Log.ChannelMessagePublished(logger, jsonMessage);
            }
            catch (Exception ex)
            {
                Log.ErrorPublishingChannelMessage(logger, ex);
                throw;
            }
        }
    }
}
