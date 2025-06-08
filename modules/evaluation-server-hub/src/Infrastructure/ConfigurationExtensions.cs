using Infrastructure.MQ;
using Microsoft.Extensions.Configuration;
using Npgsql;
using StackExchange.Redis;
//using Infrastructure.Backplane;

namespace Infrastructure;

public static class ConfigurationExtensions
{

    //public static string GetBackplaneProvider(this IConfiguration configuration)
    //{
    //    var provider = configuration.GetValue(BackplaneProvider.SectionName, BackplaneProvider.Redis)!;
    //    return provider;
    //}

    public static string GetMqProvider(this IConfiguration configuration)
    {
        var provider = configuration.GetValue(MqProvider.SectionName, MqProvider.Redis)!;
        return provider;
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