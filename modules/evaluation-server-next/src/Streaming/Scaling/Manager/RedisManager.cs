using Streaming.Scaling.Manager;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System;
using System.Threading.Tasks;

namespace Streaming.Scaling.Manager
{
    public class RedisManager : IBackplaneManager
    {
        private static RedisManager? _instance;
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
            var options = ConfigurationOptions.Parse(connectionString);
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
            _logger.LogDebug($"Publishing message to Redis channel '{channel}': {message}");
            try
            {
                if (!_redis.IsConnected)
                {
                    _logger.LogDebug("Redis not connected, attempting to reconnect...");
                    await ConnectAsync();
                }
                var subscribers = await _subscriber.PublishAsync(channel, message);
                _logger.LogDebug($"Message published successfully. Number of subscribers: {subscribers}");
                return subscribers;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error publishing message to Redis: {ex}");
                throw;
            }
        }

        public async Task SubscribeAsync(string channel, Action<string> callback)
        {
            _logger.LogDebug($"Subscribing to Redis channel: {channel}");
            try
            {
                if (!_redis.IsConnected)
                {
                    _logger.LogDebug("Redis not connected, attempting to reconnect...");
                    await ConnectAsync();
                }
                await _subscriber.SubscribeAsync(channel, (_, value) =>
                {
                    _logger.LogDebug($"Received message from Redis channel '{channel}': {value}");
                    callback(value);
                });
                _logger.LogDebug($"Successfully subscribed to Redis channel: {channel}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error subscribing to Redis channel: {ex}");
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
                await _subscriber.UnsubscribeAsync(channel);
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