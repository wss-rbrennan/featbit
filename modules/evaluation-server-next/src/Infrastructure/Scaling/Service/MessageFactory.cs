using Infrastructure.Scaling.Types;
using System.Text.Json;

namespace Infrastructure.Scaling.Service
{
    /// <summary>
    /// Factory for creating messages with proper correlation and sender IDs
    /// </summary>
    public interface IMessageFactory
    {
        /// <summary>
        /// Creates a new message with a new correlation ID
        /// </summary>
        Message CreateMessage(string type, string? channelId, string? channelName, JsonElement messageContent, string senderId, string serviceType);

        /// <summary>
        /// Creates a response message preserving the correlation ID from the original message
        /// </summary>
        Message CreateResponseMessage(string type, string? channelId, string? channelName, JsonElement messageContent, string senderId, string serviceType, string correlationId);

        /// <summary>
        /// Generates a new correlation ID
        /// </summary>
        string GenerateCorrelationId();
    }

    /// <summary>
    /// Implementation of message factory
    /// </summary>
    public class MessageFactory : IMessageFactory
    {
        public Message CreateMessage(string type, string? channelId, string? channelName, JsonElement messageContent, string senderId, string serviceType)
        {
            return new Message
            {
                Type = type,
                ChannelId = channelId,
                ChannelName = channelName,
                MessageContent = messageContent,
                SenderId = senderId,
                CorrelationId = GenerateCorrelationId(),
                ServiceType = serviceType
            };
        }

        public Message CreateResponseMessage(string type, string? channelId, string? channelName, JsonElement messageContent, string senderId, string serviceType, string correlationId)
        {
            return new Message
            {
                Type = type,
                ChannelId = channelId,
                ChannelName = channelName,
                MessageContent = messageContent,
                SenderId = senderId,
                CorrelationId = correlationId,
                ServiceType = serviceType
            };
        }

        public string GenerateCorrelationId()
        {
            return Guid.NewGuid().ToString();
        }
    }
} 