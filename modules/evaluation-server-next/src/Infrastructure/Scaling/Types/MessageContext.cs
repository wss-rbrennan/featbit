using System.Text.Json;

namespace Infrastructure.Scaling.Types;

public class MessageContext
{
    public DefaultConnectionContextInfo Connection { get; }

    public JsonElement Data { get; set; }

    public MessageContext(DefaultConnectionContextInfo connection, JsonElement data)
    {
        Connection = connection;
        Data = data;

    }
}