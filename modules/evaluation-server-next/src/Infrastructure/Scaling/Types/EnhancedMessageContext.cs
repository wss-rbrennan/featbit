using Infrastructure.Scaling.Handlers;
using System.Text.Json;

namespace Infrastructure.Scaling.Types
{
    /// <summary>
    /// Enhanced message context that includes both the original message context and the full message metadata
    /// </summary>
    public class EnhancedMessageContext : MessageContext
    {
        /// <summary>
        /// The full message with correlation metadata
        /// </summary>
        public Message FullMessage { get; }

        public EnhancedMessageContext(MessageContext originalContext, Message fullMessage) 
            : base(originalContext.Connection, originalContext.Data)
        {
            FullMessage = fullMessage;
        }

        /// <summary>
        /// Gets the sender ID from the full message
        /// </summary>
        public string? SenderId => FullMessage.SenderId;

        /// <summary>
        /// Gets the correlation ID from the full message
        /// </summary>
        public string? CorrelationId => FullMessage.CorrelationId;

        /// <summary>
        /// Gets the service type from the full message
        /// </summary>
        public string? ServiceType => FullMessage.ServiceType;
    }
} 