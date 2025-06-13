using Infrastructure.Connections;
using Streaming.Scaling.Manager;
using Infrastructure.Scaling.Types;
using Microsoft.Extensions.Logging;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Domain.Shared;
using Microsoft.Extensions.Hosting;
using Infrastructure.Scaling.Service;

namespace Streaming.Scaling.Service
{
    public class WebSocketService : BackgroundService, IWebSocketService
    {
        private readonly IBackplaneManager _backplaneManager;
        private readonly ISubscriptionService _subscriptionService;
        private readonly IMessageFactory _messageFactory;
        private readonly IServiceIdentityProvider _serviceIdentityProvider;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Timer _subscriptionLogger;
        private readonly ILogger<WebSocketService> _logger;
        private const string FEATBIT_ELS_PREFIX = "featbit:els:edge:";
        private const string FEATBIT_ELS_BACKPLANE_PREFIX = "featbit:els:backplane:";
        private readonly Dictionary<string, bool> _subscribedChannels = new();

        public WebSocketService(ILogger<WebSocketService> logger,
                                ISubscriptionService subscriptionService,
                                IBackplaneManager backplaneManager,
                                IMessageFactory messageFactory,
                                IServiceIdentityProvider serviceIdentityProvider)
        {
            _backplaneManager = backplaneManager;
            _subscriptionService = subscriptionService;
            _messageFactory = messageFactory;
            _serviceIdentityProvider = serviceIdentityProvider;
            _cancellationTokenSource = new CancellationTokenSource();
            _subscriptionLogger = new Timer(LogSubscriptions, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
            _logger = logger;
            _logger.LogInformation("WebSocketService initialized with service ID: {ServiceId}", _serviceIdentityProvider.ServiceId);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Keep the service running
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("WebSocketService shutdown initiated - disconnecting all WebSocket connections");
            
            // Cancel the internal cancellation token to stop any ongoing operations
            _cancellationTokenSource.Cancel();
            
            // Disconnect all WebSocket connections gracefully
            try
            {
                await _subscriptionService.DisconnectAllAsync(
                    WebSocketCloseStatus.NormalClosure, 
                    "Server is shutting down"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while disconnecting WebSocket connections during shutdown");
            }
            
            // Dispose the subscription logger timer
            _subscriptionLogger.Dispose();
            
            // Call base implementation
            await base.StopAsync(cancellationToken);
            
            _logger.LogInformation("WebSocketService shutdown completed");
        }

        // Interface requirement - but we'll use our new subscription approach
        public async Task SubscribeToFeatbitChannels()
        {
            _logger.LogInformation("SubscribeToFeatbitChannels called - using per-connection subscription model");
            // No-op as we now subscribe per connection
        }

        private async Task SubscribeToBackplaneChannel(string envId)
        {
            var backplaneChannel = $"{FEATBIT_ELS_BACKPLANE_PREFIX}{envId}";
            
            if (_subscribedChannels.ContainsKey(backplaneChannel))
            {
                _logger.LogInformation("Already subscribed to backplane channel: {BackplaneChannel}", backplaneChannel);
                return;
            }

            _logger.LogInformation("Setting up subscription for backplane channel: {BackplaneChannel}", backplaneChannel);
            try
            {
                var subscriptionReady = new TaskCompletionSource<bool>();
                
                // Subscribe to backplane channel only - Edge service listens to backplane, publishes to edge
                await _backplaneManager.SubscribeAsync(backplaneChannel, async (message) =>
                {
                    _logger.LogInformation("[SUBSCRIPTION CALLBACK] Received message from Redis backplane channel: {Channel}, Message: {Message}", backplaneChannel, message);
                    await HandleRedisMessage(message, backplaneChannel, subscriptionReady);
                });

                _logger.LogInformation("Successfully set up subscription callback for backplane channel: {BackplaneChannel}", backplaneChannel);
                _subscribedChannels[backplaneChannel] = true;
                _logger.LogInformation("Marked backplane channel as subscribed: {BackplaneChannel}", backplaneChannel);

                // Send a test message to verify the subscription
                var testMessage = _messageFactory.CreateMessage(
                    type: "server",
                    channelId: envId,
                    channelName: envId,
                    messageContent: JsonDocument.Parse(JsonSerializer.Serialize(new
                    {
                        messageType = "test",
                        data = new { timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }
                    })).RootElement,
                    senderId: _serviceIdentityProvider.ServiceId,
                    serviceType: ServiceTypes.Edge
                );

                _logger.LogInformation("Sending test message to verify subscription on channel: {Channel}", backplaneChannel);
                await _backplaneManager.PublishAsync(backplaneChannel, JsonSerializer.Serialize(testMessage));

                // Wait for the test message to be received (with timeout)
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try
                {
                    await subscriptionReady.Task.WaitAsync(cts.Token);
                    _logger.LogInformation("Subscription verified for backplane channel: {BackplaneChannel}", backplaneChannel);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Timeout waiting for subscription verification on backplane channel: {BackplaneChannel}", backplaneChannel);
                    throw new TimeoutException($"Subscription verification timed out for backplane channel: {backplaneChannel}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting up subscription for backplane channel: {BackplaneChannel}", backplaneChannel);
                throw;
            }
        }

        private async Task HandleRedisMessage(string message, string channel, TaskCompletionSource<bool> subscriptionReady)
        {
            try
            {
                var parsedMessage = JsonSerializer.Deserialize<Infrastructure.Scaling.Types.Message>(message);
                _logger.LogInformation("[SUBSCRIPTION CALLBACK] Successfully parsed message: {ParsedMessage}", JsonSerializer.Serialize(parsedMessage));

                if (parsedMessage?.ChannelId != null)
                {
                    // Log correlation information if available
                    if (!string.IsNullOrEmpty(parsedMessage.SenderId) || !string.IsNullOrEmpty(parsedMessage.CorrelationId))
                    {
                        WebSocketServiceLogger.ProcessingMessageWithCorrelation(_logger, parsedMessage.ServiceType, parsedMessage.SenderId, parsedMessage.CorrelationId);
                    }

                    var rawContent = parsedMessage.MessageContent.GetRawText() ?? string.Empty;
                    _logger.LogInformation("[SUBSCRIPTION CALLBACK] Raw message content: {RawContent}", rawContent);

                    using var document = JsonDocument.Parse(rawContent);
                    var root = document.RootElement;

                    // First try to get messageType from the root
                    if (root.TryGetProperty("messageType", out var messageType))
                    {
                        var messageTypeStr = messageType.GetString();
                        _logger.LogInformation("[SUBSCRIPTION CALLBACK] Found messageType in root: {MessageType}", messageTypeStr);
                        
                        // If this is a test message, mark the subscription as ready
                        if (messageTypeStr == "test" && !subscriptionReady.Task.IsCompleted)
                        {
                            _logger.LogInformation("[SUBSCRIPTION CALLBACK] Received test message, marking subscription as ready");
                            subscriptionReady.TrySetResult(true);
                            return;
                        }
                        
                        await HandleMessageType(messageTypeStr, parsedMessage.ChannelId, rawContent);
                    }
                    // Then try to get it from the nested message structure
                    else if (root.TryGetProperty("message", out var messageElement))
                    {
                        _logger.LogInformation("[SUBSCRIPTION CALLBACK] Found message element in root, checking for nested messageType");
                        if (messageElement.TryGetProperty("messageType", out var nestedMessageType))
                        {
                            var messageTypeStr = nestedMessageType.GetString();
                            _logger.LogInformation("[SUBSCRIPTION CALLBACK] Found messageType in nested message: {MessageType}", messageTypeStr);
                            
                            // If this is a test message, mark the subscription as ready
                            if (messageTypeStr == "test" && !subscriptionReady.Task.IsCompleted)
                            {
                                _logger.LogInformation("[SUBSCRIPTION CALLBACK] Received test message, marking subscription as ready");
                                subscriptionReady.TrySetResult(true);
                                return;
                            }
                            
                            await HandleMessageType(messageTypeStr, parsedMessage.ChannelId, rawContent);
                        }
                        else if (messageElement.TryGetProperty("data", out var dataElement) && 
                                dataElement.TryGetProperty("messageType", out var dataMessageType))
                        {
                            var messageTypeStr = dataMessageType.GetString();
                            _logger.LogInformation("[SUBSCRIPTION CALLBACK] Found messageType in data element: {MessageType}", messageTypeStr);
                            
                            // If this is a test message, mark the subscription as ready
                            if (messageTypeStr == "test" && !subscriptionReady.Task.IsCompleted)
                            {
                                _logger.LogInformation("[SUBSCRIPTION CALLBACK] Received test message, marking subscription as ready");
                                subscriptionReady.TrySetResult(true);
                                return;
                            }
                            
                            await HandleMessageType(messageTypeStr, parsedMessage.ChannelId, rawContent);
                        }
                        else
                        {
                            _logger.LogWarning("[SUBSCRIPTION CALLBACK] Could not find messageType in nested message structure. Message: {Message}", message);
                            WebSocketServiceLogger.FailedToParseMessageOrChannelIdIsNull(_logger, message);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("[SUBSCRIPTION CALLBACK] Message does not contain messageType property or message element. Full message: {Message}", message);
                        WebSocketServiceLogger.FailedToParseMessageOrChannelIdIsNull(_logger, message);
                    }
                }
                else
                {
                    _logger.LogWarning("[SUBSCRIPTION CALLBACK] Message or ChannelId is null. Full message: {Message}", message);
                    WebSocketServiceLogger.FailedToParseMessageOrChannelIdIsNull(_logger, message);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "[SUBSCRIPTION CALLBACK] Failed to parse message as JSON: {Message}", message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SUBSCRIPTION CALLBACK] Error processing Redis message: {Message}", message);
            }
        }

        public async Task HandleConnectionAsync(ConnectionContext connection, CancellationToken token)
        {
            _logger.LogInformation("Handling new WebSocket connection");

            var webSocket = connection.WebSocket;
            var envId = connection.Connection.EnvId.ToString();

            WebSocketServiceLogger.NewWebSocketConnection(_logger);
            var id = _subscriptionService.AddSubscription(webSocket);
            _logger.LogInformation("Created new subscription with ID: {Id} for environment: {EnvId}", id, envId);
            
            // Subscribe to the specific backplane channel for this environment
            await SubscribeToBackplaneChannel(envId);
            
            // Add a small delay to ensure subscription is fully ready
            await Task.Delay(100);
            
            _subscriptionService.AddChannelToSubscription(id, envId);
            _logger.LogInformation("Added channel {Channel} to subscription {Id}", envId, id);

            WebSocketServiceLogger.CreatedSubscription(_logger, id);
            
            var buffer = new byte[1024 * 4];

            try
            {
                while (webSocket.State == WebSocketState.Open)
                {
                    _logger.LogDebug("Waiting for message from client {Id}...", id);
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellationTokenSource.Token);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.LogInformation("Client {Id} requested close", id);
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                        break;
                    }

                    var messageJson = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    _logger.LogInformation("Received message from client {Id}: {Message}", id, messageJson);

                    try
                    {
                        if (!String.IsNullOrEmpty(messageJson))
                        {
                            await HandleMessageAsync(id, connection, messageJson);
                        }
                        else
                        {
                            _logger.LogWarning("Received empty message from client {Id}", id);
                            throw new JsonException("Failed to deserialize message from client");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing message from client {Id}: {Message}", id, messageJson);
                        WebSocketServiceLogger.ErrorProcessingMessageFromClient(_logger, id, ex);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling WebSocket client {Id}", id);
                WebSocketServiceLogger.ErrorHandlingWebSocketClient(_logger, id, ex);
            }
            finally
            {
                if (webSocket.State == WebSocketState.Open)
                {
                    _logger.LogInformation("Closing WebSocket connection for client {Id}", id);
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                }
                _logger.LogInformation("Removing subscription {Id}", id);
                _subscriptionService.RemoveSubscription(id);
            }
        }

        private async Task HandleMessageType(string? messageType, string channelId, string rawContent)
        {
            if (string.IsNullOrEmpty(messageType))
            {
                _logger.LogWarning("Message type is null or empty");
                return;
            }

            _logger.LogInformation("Processing message of type: {MessageType} for channel: {ChannelId}", messageType, channelId);

            switch (messageType)
            {
                case "data-sync":
                    _logger.LogInformation("Broadcasting data-sync message to channel: {ChannelId}", channelId);
                    WebSocketServiceLogger.BroadcastToSubscribers(_logger, channelId);
                    await _subscriptionService.BroadcastToSubscribersAsync(channelId, rawContent);
                    _logger.LogInformation("Data-sync message broadcast complete");
                    break;
                case "test":
                    _logger.LogInformation("Received test message on channel: {ChannelId}", channelId);
                    break;
                default:
                    _logger.LogWarning("Unknown message type: {MessageType}", messageType);
                    WebSocketServiceLogger.UnknownMessageType(_logger, messageType);
                    break;
            }
        }

        public void LogSubscriptions(object? state)
        {
            _logger.LogTrace(JsonSerializer.Serialize(_subscriptionService.GetSubscriptions()));
        }

        public async Task HandleMessageAsync(string id, ConnectionContext ctx, string message)
        {
            _logger.LogInformation("Handling message from client {Id}: {Message}", id, message);
            WebSocketServiceLogger.HandlingMessageContent(_logger, JsonSerializer.Serialize(message));

            using var document = JsonDocument.Parse(message);
            var root = document.RootElement;

            if (!root.TryGetProperty("messageType", out var messageType))
            {
                _logger.LogWarning("Invalid message format - missing messageType. Message: {Message}", message);
                throw new InvalidDataException("invalid message type");
            }

            var envId = ctx.Connection.EnvId.ToString();
            var channelId = FEATBIT_ELS_PREFIX + envId;
            _logger.LogInformation("Processing message for environment {EnvId} on channel {ChannelId}", envId, channelId);

            switch (messageType.ToString())
            {
                case "data-sync":
                    _logger.LogInformation("Processing data-sync message for environment {EnvId}", envId);
                    WebSocketServiceLogger.WebSocketSendingMessage(_logger, id, channelId);
                    
                    var messageContext = CreateMessageContextTransport(ctx, JsonDocument.Parse(message).RootElement);
                    var messageContextJson = JsonSerializer.Serialize(messageContext, JsonSerializerOptions.Web);
                    _logger.LogInformation("Created message context: {MessageContext}", messageContextJson);

                    var backplaneMessage = _messageFactory.CreateMessage(
                        type: "server",
                        channelId: envId,
                        channelName: envId,
                        messageContent: JsonDocument.Parse(messageContextJson).RootElement,
                        senderId: id, // Use the WebSocket subscription ID as sender ID
                        serviceType: ServiceTypes.Edge
                    );

                    WebSocketServiceLogger.MessageCreatedWithCorrelation(_logger, backplaneMessage.ServiceType, backplaneMessage.SenderId, backplaneMessage.CorrelationId);

                    var serverMessageJson = JsonSerializer.Serialize<Message>(backplaneMessage, JsonSerializerOptions.Web);
                    _logger.LogInformation("Publishing to backplane channel {ChannelId}: {Message} with SenderId: {SenderId} and CorrelationId: {CorrelationId}", 
                        channelId, serverMessageJson, backplaneMessage.SenderId, backplaneMessage.CorrelationId);

                    await _backplaneManager.PublishAsync(channelId, serverMessageJson);
                    _logger.LogInformation("Successfully published message to backplane");
                    break;

                case "ping":
                    _logger.LogInformation("Handling ping message from client {Id}", id);
                    await HandlePingMessage(ctx);
                    break;

                default:
                    _logger.LogWarning("Unknown message type: {MessageType}", messageType.ToString());
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

    }
}