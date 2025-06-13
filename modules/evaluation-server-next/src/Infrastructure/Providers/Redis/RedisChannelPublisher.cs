using Infrastructure.Channels;
using Infrastructure.Protocol;
using DataStore.Caches.Redis;
using Microsoft.Extensions.Logging;
using Domain.Shared;
using StackExchange.Redis;

namespace Infrastructure.Providers.Redis;

public class RedisChannelPublisher : IChannelPublisher
{
    private readonly IRedisClient _redisClient;
    private readonly ILogger<RedisChannelPublisher> _logger;

    public RedisChannelPublisher(IRedisClient redisClient, ILogger<RedisChannelPublisher> logger)
    {
        _redisClient = redisClient;
        _logger = logger;
    }

    public async Task PublishAsync<T>(string channelId, T message)
    {
        try
        {
            var jsonMessage = System.Text.Json.JsonSerializer.Serialize(message, ReusableJsonSerializerOptions.Web);
            var redisChannel = RedisChannel.Literal(channelId); // Explicitly specify the channel as a literal
            await _redisClient.GetDatabase().PublishAsync(redisChannel, jsonMessage);
            _logger.LogDebug("Channel message {Message} was published successfully to channel {Channel}", jsonMessage, channelId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while publishing message to channel {Channel}", channelId);
            throw;
        }
    }
}