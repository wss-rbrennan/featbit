//using Microsoft.Extensions.Logging;

//namespace Infrastructure.Scaling.Service
//{
//    public static partial class WebSocketServiceLogger
//    {
//        [LoggerMessage(
//            EventId = 1,
//            Level = LogLevel.Information,
//            Message = "Setting up subscription for pattern: {Pattern}")]
//        public static partial void SettingUpSubscription(this ILogger logger, string pattern);

//        [LoggerMessage(
//            EventId = 2,
//            Level = LogLevel.Information,
//            Message = "Received message from channel {Pattern}: {Message}")]
//        public static partial void ReceivedChannelMessage(this ILogger logger, string pattern, string message);

//        [LoggerMessage(
//            EventId = 3,
//            Level = LogLevel.Information,
//            Message = "Broadcasting to channel: {ChannelId}")]
//        public static partial void BroadcastToSubscribers(this ILogger logger, string channelId);

//        [LoggerMessage(
//            EventId = 4,
//            Level = LogLevel.Warning,
//            Message = "Failed to parse message or ChannelId is null: {Message}")]
//        public static partial void FailedToParseMessageOrChannelIdIsNull(this ILogger logger, string message);

//        [LoggerMessage(
//            EventId = 5,
//            Level = LogLevel.Error,
//            Message = "Error processing message for pattern {Pattern}: {Error}")]
//        public static partial void ErrorProcessingMessage(this ILogger logger, string pattern, Exception error);

//        [LoggerMessage(
//            EventId = 6,
//            Level = LogLevel.Information,
//            Message = "New WebSocket connection established")]
//        public static partial void NewWebSocketConnection(this ILogger logger);

//        [LoggerMessage(
//            EventId = 7,
//            Level = LogLevel.Information,
//            Message = "Created subscription with ID: {Id}")]
//        public static partial void CreatedSubscription(this ILogger logger, string id);

//        [LoggerMessage(
//            EventId = 8,
//            Level = LogLevel.Information,
//            Message = "Received message of type {MessageType} with length {Length}")]
//        public static partial void ReceivedMessage(this ILogger logger, string messageType, int length);

//        [LoggerMessage(
//            EventId = 9,
//            Level = LogLevel.Error,
//            Message = "Error processing message from client {Id}: {Error}")]
//        public static partial void ErrorProcessingMessageFromClient(this ILogger logger, string id, Exception error);

//        [LoggerMessage(
//            EventId = 10,
//            Level = LogLevel.Error,
//            Message = "Error handling WebSocket client {Id}: {Error}")]
//        public static partial void ErrorHandlingWebSocketClient(this ILogger logger, string id, Exception error);

//        [LoggerMessage(
//            EventId = 11,
//            Level = LogLevel.Information,
//            Message = "Handling message content: {Content}")]
//        public static partial void HandlingMessageContent(this ILogger logger, string content);

//        [LoggerMessage(
//            EventId = 12,
//            Level = LogLevel.Warning,
//            Message = "ChannelId is null")]
//        public static partial void NullChannelId(this ILogger logger);

//        [LoggerMessage(
//            EventId = 13,
//            Level = LogLevel.Warning,
//            Message = "Unknown message type: {Type}")]
//        public static partial void UnknownMessageType(this ILogger logger, string type);

//        [LoggerMessage(
//            EventId = 14,
//            Level = LogLevel.Information,
//            Message = "Added channel {Channel} to subscription {Id}")]
//        public static partial void AddedChannelToSubscription(this ILogger logger, string channel, string id);

//        [LoggerMessage(
//            EventId = 15,
//            Level = LogLevel.Information,
//            Message = "Unsubscribing from channel: {Channel}")]
//        public static partial void UnsubscribingFromChannel(this ILogger logger, string channel);
//    }
//} 