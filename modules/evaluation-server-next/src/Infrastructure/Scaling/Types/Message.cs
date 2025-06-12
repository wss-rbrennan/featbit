using System.Text.Json;
using System.Text.Json.Serialization;

namespace Infrastructure.Scaling.Types
{
    public class Message
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("channelId")]
        public string? ChannelId { get; set; }

        [JsonPropertyName("channelName")]
        public string? ChannelName { get; set; }

        [JsonPropertyName("message")]
        public JsonElement MessageContent { get; set; }

    }
}