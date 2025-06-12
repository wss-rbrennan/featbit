using Domain.Messages;
using DataStore.Caches.Redis;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Infrastructure.MQ.Redis;

public partial class RedisMessageConsumer : BackgroundService
{
    private readonly IRedisClient _redisClient;
    private readonly Dictionary<string, IMessageConsumer> _handlers;
    private readonly ILogger<RedisMessageConsumer> _logger;

    public RedisMessageConsumer(
        IRedisClient redisClient,
        IEnumerable<IMessageConsumer> handlers,
        ILogger<RedisMessageConsumer> logger)
    {
        _redisClient = redisClient;
        _handlers = handlers.ToDictionary(x => x.Topic, x => x);
        _logger = logger;
        
        // Log registered handlers
        foreach (var handler in _handlers)
        {
            _logger.LogInformation("Registered handler for topic: {Topic}", handler.Key);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Subscribe Topics.FeatureFlagChange, Topics.SegmentChange
        var channel = new RedisChannel(Topics.DataChangePattern, RedisChannel.PatternMode.Pattern);
        var queue = await _redisClient.GetSubscriber().SubscribeAsync(channel);

        _logger.LogInformation(
            "Start consuming flag & segment change messages through channel {Channel}.",
            channel.ToString()
        );
        // process messages sequentially. ref: https://stackexchange.github.io/StackExchange.Redis/PubSubOrder.html
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

                var topic = theChannel.ToString();
                _logger.LogInformation("Received message on topic: {Topic}", topic);
                
                // Find the handler that matches the topic pattern
                var handler = _handlers.Values.FirstOrDefault(h => 
                    topic.Equals(h.Topic, StringComparison.OrdinalIgnoreCase));
                
                if (handler == null)
                {
                    _logger.LogWarning("No handler found for topic: {Topic}. Available handlers: {Handlers}", 
                        topic, string.Join(", ", _handlers.Keys));
                    Log.NoHandlerForTopic(_logger, topic);
                    return;
                }

                var value = channelMessage.Message;
                if (value.IsNullOrEmpty)
                {
                    return;
                }

                message = value.ToString();
                await handler.HandleAsync(message, stoppingToken);

                Log.MessageHandled(_logger, message);
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
            catch (Exception ex)
            {
                Log.ErrorConsumeMessage(_logger, message, ex);
            }
        }
    }
}