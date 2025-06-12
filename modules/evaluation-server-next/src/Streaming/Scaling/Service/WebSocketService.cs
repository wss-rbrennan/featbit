using Infrastructure.Connections;
using Streaming.Scaling.Manager;
using Infrastructure.Scaling.Types;
using Microsoft.Extensions.Logging;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Domain.Shared;

namespace Streaming.Scaling.Service
{
    public class WebSocketService : IWebSocketService
    {
        private readonly IBackplaneManager _backplaneManager;
        private readonly ISubscriptionService _subscriptionService;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Timer _subscriptionLogger;
        private readonly ILogger<WebSocketService> _logger;
        private const string FEATBIT_ELS_PATTERN = "featbit:els:*";
        private const string FEATBIT_ELS_PREFIX = "featbit:els:";

        public WebSocketService(ILogger<WebSocketService> logger,
                                ISubscriptionService subscriptionService,
                                IBackplaneManager backplaneManager)
        {
            _backplaneManager = backplaneManager;
            _subscriptionService = subscriptionService;
            _cancellationTokenSource = new CancellationTokenSource();
            _subscriptionLogger = new Timer(LogSubscriptions, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
            _logger = logger;
            // Subscribe to all featbit:els:* channels
            SubscribeToFeatbitChannels().ConfigureAwait(false);
        }

        public async Task SubscribeToFeatbitChannels()
        {
            WebSocketServiceLogger.SettingUpSubscription(_logger, FEATBIT_ELS_PATTERN);
            try
            {
                await _backplaneManager.SubscribeAsync(FEATBIT_ELS_PATTERN, async (message) =>
                {
                    WebSocketServiceLogger.ReceivedChannelMessage(_logger, FEATBIT_ELS_PATTERN, message);

                    try
                    {
                        var parsedMessage = JsonSerializer.Deserialize<Infrastructure.Scaling.Types.Message>(message);

                        if (parsedMessage?.ChannelId != null)
                        {
                            var rawContent = parsedMessage.MessageContent.GetRawText() ?? string.Empty;

                            using var document = JsonDocument.Parse(rawContent);
                            var root = document.RootElement;

                            if (!root.TryGetProperty("messageType", out var messageType))
                            {
                                WebSocketServiceLogger.FailedToParseMessageOrChannelIdIsNull(_logger, message);
                                return;
                            }

                            switch (messageType.GetString())
                            {
                                case "data-sync":
                                    // Broadcast to all websocket connections subscribed to the channel
                                    WebSocketServiceLogger.BroadcastToSubscribers(_logger, parsedMessage.ChannelId);
                                    await _subscriptionService.BroadcastToSubscribersAsync(parsedMessage.ChannelId, rawContent);
                                    break;
                                default:
                                    WebSocketServiceLogger.UnknownMessageType(_logger, messageType.GetString() ?? "unknown");
                                    break;
                            }
                        }
                        else
                        {
                            WebSocketServiceLogger.FailedToParseMessageOrChannelIdIsNull(_logger, message);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug($"Error processing Redis message: {ex}");
                    }
                });
                _logger.LogDebug($"Successfully subscribed to Redis channels matching pattern: {FEATBIT_ELS_PATTERN}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error setting up Redis subscription for pattern {FEATBIT_ELS_PATTERN}: {ex}");
                WebSocketServiceLogger.ErrorProcessingMessage(_logger, FEATBIT_ELS_PATTERN, ex);
            }
        }

        public void LogSubscriptions(object? state)
        {
            _logger.LogTrace(JsonSerializer.Serialize(_subscriptionService.GetSubscriptions()));
        }

        public async Task HandleConnectionAsync(ConnectionContext connection, CancellationToken token)
        {

            var webSocket = connection.WebSocket;

            WebSocketServiceLogger.NewWebSocketConnection(_logger);
            var id = _subscriptionService.AddSubscription(webSocket);
            _subscriptionService.AddChannelToSubscription(id, connection.Connection.EnvId.ToString());

            WebSocketServiceLogger.CreatedSubscription(_logger, id);
            
            var buffer = new byte[1024 * 4];

            try
            {
                while (webSocket.State == WebSocketState.Open)
                {
                    _logger.LogDebug($"Waiting for message from client {id}...");
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellationTokenSource.Token);
                    //_logger.LogDebug($"Received message of type: {result.MessageType}, length: {result.Count}");
                    //WebSocketServiceLogger.ReceivedMessage(_logger, result.MessageType.ToString(), result.Count);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.LogDebug($"Client {id} requested close");
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                        break;
                    }

                     var messageJson = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    _logger.LogDebug($"Received raw message from client {id}: {messageJson}");

                    try
                    {
                        //var message = JsonSerializer.Deserialize<Message>(messageJson);// This needs to handle the messages
                        if (!String.IsNullOrEmpty(messageJson))
                        {
                            await HandleMessageAsync(id, connection, messageJson);
                        }
                        else
                        {
                            throw new JsonException("Failed to deserialize message from client");
                        }
                    }
                    catch (Exception ex)
                    {
                        WebSocketServiceLogger.ErrorProcessingMessageFromClient(_logger, id, ex);
                    }
                }
            }
            catch (Exception ex)
            {
                WebSocketServiceLogger.ErrorHandlingWebSocketClient(_logger, id, ex);
            }
            finally
            {
                if (webSocket.State == WebSocketState.Open)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                }
                _subscriptionService.RemoveSubscription(id);
            }
        }

