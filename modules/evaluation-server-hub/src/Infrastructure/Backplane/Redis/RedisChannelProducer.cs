using Domain.BackplaneMesssages;
using Domain.Shared;
using DataStore.Caches.Redis;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Infrastructure.Backplane.Redis
{
    public partial class RedisChannelProducer : IChannelProducer
    {
        private readonly IRedisClient _redisClient;
        private readonly Dictionary<string, IChannelConsumer> _handlers;
        private readonly ILogger<RedisChannelProducer> _logger;

        public RedisChannelProducer(
            IRedisClient redisClient,
            IEnumerable<IChannelConsumer> handlers,
            ILogger<RedisChannelProducer> logger)
        {
            _redisClient = redisClient;
            _handlers = handlers.ToDictionary(h => h.Channel, h => h);
            _logger = logger;
        }

        public async Task PublishAsync<TMessage>(string channel, TMessage? message) where TMessage : class
        {
            try
            {
                var jsonMessage = JsonSerializer.Serialize(message, ReusableJsonSerializerOptions.Web);

                await _redisClient.GetDatabase().PublishAsync(channel, jsonMessage);

                Log.ChannelMessagePublished(_logger, jsonMessage);
            }
            catch (Exception ex)
            {
                Log.ErrorPublishingChannelMessage(_logger, ex);
                throw;
            }
        }
    }
}
