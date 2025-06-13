using DataStore.Caches.Redis;
using Infrastructure.BackplaneMesssages;
using Domain.Shared;
//using Infrastructure.Caches.Redis;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace Infrastructure.Providers.Redis
{
    public partial class RedisChannelProducer(IRedisClient redisClient, ILogger<RedisChannelProducer> logger)
            : IChannelProducer
    {

        public async Task PublishAsync<TMessage>(string channel, TMessage? message) where TMessage : class
        {
            try
            {
                var jsonMessage = JsonSerializer.Serialize(message, ReusableJsonSerializerOptions.Web);

                // Explicitly specify the PatternMode using RedisChannel.Literal
                var redisChannel = RedisChannel.Literal(channel);

                await redisClient.GetDatabase().PublishAsync(redisChannel, jsonMessage);

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
