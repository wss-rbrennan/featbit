using StackExchange.Redis;

namespace DataStore.Caches.Redis;

public interface IRedisClient
{
    IConnectionMultiplexer Connection { get; }

    Task<bool> IsHealthyAsync();

    IDatabase GetDatabase();

    ISubscriber GetSubscriber();
}