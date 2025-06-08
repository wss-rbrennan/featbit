using Backplane.Channels;
using Backplane.Protocol;
using DataStore.Caches.Redis;
using Microsoft.Extensions.Logging;
using Domain.Shared;

namespace Backplane.Providers.Redis;

public class RedisChannelPublisher : IChannelPublisher
{
    private readonly IRedisClient _redisClient;
    private readonly ILogger<RedisChannelPublisher> _logger;

    public RedisChannelPublisher(IRedisClient redisClient, ILogger<RedisChannelPublisher> logger)
    {
        _redisClient = redisClient;
        _logger = logger;
    }

    public async Task PublishToChannelAsync(string channelId, ServerMessage serverMessage, CancellationToken cancellationToken)
    {
        try
        {
            var jsonMessage = System.Text.Json.JsonSerializer.Serialize(serverMessage, ReusableJsonSerializerOptions.Web);
            await _redisClient.GetDatabase().PublishAsync(channelId, jsonMessage);
            _logger.LogDebug("Channel message {Message} was published successfully to channel {Channel}", jsonMessage, channelId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while publishing message to channel {Channel}", channelId);
            throw;
        }
    }
} 