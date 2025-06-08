using DataStore.Caches;
using DataStore.Persistence;
using Microsoft.Extensions.Configuration;
using Npgsql;
using StackExchange.Redis;

namespace DataStore;

public static class ConfigurationExtensions
{

    public static DbProvider GetDbProvider(this IConfiguration configuration)
    {
        var name = configuration.GetValue(DbProvider.SectionName, DbProvider.MongoDb)!;
        var connectionString = configuration.GetSection(name).GetValue("ConnectionString", string.Empty)!;

        return new DbProvider
        {
            Name = name,
            ConnectionString = connectionString
        };
    }

    public static string GetCacheProvider(this IConfiguration configuration)
    {
        var provider = configuration.GetValue(CacheProvider.SectionName, CacheProvider.Redis)!;
        return provider;
    }

    public static string GetRedisConnectionString(this IConfiguration configuration)
    {
        var connectionString = configuration["Redis:ConnectionString"];
        var options = ConfigurationOptions.Parse(connectionString!);

        // if we specified a password in the configuration, use it
        var password = configuration["Redis:Password"];
        if (!string.IsNullOrWhiteSpace(password))
        {
            options.Password = password;
        }

        return options.ToString(includePassword: true);
    }

    public static string GetPostgresConnectionString(this IConfiguration configuration)
    {
        var connectionString = configuration["Postgres:ConnectionString"];
        var builder = new NpgsqlConnectionStringBuilder(connectionString!);

        // if we specified a password in the configuration, use it
        var password = configuration["Postgres:Password"];
        if (!string.IsNullOrWhiteSpace(password))
        {
            builder.Password = password;
        }

        return builder.ToString();
    }
}