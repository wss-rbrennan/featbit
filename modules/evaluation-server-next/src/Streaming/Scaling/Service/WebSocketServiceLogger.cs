using Microsoft.Extensions.Logging;

namespace Streaming.Scaling.Service
{
    public static partial class WebSocketServiceLogger
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Debug,
            Message = "Setting up subscription for pattern: {Pattern}")]
        public static partial void SettingUpSubscription(this ILogger logger, string pattern);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Debug,
            Message = "Received message from channel {Pattern}: {Message}")]
        public static partial void ReceivedChannelMessage(this ILogger logger, string pattern, string message);

        [LoggerMessage(
            EventId = 3,
            Level = LogLevel.Debug,
            Message = "Broadcasting to channel: {ChannelId}")]
        public static partial void BroadcastToSubscribers(this ILogger logger, string channelId);

        [LoggerMessage(
            EventId = 4,
            Level = LogLevel.Warning,
            Message = "Failed to parse message or ChannelId is null: {Message}")]
        public static partial void FailedToParseMessageOrChannelIdIsNull(this ILogger logger, string message);

        [LoggerMessage(
            EventId = 5,
            Level = LogLevel.Error,
            Message = "Error processing message for pattern {Pattern}:")]
        public static partial void ErrorProcessingMessage(this ILogger logger, string pattern, Exception error);

        [LoggerMessage(
            EventId = 6,
            Level = LogLevel.Debug,
            Message = "New WebSocket connection established")]
        public static partial void NewWebSocketConnection(this ILogger logger);

        [LoggerMessage(
            EventId = 7,
            Level = LogLevel.Debug,
            Message = "Created subscription with ID: {Id}")]
        public static partial void CreatedSubscription(this ILogger logger, string id);

        [LoggerMessage(
            EventId = 8,
            Level = LogLevel.Debug,
            Message = "Received message of type {MessageType} with length {Length}")]
        public static partial void ReceivedMessage(this ILogger logger, string messageType, int length);

        [LoggerMessage(
            EventId = 9,
            Level = LogLevel.Error,
            Message = "Error processing message from client {Id}")]
        public static partial void ErrorProcessingMessageFromClient(this ILogger logger, string id, Exception error);

        [LoggerMessage(
            EventId = 10,
            Level = LogLevel.Error,
            Message = "Error handling WebSocket client {Id}")]
        public static partial void ErrorHandlingWebSocketClient(this ILogger logger, string id, Exception error);

        [LoggerMessage(
            EventId = 11,
            Level = LogLevel.Debug,
            Message = "Handling message content: {Content}")]
        public static partial void HandlingMessageContent(this ILogger logger, string content);

        [LoggerMessage(
            EventId = 12,
            Level = LogLevel.Warning,
            Message = "ChannelId is null")]
        public static partial void NullChannelId(this ILogger logger);

        [LoggerMessage(
            EventId = 13,
            Level = LogLevel.Warning,
            Message = "Unknown message type: {Type}")]
        public static partial void UnknownMessageType(this ILogger logger, string type);

        [LoggerMessage(
            EventId = 14,
            Level = LogLevel.Debug,
            Message = "Added channel {Channel} to subscription {Id}")]
        public static partial void AddedChannelToSubscription(this ILogger logger, string channel, string id);

        [LoggerMessage(
            EventId = 15,
            Level = LogLevel.Debug,
            Message = "Unsubscribing from channel: {Channel}")]
        public static partial void UnsubscribingFromChannel(this ILogger logger, string channel);

        [LoggerMessage(
            EventId = 16,
            Level = LogLevel.Information,
            Message = "WebSocket shutdown initiated - disconnecting {ConnectionCount} connections")]
        public static partial void WebSocketShutdownInitiated(this ILogger logger, int connectionCount);

        [LoggerMessage(
            EventId = 17,
            Level = LogLevel.Information,
            Message = "WebSocket shutdown completed successfully")]
        public static partial void WebSocketShutdownCompleted(this ILogger logger);

        [LoggerMessage(
            EventId = 18,
            Level = LogLevel.Error,
            Message = "Error during WebSocket shutdown")]
        public static partial void WebSocketShutdownError(this ILogger logger, Exception exception);

        [LoggerMessage(
            EventId = 19,
            Level = LogLevel.Debug,
            Message = "Closing WebSocket connection {SubscriptionId} due to shutdown")]
        public static partial void ClosingWebSocketConnection(this ILogger logger, string subscriptionId);

        [LoggerMessage(
            EventId = 20,
            Level = LogLevel.Debug,
            Message = "Message created with ServiceType: {ServiceType}, SenderId: {SenderId} and CorrelationId: {CorrelationId}")]
        public static partial void MessageCreatedWithCorrelation(this ILogger logger, string? serviceType, string? senderId, string? correlationId);

        [LoggerMessage(
            EventId = 21,
            Level = LogLevel.Debug,
            Message = "Processing message from ServiceType: {ServiceType}, SenderId: {SenderId} with CorrelationId: {CorrelationId}")]
        public static partial void ProcessingMessageWithCorrelation(this ILogger logger, string? serviceType, string? senderId, string? correlationId);

        [LoggerMessage(
            EventId = 22,
            Level = LogLevel.Debug,
            Message = "WebSocket subscription {SubscriptionId} sending message to channel {ChannelId}")]
        public static partial void WebSocketSendingMessage(this ILogger logger, string subscriptionId, string channelId);
    }
}