        public async Task HandleMessageAsync(string id, ConnectionContext ctx, string message)
        {
            WebSocketServiceLogger.HandlingMessageContent(_logger, JsonSerializer.Serialize(message));

            using var document = JsonDocument.Parse(message);
            var root = document.RootElement;

            // get the message type
            if (!root.TryGetProperty("messageType", out var messageType))
            {
                {
                    throw new InvalidDataException("invalid message type");
                }
            }

            var envId = ctx.Connection.EnvId.ToString();
            var channelId = FEATBIT_ELS_PREFIX + ctx.Connection.EnvId.ToString();

            switch (messageType.ToString())
            {

                case "data-sync":


                    var messageContext = CreateMessageContextTransport(ctx, JsonDocument.Parse(message).RootElement);
                    var messageContextJson = JsonSerializer.Serialize(messageContext, JsonSerializerOptions.Web);

                    var backplaneMessage = new Message
                    {
                        ChannelId = envId,
                        Type = "server",
                        ChannelName = envId,
                        MessageContent = JsonDocument.Parse(messageContextJson).RootElement
                    };

                    var serverMessageJson = JsonSerializer.Serialize<Message>(backplaneMessage, JsonSerializerOptions.Web);

                    await _backplaneManager.PublishAsync(channelId, serverMessageJson);

                    break;
                case "ping":
                    await HandlePingMessage(ctx);
                    break;
                default:
                    WebSocketServiceLogger.UnknownMessageType(_logger, messageType.ToString());
                    break;
            }
        }

        private MessageContext CreateMessageContextTransport(ConnectionContext ctx, JsonElement messageContent)
        {
            var connectionInfo = new Infrastructure.Scaling.Types.ConnectionInfo(ctx.Connection.Id, ctx.Connection.Secret);
            connectionInfo.User = ctx.Connection.User;
            var mappedRpConnections = ctx.MappedRpConnections.Select(c => new Infrastructure.Scaling.Types.ConnectionInfo(c.Id, c.Secret)).ToArray();
            var connectionContextInfo = new DefaultConnectionContextInfo(ctx.RawQuery.ToString(),
                ctx.ConnectAt, ctx.Client, connectionInfo, mappedRpConnections);
            return new MessageContext(connectionContextInfo, messageContent);
        }

