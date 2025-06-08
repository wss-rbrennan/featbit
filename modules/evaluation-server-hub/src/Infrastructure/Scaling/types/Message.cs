using System.Text.Json.Serialization;

namespace Infrastructure.Scaling.Types
{
    public class Message
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("roomId")]
        public string? RoomId { get; set; }

        [JsonPropertyName("room")]
        public string? Room { get; set; }

        [JsonPropertyName("message")]
        public string? MessageContent { get; set; }

    }
} 