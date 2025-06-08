using Domain.BackplaneMesssages;
using Domain.Messages;
using DataStore.Caches.Redis;
using Infrastructure.MQ.Redis;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Backplane.Redis
{
    public partial class RedisChannelConsumer : BackgroundService
    {
        private readonly IRedisClient _redisClient;
        private readonly Dictionary<string, IChannelConsumer> _handlers;
        private readonly ILogger<RedisChannelConsumer> _logger;

        public RedisChannelConsumer(
            IRedisClient redisClient,
            IEnumerable<IChannelConsumer> handlers,
            ILogger<RedisChannelConsumer> logger)
        {
            _redisClient = redisClient;
            _handlers = handlers.ToDictionary(h => h.Channel, h => h);
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var channel = new RedisChannel(Channels.EnvironmentPattern, RedisChannel.PatternMode.Pattern);
            var queue = await _redisClient.GetSubscriber().SubscribeAsync(channel);

            _logger.LogInformation(
                "Start consuming environment request namespace through channel {Channel}.",
                channel.ToString());

            queue.OnMessage(HandleMessageAsync);
            return;

            async Task HandleMessageAsync(ChannelMessage channelMessage)
            {
                var message = string.Empty;

                try
                {
                    var theChannel = channelMessage.Channel;
                    if (theChannel.IsNullOrEmpty)
                    {
                        return;
                    }

                    var channel = theChannel.ToString();

                    if (!_handlers.TryGetValue(channel, out var handler))
                    {
                        Log.NoHandlerForChannel(_logger, channel);
                        return;
                    }

                    var value = channelMessage.Message;
                    if (value.IsNullOrEmpty)
                    {
                        return;
                    }

                    message = value.ToString();

                    await handler.HandleAsync(message, stoppingToken);

                    Log.ChannelMessageHandled(_logger, message);
                }
                catch (Exception ex)
                {
                    Log.ErrorHandlingChannelMessage(_logger, message, ex);

                }
            }
        }
    }
}
