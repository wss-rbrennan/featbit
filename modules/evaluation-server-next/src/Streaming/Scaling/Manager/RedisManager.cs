using Streaming.Scaling.Manager;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System;
using System.Threading.Tasks;
using System.Text.Json;

namespace Streaming.Scaling.Manager
{
    public class RedisManager : IBackplaneManager
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ISubscriber _subscriber;
        private readonly IConfiguration _configuration;
        private readonly ILogger<RedisManager> _logger;
        private const int MaxRetries = 3;
        private const int RetryDelayMs = 1000;

        public RedisManager(ILogger<RedisManager> logger, IConfiguration configuration) 
        {
            _configuration = configuration;
            _logger = logger;
            var redisSection = _configuration.GetSection("Redis");
            var connectionString = redisSection["ConnectionString"];
            var instanceName = redisSection["InstanceName"];

            _logger.LogDebug($"Connecting to Redis at: {connectionString}");
            var options = ConfigurationOptions.Parse(connectionString!);
            options.ConnectTimeout = 5000; // 5 seconds
            options.SyncTimeout = 5000; // 5 seconds
            options.AbortOnConnectFail = false; // Don't throw on connection failure
            options.ConnectRetry = 3; // Retry connection 3 times

            _redis = ConnectionMultiplexer.Connect(options);
            _subscriber = _redis.GetSubscriber();
            _logger.LogDebug("Redis connection established successfully");
        }

        public async Task ConnectAsync()
        {
            _logger.LogDebug("Verifying Redis connection...");
            for (int i = 0; i < MaxRetries; i++)
            {
                try
                {
                    if (!_redis.IsConnected)
                    {
                        _logger.LogDebug($"Redis not connected, attempt {i + 1} of {MaxRetries}...");
                        await _redis.GetDatabase().PingAsync();
                    }
                    _logger.LogDebug("Redis connection verified");
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Redis connection attempt {i + 1} failed: {ex.Message}");
                    if (i < MaxRetries - 1)
                    {
                        _logger.LogError($"Waiting {RetryDelayMs}ms before retry...");
                        await Task.Delay(RetryDelayMs);
                    }
                    else
                    {
                        throw new Exception("Failed to connect to Redis after multiple attempts", ex);
                    }
                }
            }
        }

        public async Task<long> PublishAsync(string channel, string message)
        {
            _logger.LogDebug("Publishing message to Redis channel: {Channel}", channel);
            try
            {
                if (!_redis.IsConnected)
                {
                    _logger.LogWarning("Redis not connected, attempting to reconnect...");
                    await ConnectAsync();
                }

                var redisChannel = new RedisChannel(channel, RedisChannel.PatternMode.Literal);
                var subscriberCount = await _subscriber.PublishAsync(redisChannel, message);
                _logger.LogDebug("Published message to Redis channel {Channel}, received by {Count} subscribers", channel, subscriberCount);
                return subscriberCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing to Redis channel: {Channel}", channel);
                throw;
            }
        }

        public async Task SubscribeAsync(string channel, Action<string> callback)
        {
            _logger.LogDebug("Setting up Redis subscription for channel: {Channel}", channel);
            try
            {
                if (!_redis.IsConnected)
                {
                    _logger.LogWarning("Redis not connected, attempting to reconnect...");
                    await ConnectAsync();
                }

                // Check if this is a pattern subscription
                bool isPattern = channel.Contains("*");
                var redisChannel = isPattern 
                    ? new RedisChannel(channel, RedisChannel.PatternMode.Pattern)
                    : new RedisChannel(channel, RedisChannel.PatternMode.Literal);

                _logger.LogDebug("Setting up Redis subscription for channel: {Channel} (Pattern: {IsPattern})", channel, isPattern);
                
                // Subscribe using the correct pattern mode
                await _subscriber.SubscribeAsync(redisChannel, (_, value) =>
                {
                    _logger.LogDebug("Received message from Redis channel '{Channel}': {Message}", channel, value);
                    try
                    {
                        callback(value!);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in callback for Redis channel {Channel}", channel);
                    }
                });
                
                _logger.LogDebug("Successfully subscribed to Redis channel: {Channel} (Pattern: {IsPattern})", channel, isPattern);

                // Verify subscription with a properly formatted test message
                var testMessage = new
                {
                    type = "server",
                    channelId = "test",
                    channelName = "test",
                    message = new
                    {
                        messageType = "test",
                        data = new { timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }
                    }
                };
                var testMessageJson = JsonSerializer.Serialize(testMessage);
                var subscriptionCount = await _subscriber.PublishAsync(redisChannel, testMessageJson);
                _logger.LogDebug("Subscription verification - published test message to {Channel}, received by {Count} subscribers", channel, subscriptionCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error subscribing to Redis channel: {Channel}", channel);
                throw;
            }
        }

        public async Task UnsubscribeAsync(string channel)
        {
            _logger.LogDebug($"Unsubscribing from Redis channel: {channel}");
            try
            {
                if (!_redis.IsConnected)
                {
                    _logger.LogDebug("Redis not connected, attempting to reconnect...");
                    await ConnectAsync();
                }

                // Explicitly specify the PatternMode as Literal for the RedisChannel
                var redisChannel = new RedisChannel(channel, RedisChannel.PatternMode.Literal);
                await _subscriber.UnsubscribeAsync(redisChannel);

                _logger.LogDebug($"Successfully unsubscribed from Redis channel: {channel}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error unsubscribing from Redis channel: {ex}");
                throw;
            }
        }

        public async Task DisconnectAsync()
        {
            _logger.LogDebug("Disconnecting from Redis...");
            await _redis.CloseAsync();
            _logger.LogDebug("Redis disconnected successfully");
        }
    }
} 