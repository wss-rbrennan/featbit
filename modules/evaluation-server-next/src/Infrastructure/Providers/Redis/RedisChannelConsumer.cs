using Infrastructure.BackplaneMesssages;
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
using System.Text.Json;
using Infrastructure.Scaling.Types;
using Infrastructure.Scaling.Handlers;

namespace Infrastructure.Providers.Redis
{
    public partial class RedisChannelConsumer : BackgroundService
    {
        private readonly IRedisClient _redisClient;
        private readonly Dictionary<string, IMessageHandler> _handlers;
        private readonly ILogger<RedisChannelConsumer> _logger;

        public RedisChannelConsumer(
            IRedisClient redisClient,
            IEnumerable<IMessageHandler> handlers,
            ILogger<RedisChannelConsumer> logger)
        {
            _redisClient = redisClient;
            _handlers = handlers.ToDictionary(h => h.Type, h => h);
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var pattern = Infrastructure.BackplaneMesssages.Channels.GetEdgeChannelPattern().Replace("featbit-els-edge-", "featbit:els:edge:");
            var channel = new RedisChannel(pattern, RedisChannel.PatternMode.Pattern);
            var queue = await _redisClient.GetSubscriber().SubscribeAsync(channel);

            _logger.LogInformation(
                "Start consuming environment request namespace through channel {Channel}.",
                channel.ToString());

            queue.OnMessage(HandleMessageAsync);
            return;

            async Task HandleMessageAsync(ChannelMessage channelMessage)
            {
                try
                {
                    var theChannel = channelMessage.Channel;
                    if (theChannel.IsNullOrEmpty)
                    {
                        return;
                    }

                    var value = channelMessage.Message;
                    if (value.IsNullOrEmpty)
                    {
                        return;
                    }

                    var message = JsonSerializer.Deserialize<Message>(channelMessage.Message.ToString(), JsonSerializerOptions.Web);

                    // Log correlation information if available
                    if (!string.IsNullOrEmpty(message.SenderId) || !string.IsNullOrEmpty(message.CorrelationId))
                    {
                        _logger.LogInformation("Hub processing message from {ServiceType} - SenderId: {SenderId}, CorrelationId: {CorrelationId}, Channel: {Channel}",
                            message.ServiceType ?? "unknown", message.SenderId, message.CorrelationId, theChannel);
                    }

                    var messageContext = JsonSerializer.Deserialize<MessageContext>(message.MessageContent, JsonSerializerOptions.Web);

                    var messageType = messageContext.Data.GetProperty("messageType");

                    if (!_handlers.TryGetValue(messageType.ToString(), out var handler))
                    {
                        Log.NoHandlerForChannel(_logger, theChannel);
                        return;
                    }

                    await handler.HandleAsync(messageContext);

                    Log.ChannelMessageHandled(_logger, message.ToString());
                }
                catch (Exception ex)
                {
                    Log.ErrorHandlingChannelMessage(_logger, channelMessage.Message.ToString(), ex);
                }
            }
        }
    }
}
