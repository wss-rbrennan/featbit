using Domain.Shared;
using Infrastructure.Connections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;
using Infrastructure.Scaling.Utils;

namespace Infrastructure.Scaling.Types;

[JsonConverter(typeof(DefaultConnectionContextInfoJsonConverter))]
public class DefaultConnectionContextInfo
{
    public string? RawQuery { get; }
    public string Type { get; }
    public string Version { get; }
    public string Token { get; }
    public Client? Client { get; protected set; }
    public ConnectionInfo Connection { get; protected set; }
    public ConnectionInfo[] MappedRpConnections { get; protected set; }
    public long ConnectAt { get; }
    public long ClosedAt { get; protected set; }

    public DefaultConnectionContextInfo(string rawQuery, long connectedAt, Client client, ConnectionInfo connectionInfo, ConnectionInfo[] mappedRpConnecitons)
    {
        RawQuery = rawQuery;

        var query = new QueryCollection(rawQuery);

        // Fix for CS8602: Ensure the key exists and the value is not null before accessing the value
        Type = query.ContainsKey("type") && query["type"] != null ? query["type"].ToString() : string.Empty;
        Version = query.ContainsKey("version") && query["version"] != null ? query["version"].ToString() : string.Empty;
        Token = query.ContainsKey("token") && query["token"] != null ? query["token"].ToString() : string.Empty;

        ConnectAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        Client = client; // Fix: Correctly assign the passed client parameter
        Connection = connectionInfo!;
        MappedRpConnections = mappedRpConnecitons;
    }
}