        public async Task HandleSubscribeAsync(string id, string Channel)
        {
            _logger.LogDebug($"Handling subscribe request for Channel: {Channel}");
            _subscriptionService.AddChannelToSubscription(id, Channel);
            _logger.LogDebug($"Added Channel {Channel} to subscription {id}");
            WebSocketServiceLogger.AddedChannelToSubscription(_logger, Channel, id);

            if (_subscriptionService.IsFirstSubscriber(Channel))
            {
                _logger.LogDebug($"First subscriber for Channel: {Channel}, setting up Redis subscription");
                try
                {
                    await _backplaneManager.SubscribeAsync(Channel, async (message) =>
                    {
                        _logger.LogDebug($"Received message from Redis for Channel {Channel}: {message}");
                        try
                        {
                            var parsedMessage = JsonSerializer.Deserialize<Message>(message);
                            _logger.LogDebug($"Parsed message: {JsonSerializer.Serialize(parsedMessage)}");

                            if (parsedMessage?.ChannelId != null)
                            {
                                _logger.LogDebug($"Broadcasting message to Channel {parsedMessage.ChannelId}");
                                var subscribers = _subscriptionService.GetSubscriptions()
                                    .Where(s => s.Value.Channels.Contains(parsedMessage.ChannelId))
                                    .ToList();
                                _logger.LogDebug($"Found {subscribers.Count} subscribers for Channel {parsedMessage.ChannelId}");
                                _logger.LogDebug($"Subscriber IDs: {string.Join(", ", subscribers.Select(s => s.Key))}");

                                await _subscriptionService.BroadcastToSubscribersAsync(parsedMessage.ChannelId, message);
                                _logger.LogDebug($"Message broadcast complete");
                            }
                            else
                            {
                                _logger.LogDebug($"Failed to parse message or ChannelId is null: {message}");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Error processing Redis message: {ex}");
                        }
                    });
                    _logger.LogDebug($"Redis subscription for Channel {Channel} set up successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error setting up Redis subscription: {ex}");
                }
            }
            else
            {
                _logger.LogDebug($"Not first subscriber for Channel {Channel}, skipping Redis subscription");
            }
        }

        public async Task HandleUnsubscribeAsync(string id, string Channel)
        {
            _subscriptionService.RemoveChannelFromSubscription(id, Channel);
            WebSocketServiceLogger.UnsubscribingFromChannel(_logger, Channel);

            if (_subscriptionService.IsLastSubscriber(Channel))
            {
                _logger.LogDebug($"Unsubscribing from Channel: {Channel}");
                await _backplaneManager.UnsubscribeAsync(Channel);
            }
        }

        public async Task HandleDataSyncMessageAsync(ConnectionContext ctx, string messageJson)
        {
            _logger.LogDebug($"Handling data sync message: {messageJson}");
            var message = JsonSerializer.Deserialize<Message>(messageJson);
            if (message == null || string.IsNullOrEmpty(message.ChannelId))
            {
                _logger.LogError("Invalid data sync message received or ChannelId is null/empty.");
                return;
            }
            _logger.LogDebug($"Publishing data sync message to Redis for Channel: {message.ChannelId}");
            try
            {
                await _backplaneManager.PublishAsync(message.ChannelId, messageJson);
                _logger.LogDebug($"Data sync message published successfully to Redis for Channel: {message.ChannelId}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error publishing data sync message to Redis: {ex}");
            }
        }


        public async Task HandlePingMessage(ConnectionContext ctx)
        {
            var messageJson = JsonSerializer.Serialize(new
            {
                type = "pong",
                data = ""
            });

            await ctx.WebSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(messageJson)), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
        }

        // TODO: REMOVE THIS METHOD IF NOT NEEDED
        public async Task HandleSendMessageAsync(Message message)
        {
            _logger.LogDebug($"Handling send message request for Channel: {message.ChannelId}");
            var messageJson = JsonSerializer.Serialize(new
            {
                type = "SEND_MESSAGE",
                ChannelId = message.ChannelId,
                message = message.MessageContent
            });
            _logger.LogDebug($"Publishing message to Redis: {messageJson}");
            try
            {
                await _backplaneManager.PublishAsync(message.ChannelId, messageJson);
                _logger.LogDebug($"Message published to Redis successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error publishing message to Redis: {ex}");
            }
        }
    }
}