using Infrastructure.Scaling.Types;
using System.Text.Json;
using System.Text.Json.Serialization;
using Infrastructure.Connections;

namespace Infrastructure.Scaling.Utils
{
    public class DefaultConnectionContextInfoJsonConverter : JsonConverter<DefaultConnectionContextInfo>
    {
        public override DefaultConnectionContextInfo? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Expected StartObject token.");
            }

            string? rawQuery = null;
            long connectedAt = 0;
            Client? client = null;
            ConnectionInfo? connectionInfo = null;
            ConnectionInfo[]? mappedRpConnections = null;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    break;
                }

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    string propertyName = reader.GetString()!;
                    reader.Read();

                    switch (propertyName)
                    {
                        case "rawQuery":
                            rawQuery = reader.GetString();
                            break;
                        case "connectAt":
                            connectedAt = reader.GetInt64();
                            break;
                        case "client":
                            client = JsonSerializer.Deserialize<Client>(ref reader, options);
                            break;
                        case "connection":
                            connectionInfo = JsonSerializer.Deserialize<ConnectionInfo>(ref reader, options);
                            break;
                        case "mappedRpConnections":
                            mappedRpConnections = JsonSerializer.Deserialize<ConnectionInfo[]>(ref reader, options);
                            break;
                        default:
                            reader.Skip();
                            break;
                    }
                }
            }

            if (rawQuery == null || client == null || connectionInfo == null || mappedRpConnections == null)
            {
                throw new JsonException("Missing required properties.");
            }

            return new DefaultConnectionContextInfo(rawQuery, connectedAt, client, connectionInfo, mappedRpConnections);
        }

        public override void Write(Utf8JsonWriter writer, DefaultConnectionContextInfo value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WriteString("rawQuery", value.RawQuery);
            writer.WriteNumber("connectAt", value.ConnectAt);
            writer.WritePropertyName("client");
            JsonSerializer.Serialize(writer, value.Client, options);
            writer.WritePropertyName("connection");
            JsonSerializer.Serialize(writer, value.Connection, options);
            writer.WritePropertyName("mappedRpConnections");
            JsonSerializer.Serialize(writer, value.MappedRpConnections, options);

            writer.WriteEndObject();
        }
    }
}