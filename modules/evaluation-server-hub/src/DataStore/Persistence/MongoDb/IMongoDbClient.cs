using MongoDB.Driver;

namespace DataStore.Persistence.MongoDb;

public interface IMongoDbClient
{
    Task<bool> IsHealthyAsync();

    IMongoDatabase Database { get; }
}