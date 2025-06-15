using Domain.Shared;
using Infrastructure.Connections;
using Infrastructure.Scaling.Service;
using Infrastructure.Scaling.Types;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Streaming.Scaling.Manager;
using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using System.Diagnostics;

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
        private readonly IConfiguration _configuration;
        
        // Performance configurations
        private readonly int _maxConnections;
        private readonly int _messageBufferSize;
        private readonly int _connectionTimeoutMs;
        private readonly int _messageQueueLimit;
        private readonly bool _enableThrottling;
        
        // Connection tracking
        private readonly SemaphoreSlim _connectionSemaphore;
        private readonly ConcurrentDictionary<string, DateTime> _connectionTimestamps = new();
        
        // Correlation tracking: correlationId -> connectionId
        private readonly ConcurrentDictionary<string, string> _correlationToConnection = new();
        private readonly ConcurrentDictionary<string, DateTime> _correlationTimestamps = new();
        private readonly ConcurrentDictionary<string, int> _correlationDeliveryCount = new();
        
        // Message deduplication for Redis pub/sub duplicates
        private readonly ConcurrentDictionary<string, DateTime> _processedMessages = new();
        private readonly Timer _messageCleanupTimer;
        
        private const string FEATBIT_ELS_PREFIX = "featbit:els:edge:";
        private const string FEATBIT_ELS_BACKPLANE_PREFIX = "featbit:els:backplane:";
        private readonly ConcurrentDictionary<string, bool> _subscribedChannels = new();

        public WebSocketService(ILogger<WebSocketService> logger,
                                ISubscriptionService subscriptionService,
                                IBackplaneManager backplaneManager,
                                IMessageFactory messageFactory,
                                IServiceIdentityProvider serviceIdentityProvider,
                                IConfiguration configuration)
        {
            _backplaneManager = backplaneManager;
            _subscriptionService = subscriptionService;
            _messageFactory = messageFactory;
            _serviceIdentityProvider = serviceIdentityProvider;
            _configuration = configuration;
            _cancellationTokenSource = new CancellationTokenSource();
            _subscriptionLogger = new Timer(LogSubscriptions, null, TimeSpan.Zero, TimeSpan.FromSeconds(30)); // Reduced frequency
            _messageCleanupTimer = new Timer(CleanupProcessedMessages, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
            _logger = logger;
            
            // Load performance configurations
            var perfSection = configuration.GetSection("Performance");
            _maxConnections = perfSection.GetValue<int>("MaxWebSocketConnections", 1000);
            _messageBufferSize = perfSection.GetValue<int>("MessageBufferSize", 2048);
            _connectionTimeoutMs = perfSection.GetValue<int>("ConnectionTimeoutMs", 60000);
            _messageQueueLimit = perfSection.GetValue<int>("MessageQueueLimit", 50);
            _enableThrottling = perfSection.GetValue<bool>("EnableConnectionThrottling", true);
            
            _connectionSemaphore = new SemaphoreSlim(_maxConnections, _maxConnections);
            
            _logger.LogInformation("WebSocketService initialized with service ID: {ServiceId}, MaxConnections: {MaxConnections}, BufferSize: {BufferSize}", 
                _serviceIdentityProvider.ServiceId, _maxConnections, _messageBufferSize);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Start cleanup tasks
            _ = Task.Run(() => CleanupExpiredConnections(stoppingToken), stoppingToken);
            _ = Task.Run(() => CleanupExpiredCorrelations(stoppingToken), stoppingToken);
            
            // Keep the service running
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping WebSocketService...");
            _cancellationTokenSource.Cancel();
            _subscriptionLogger?.Dispose();
            _messageCleanupTimer?.Dispose();
            _connectionSemaphore?.Dispose();
            await base.StopAsync(cancellationToken);
        }

        private async Task CleanupExpiredConnections(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var expiredConnections = _connectionTimestamps
                        .Where(kvp => DateTime.UtcNow - kvp.Value > TimeSpan.FromMilliseconds(_connectionTimeoutMs))
                        .Select(kvp => kvp.Key)
                        .ToList();

                    foreach (var connectionId in expiredConnections)
                    {
                        _connectionTimestamps.TryRemove(connectionId, out _);
                        _subscriptionService.RemoveSubscription(connectionId);
                        
                        // Clean up any correlation tracking for this connection
                        var correlationsToRemove = _correlationToConnection
                            .Where(kvp => kvp.Value == connectionId)
                            .Select(kvp => kvp.Key)
                            .ToList();
                        
                        foreach (var correlationId in correlationsToRemove)
                        {
                            _correlationToConnection.TryRemove(correlationId, out _);
                            _correlationTimestamps.TryRemove(correlationId, out _);
                        }
                    }

                    if (expiredConnections.Count > 0)
                    {
                        _logger.LogDebug("Cleaned up {Count} expired connections", expiredConnections.Count);
                    }

                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during connection cleanup");
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
            }
        }

        private async Task CleanupExpiredCorrelations(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Clean up correlations older than 2 minutes (increased from 30 seconds to handle high load)
                    var expiredCorrelations = _correlationTimestamps
                        .Where(kvp => DateTime.UtcNow - kvp.Value > TimeSpan.FromMinutes(2))
                        .Select(kvp => kvp.Key)
                        .ToList();

                    foreach (var correlationId in expiredCorrelations)
                    {
                        _correlationToConnection.TryRemove(correlationId, out var connectionId);
                        _correlationTimestamps.TryRemove(correlationId, out var timestamp);
                        _correlationDeliveryCount.TryRemove(correlationId, out var deliveryCount);
                        _logger.LogDebug("Cleaned up expired correlation {CorrelationId} for connection {ConnectionId} (age: {Age}s, deliveries: {DeliveryCount})", 
                            correlationId, connectionId, (DateTime.UtcNow - timestamp).TotalSeconds, deliveryCount);
                    }

                    if (expiredCorrelations.Count > 0)
                    {
                        _logger.LogInformation("Cleaned up {Count} expired correlations. Current active correlations: {ActiveCount}", 
                            expiredCorrelations.Count, _correlationToConnection.Count);
                    }

                    // Log correlation statistics periodically
                    var totalCorrelations = _correlationToConnection.Count;
                    if (totalCorrelations > 0)
                    {
                        _logger.LogDebug("Correlation tracking stats - Active: {ActiveCount}, Oldest: {OldestAge}s", 
                            totalCorrelations, 
                            _correlationTimestamps.Values.Count > 0 ? (DateTime.UtcNow - _correlationTimestamps.Values.Min()).TotalSeconds : 0);
                    }

                    // Run every 30 seconds (reduced frequency to minimize overhead)
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during correlation cleanup");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
            }
        }

        private async Task SubscribeToBackplaneChannel(string envId)
        {
            var backplaneChannel = $"{FEATBIT_ELS_BACKPLANE_PREFIX}{envId}";

            if (_subscribedChannels.ContainsKey(backplaneChannel))
            {
                _logger.LogDebug("Already subscribed to backplane channel: {BackplaneChannel}", backplaneChannel);
                return;
            }

            _logger.LogDebug("Setting up subscription for backplane channel: {BackplaneChannel}", backplaneChannel);
            try
            {
                var subscriptionReady = new TaskCompletionSource<bool>();

                // Subscribe to backplane channel only - Edge service listens to backplane, publishes to edge
                await _backplaneManager.SubscribeAsync(backplaneChannel, async (message) =>
                {
                    _logger.LogDebug("[SUBSCRIPTION CALLBACK] Received message from Redis backplane channel: {Channel}, Message: {Message}", backplaneChannel, message);
                    await HandleRedisMessage(message, backplaneChannel, subscriptionReady);
                });

                _logger.LogDebug("Successfully set up subscription callback for backplane channel: {BackplaneChannel}", backplaneChannel);
                _subscribedChannels.TryAdd(backplaneChannel, true);
                _logger.LogDebug("Marked backplane channel as subscribed: {BackplaneChannel}", backplaneChannel);

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

                _logger.LogDebug("Sending test message to verify subscription on channel: {Channel}", backplaneChannel);
                await _backplaneManager.PublishAsync(backplaneChannel, JsonSerializer.Serialize(testMessage));

                // Wait for the test message to be received (with timeout)
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try
                {
                    await subscriptionReady.Task.WaitAsync(cts.Token);
                    _logger.LogDebug("Subscription verified for backplane channel: {BackplaneChannel}", backplaneChannel);
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
                _logger.LogDebug("[SUBSCRIPTION CALLBACK] Successfully parsed message: {ParsedMessage}", JsonSerializer.Serialize(parsedMessage));

                if (parsedMessage?.ChannelId != null)
                {
                    // Check for message deduplication using correlation ID and sender ID
                    if (!string.IsNullOrEmpty(parsedMessage.CorrelationId) && !string.IsNullOrEmpty(parsedMessage.SenderId))
                    {
                        var messageKey = $"{parsedMessage.CorrelationId}:{parsedMessage.SenderId}";
                        var now = DateTime.UtcNow;
                        
                        // Check if we've already processed this exact message recently
                        if (_processedMessages.TryGetValue(messageKey, out var lastProcessed))
                        {
                            var timeSinceLastProcessed = now - lastProcessed;
                            if (timeSinceLastProcessed < TimeSpan.FromSeconds(30)) // Deduplicate within 30 seconds
                            {
                                _logger.LogDebug("[SUBSCRIPTION CALLBACK] Skipping duplicate Redis message: CorrelationId={CorrelationId}, SenderId={SenderId}, TimeSinceLastProcessed={TimeSinceLastProcessed}ms", 
                                    parsedMessage.CorrelationId, parsedMessage.SenderId, timeSinceLastProcessed.TotalMilliseconds);
                                return; // Skip processing this duplicate message
                            }
                        }
                        
                        // Mark this message as processed
                        _processedMessages.AddOrUpdate(messageKey, now, (key, oldValue) => now);
                    }

                    // Log correlation information if available
                    if (!string.IsNullOrEmpty(parsedMessage.SenderId) || !string.IsNullOrEmpty(parsedMessage.CorrelationId))
                    {
                        WebSocketServiceLogger.ProcessingMessageWithCorrelation(_logger, parsedMessage.ServiceType, parsedMessage.SenderId, parsedMessage.CorrelationId);
                    }

                    var rawContent = parsedMessage.MessageContent.GetRawText() ?? string.Empty;
                    _logger.LogDebug("[SUBSCRIPTION CALLBACK] Raw message content: {RawContent}", rawContent);

                    using var document = JsonDocument.Parse(rawContent);
                    var root = document.RootElement;

                    // First try to get messageType from the root
                    if (root.TryGetProperty("messageType", out var messageType))
                    {
                        var messageTypeStr = messageType.GetString();
                        _logger.LogDebug("[SUBSCRIPTION CALLBACK] Found messageType in root: {MessageType}", messageTypeStr);

                        // If this is a test message, mark the subscription as ready
                        if (messageTypeStr == "test" && !subscriptionReady.Task.IsCompleted)
                        {
                            _logger.LogDebug("[SUBSCRIPTION CALLBACK] Received test message, marking subscription as ready");
                            subscriptionReady.TrySetResult(true);
                            return;
                        }

                        await HandleMessageType(messageTypeStr, parsedMessage.ChannelId, rawContent, parsedMessage);
                    }
                    // Then try to get it from the nested message structure
                    else if (root.TryGetProperty("message", out var messageElement))
                    {
                        _logger.LogDebug("[SUBSCRIPTION CALLBACK] Found message element in root, checking for nested messageType");
                        if (messageElement.TryGetProperty("messageType", out var nestedMessageType))
                        {
                            var messageTypeStr = nestedMessageType.GetString();
                            _logger.LogDebug("[SUBSCRIPTION CALLBACK] Found messageType in nested message: {MessageType}", messageTypeStr);

                            // If this is a test message, mark the subscription as ready
                            if (messageTypeStr == "test" && !subscriptionReady.Task.IsCompleted)
                            {
                                _logger.LogDebug("[SUBSCRIPTION CALLBACK] Received test message, marking subscription as ready");
                                subscriptionReady.TrySetResult(true);
                                return;
                            }

                            await HandleMessageType(messageTypeStr, parsedMessage.ChannelId, rawContent, parsedMessage);
                        }
                                                else if (messageElement.TryGetProperty("data", out var dataElement) &&
                                 dataElement.TryGetProperty("messageType", out var dataMessageType))
                        {
                            var messageTypeStr = dataMessageType.GetString();
                            _logger.LogDebug("[SUBSCRIPTION CALLBACK] Found messageType in data element: {MessageType}", messageTypeStr);

                            // If this is a test message, mark the subscription as ready
                            if (messageTypeStr == "test" && !subscriptionReady.Task.IsCompleted)
                            {
                                _logger.LogDebug("[SUBSCRIPTION CALLBACK] Received test message, marking subscription as ready");
                                subscriptionReady.TrySetResult(true);
                                return;
                            }

                            await HandleMessageType(messageTypeStr, parsedMessage.ChannelId, rawContent, parsedMessage);
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
            // Connection throttling
            if (_enableThrottling && !await _connectionSemaphore.WaitAsync(100, token))
            {
                _logger.LogWarning("Connection rejected - too many concurrent connections");
                await connection.WebSocket.CloseAsync(
                    WebSocketCloseStatus.InternalServerError, 
                    "Server overloaded", 
                    CancellationToken.None);
                return;
            }

            var webSocket = connection.WebSocket;
            var envId = connection.Connection.EnvId.ToString();
            var id = _subscriptionService.AddSubscription(webSocket);
            
            _connectionTimestamps[id] = DateTime.UtcNow;
            
            WebSocketServiceLogger.NewWebSocketConnection(_logger);
            
            try
            {
                await SubscribeToBackplaneChannel(envId);
                _subscriptionService.AddChannelToSubscription(id, envId);
                WebSocketServiceLogger.CreatedSubscription(_logger, id);

                // Use smaller, pooled buffer
                var buffer = ArrayPool<byte>.Shared.Rent(_messageBufferSize);
                
                // Use bounded channel to prevent memory accumulation
                var channelOptions = new BoundedChannelOptions(_messageQueueLimit)
                {
                    FullMode = BoundedChannelFullMode.DropOldest,
                    SingleWriter = true,
                    SingleReader = true,
                    AllowSynchronousContinuations = false
                };
                var channel = Channel.CreateBounded<string>(channelOptions);

                // Process messages on same thread to reduce Task overhead
                var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
                
                try
                {
                    // Main receive loop - simplified without Task.Run overhead
                    await ProcessWebSocketMessages(webSocket, id, connection, buffer, channel.Writer, cts.Token);
                }
                finally
                {
                    // Cleanup resources
                    channel.Writer.Complete();
                    ArrayPool<byte>.Shared.Return(buffer);
                    cts.Dispose();
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // Graceful shutdown - don't log as error
                _logger.LogDebug("WebSocket connection {Id} cancelled during shutdown", id);
            }
            catch (TaskCanceledException) when (token.IsCancellationRequested)
            {
                // Graceful shutdown - don't log as error
                _logger.LogDebug("WebSocket connection {Id} task cancelled during shutdown", id);
            }
            finally
            {
                // Always cleanup connection tracking
                _connectionTimestamps.TryRemove(id, out _);
                _subscriptionService.RemoveSubscription(id);
                
                // Clean up any correlation tracking for this connection
                var correlationsToRemove = _correlationToConnection
                    .Where(kvp => kvp.Value == id)
                    .Select(kvp => kvp.Key)
                    .ToList();
                
                foreach (var correlationId in correlationsToRemove)
                {
                    _correlationToConnection.TryRemove(correlationId, out _);
                    _correlationTimestamps.TryRemove(correlationId, out _);
                    _correlationDeliveryCount.TryRemove(correlationId, out _);
                }
                
                if (correlationsToRemove.Count > 0)
                {
                    _logger.LogDebug("Cleaned up {Count} correlations for disconnected connection {ConnectionId}", 
                        correlationsToRemove.Count, id);
                }
                
                _connectionSemaphore.Release();
                
                if (webSocket.State == WebSocketState.Open)
                {
                    try
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error closing WebSocket for client {Id}", id);
                    }
                }
            }
        }

        private async Task ProcessWebSocketMessages(
            WebSocket webSocket, 
            string id, 
            ConnectionContext connection, 
            byte[] buffer, 
            ChannelWriter<string> messageWriter,
            CancellationToken token)
        {
            var messageCount = 0;
            var lastActivity = DateTime.UtcNow;

            try
            {
                while (!token.IsCancellationRequested && webSocket.State == WebSocketState.Open)
                {
                    WebSocketReceiveResult result;
                    try
                    {
                        // Use timeout to prevent hanging connections
                        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                        timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));
                        
                        result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), timeoutCts.Token);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (WebSocketException ex)
                    {
                        _logger.LogDebug(ex, "WebSocket receive error from client {Id}", id);
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        // Timeout occurred
                        _logger.LogWarning("WebSocket receive timeout for client {Id}", id);
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }

                    lastActivity = DateTime.UtcNow;

                    if (result.Count > 0)
                    {
                        var messageJson = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        if (!string.IsNullOrWhiteSpace(messageJson))
                        {
                            // Process message immediately to avoid queuing overhead
                            try
                            {
                                await HandleMessageAsync(id, connection, messageJson);
                                messageCount++;
                                
                                // Update connection timestamp periodically
                                if (messageCount % 10 == 0)
                                {
                                    _connectionTimestamps[id] = lastActivity;
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error processing message from client {Id}", id);
                                WebSocketServiceLogger.ErrorProcessingMessageFromClient(_logger, id, ex);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in message processing loop for client {Id}", id);
                WebSocketServiceLogger.ErrorHandlingWebSocketClient(_logger, id, ex);
            }
        }

        private async Task HandleMessageType(string? messageType, string channelId, string rawContent, Infrastructure.Scaling.Types.Message? outerMessage = null)
        {
            if (string.IsNullOrEmpty(messageType))
            {
                _logger.LogWarning("Message type is null or empty");
                return;
            }

            _logger.LogDebug("Processing message of type: {MessageType} for channel: {ChannelId}", messageType, channelId);

            switch (messageType)
            {
                case "data-sync":
                    await HandleDataSyncResponse(channelId, rawContent, outerMessage);
                    break;
                case "test":
                    _logger.LogDebug("Received test message on channel: {ChannelId}", channelId);
                    break;
                default:
                    _logger.LogWarning("Unknown message type: {MessageType}", messageType);
                    WebSocketServiceLogger.UnknownMessageType(_logger, messageType);
                    break;
            }
        }

        private async Task HandleDataSyncResponse(string channelId, string rawContent, Infrastructure.Scaling.Types.Message? outerMessage = null)
        {
            try
            {
                _logger.LogDebug("Processing data-sync response for channel: {ChannelId}", channelId);
                
                // First, determine the eventType to decide between targeted vs broadcast delivery
                string? eventType = null;
                string messageContent = rawContent;
                
                _logger.LogDebug("Raw content received: {RawContent}", rawContent);
                
                try
                {
                    using var document = JsonDocument.Parse(rawContent);
                    var root = document.RootElement;
                    
                    // Extract nested message content if it exists
                    if (root.TryGetProperty("message", out var messageElement))
                    {
                        messageContent = messageElement.GetRawText();
                        _logger.LogDebug("Extracted nested message content: {MessageContent}", messageContent);
                    }
                    else
                    {
                        _logger.LogDebug("No nested message found, using raw content as message content");
                    }
                    
                    // Parse the actual data-sync message to get eventType
                    using var contentDocument = JsonDocument.Parse(messageContent);
                    var contentRoot = contentDocument.RootElement;
                    
                    _logger.LogDebug("Parsed message content structure: {Structure}", JsonSerializer.Serialize(contentRoot));
                    
                    if (contentRoot.TryGetProperty("data", out var dataElement))
                    {
                        _logger.LogDebug("Found data element: {DataElement}", JsonSerializer.Serialize(dataElement));
                        
                        if (dataElement.TryGetProperty("eventType", out var eventTypeElement))
                        {
                            eventType = eventTypeElement.GetString();
                            _logger.LogDebug("Found eventType: {EventType}", eventType);
                        }
                        else
                        {
                            _logger.LogDebug("No eventType property found in data element");
                        }
                    }
                    else
                    {
                        _logger.LogDebug("No data property found in message content");
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse message content to determine eventType. RawContent: {RawContent}, MessageContent: {MessageContent}", rawContent, messageContent);
                }
                
                // Handle based on eventType
                if (eventType == "patch")
                {
                    // Patch messages should be broadcast to all clients in the environment
                    _logger.LogDebug("Broadcasting patch message to all clients in channel: {ChannelId}", channelId);
                    WebSocketServiceLogger.BroadcastToSubscribers(_logger, channelId);
                    await _subscriptionService.BroadcastToSubscribersAsync(channelId, messageContent);
                    _logger.LogDebug("Patch message broadcast complete");
                    return;
                }
                else if (eventType == "full")
                {
                    // Full messages should be targeted to the requesting client using correlation tracking
                    _logger.LogDebug("Processing full message with correlation tracking");
                    
                    // Get correlation ID from outer message
                    string? correlationId = outerMessage?.CorrelationId;
                    
                    if (string.IsNullOrEmpty(correlationId))
                    {
                        // Fallback: try to parse correlation ID from raw content
                        using var document = JsonDocument.Parse(rawContent);
                        var root = document.RootElement;
                        
                        if (root.TryGetProperty("correlationId", out var correlationIdElement))
                        {
                            correlationId = correlationIdElement.GetString();
                        }
                    }
                    
                                         // Look for correlation ID to determine target connection
                     if (!string.IsNullOrEmpty(correlationId))
                     {
                         _logger.LogDebug("Found full response with correlation ID: {CorrelationId} from SenderId: {SenderId}", 
                             correlationId, outerMessage?.SenderId);
                         
                         // Debug: Show current correlation tracking state
                         var correlationCount = _correlationToConnection.Count;
                         _logger.LogDebug("Current correlation tracking count: {Count}. All correlations: {AllCorrelations}", 
                             correlationCount, string.Join(", ", _correlationToConnection.Keys.Take(10)));
                         
                         // Find the original connection that sent this request
                         if (_correlationToConnection.TryGetValue(correlationId, out var targetConnectionId))
                         {
                             // Calculate correlation age for debugging
                             var correlationAge = _correlationTimestamps.TryGetValue(correlationId, out var timestamp) 
                                 ? (DateTime.UtcNow - timestamp).TotalMilliseconds 
                                 : -1;
                             
                             _logger.LogDebug("Found target connection {ConnectionId} for correlation {CorrelationId} (age: {Age}ms)", 
                                 targetConnectionId, correlationId, correlationAge);
                             
                             // Send only to the target connection
                             var subscriptions = _subscriptionService.GetSubscriptions();
                             if (subscriptions.ContainsKey(targetConnectionId))
                             {
                                 _logger.LogDebug("Sending targeted full response to connection: {ConnectionId} (correlation age: {Age}ms)", 
                                     targetConnectionId, correlationAge);
                                 await _subscriptionService.BroadcastToSubscriberAsync(targetConnectionId, messageContent);
                                 _logger.LogDebug("Targeted full response sent successfully to connection: {ConnectionId}", targetConnectionId);
                                 
                                 // Mark correlation as delivered but don't remove immediately (delayed cleanup to handle duplicates)
                                 var deliveryCount = _correlationDeliveryCount.AddOrUpdate(correlationId, 1, (key, value) => value + 1);
                                 _logger.LogTrace("Marked correlation {CorrelationId} as delivered (delivery count: {DeliveryCount}, age: {Age}ms)", 
                                     correlationId, deliveryCount, correlationAge);
                                 
                                 // Only clean up after first delivery to prevent duplicate processing
                                 if (deliveryCount == 1)
                                 {
                                     // Schedule cleanup after a short delay to handle any duplicate messages
                                     _ = Task.Delay(TimeSpan.FromSeconds(5)).ContinueWith(task =>
                                     {
                                         _correlationToConnection.TryRemove(correlationId, out var removedConnectionId);
                                         _correlationTimestamps.TryRemove(correlationId, out var removedTimestamp);
                                         _correlationDeliveryCount.TryRemove(correlationId, out var finalCount);
                                         _logger.LogTrace("Delayed cleanup of correlation {CorrelationId} completed (final delivery count: {FinalCount})", 
                                             correlationId, finalCount);
                                     });
                                 }
                                 else
                                 {
                                     _logger.LogDebug("Duplicate message delivery for correlation {CorrelationId} (delivery count: {DeliveryCount})", 
                                         correlationId, deliveryCount);
                                 }
                                 return;
                             }
                             else
                             {
                                 _logger.LogWarning("Target connection {ConnectionId} not found in subscriptions, cleaning up correlation {CorrelationId} (age: {Age}ms)", 
                                     targetConnectionId, correlationId, correlationAge);
                                 _correlationToConnection.TryRemove(correlationId, out _);
                                 _correlationTimestamps.TryRemove(correlationId, out _);
                                 _correlationDeliveryCount.TryRemove(correlationId, out _);
                             }
                         }
                         else
                         {
                             var availableCorrelations = _correlationToConnection.Keys.Take(5).ToList();
                             var totalCorrelations = _correlationToConnection.Count;
                             var oldestCorrelationAge = _correlationTimestamps.Values.Count > 0 
                                 ? (DateTime.UtcNow - _correlationTimestamps.Values.Min()).TotalSeconds 
                                 : 0;
                             
                             _logger.LogWarning("No connection found for correlation ID: {CorrelationId}. " +
                                 "Total active correlations: {TotalCount}, Oldest correlation age: {OldestAge}s, " +
                                 "Sample available correlations: {AvailableCorrelations}", 
                                 correlationId, totalCorrelations, oldestCorrelationAge, string.Join(", ", availableCorrelations));
                         }
                     }
                     else
                     {
                         _logger.LogDebug("No correlation ID found in full message");
                     }
                    
                    // If correlation tracking fails for full message, fall back to broadcast
                    _logger.LogDebug("Correlation tracking failed for full message, falling back to broadcast");
                }
                else
                {
                    _logger.LogTrace("Unknown or missing eventType: {EventType}, ignoring message", eventType ?? "null");
                    return;
                }

                // This should not be reached due to early returns above, just log and ignore
                _logger.LogTrace("Unexpected code path reached for channel: {ChannelId}, ignoring message", channelId);
                return;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse data-sync message, falling back to broadcast");
                WebSocketServiceLogger.BroadcastToSubscribers(_logger, channelId);
                await _subscriptionService.BroadcastToSubscribersAsync(channelId, rawContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling data-sync response for channel: {ChannelId}", channelId);
            }
        }

        public void LogSubscriptions(object? state)
        {
            _logger.LogTrace(JsonSerializer.Serialize(_subscriptionService.GetSubscriptions()));
        }

        public async Task HandleMessageAsync(string id, ConnectionContext ctx, string message)
        {
            _logger.LogDebug("Handling message from client {Id}: {Message}", id, message);
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
            _logger.LogDebug("Processing message for environment {EnvId} on channel {ChannelId}", envId, channelId);

            switch (messageType.ToString())
            {
                case "data-sync":
                    _logger.LogDebug("Processing data-sync message for environment {EnvId}", envId);
                    WebSocketServiceLogger.WebSocketSendingMessage(_logger, id, channelId);
                    
                    var messageContext = CreateMessageContextTransport(ctx, JsonDocument.Parse(message).RootElement);
                    var messageContextJson = JsonSerializer.Serialize(messageContext, JsonSerializerOptions.Web);
                    _logger.LogDebug("Created message context: {MessageContext}", messageContextJson);

                    var backplaneMessage = _messageFactory.CreateMessage(
                        type: "server",
                        channelId: envId,
                        channelName: envId,
                        messageContent: JsonDocument.Parse(messageContextJson).RootElement,
                        senderId: id, // Use the WebSocket subscription ID as sender ID
                        serviceType: ServiceTypes.Edge
                    );

                    WebSocketServiceLogger.MessageCreatedWithCorrelation(_logger, backplaneMessage.ServiceType, backplaneMessage.SenderId, backplaneMessage.CorrelationId);

                    // Track correlation ID to connection ID for targeted responses
                    if (!string.IsNullOrEmpty(backplaneMessage.CorrelationId))
                    {
                        var timestamp = DateTime.UtcNow;
                        var added = _correlationToConnection.TryAdd(backplaneMessage.CorrelationId, id);
                        _correlationTimestamps.TryAdd(backplaneMessage.CorrelationId, timestamp);
                        
                        if (added)
                        {
                            _logger.LogDebug("Added correlation tracking: {CorrelationId} -> {ConnectionId}. Total correlations: {Count}", 
                                backplaneMessage.CorrelationId, id, _correlationToConnection.Count);
                        }
                        else
                        {
                            _logger.LogWarning("Failed to add correlation tracking for {CorrelationId} -> {ConnectionId} (already exists). Total correlations: {Count}", 
                                backplaneMessage.CorrelationId, id, _correlationToConnection.Count);
                        }
                    }

                    var serverMessageJson = JsonSerializer.Serialize<Message>(backplaneMessage, JsonSerializerOptions.Web);
                    _logger.LogDebug("Publishing to backplane channel {ChannelId}: {Message} with SenderId: {SenderId} and CorrelationId: {CorrelationId}", 
                        channelId, serverMessageJson, backplaneMessage.SenderId, backplaneMessage.CorrelationId);

                    await _backplaneManager.PublishAsync(channelId, serverMessageJson);
                    _logger.LogDebug("Successfully published message to backplane");
                    break;

                case "ping":
                    _logger.LogDebug("Handling ping message from client {Id}", id);
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
            var connectionContextInfo = new DefaultConnectionContextInfo(ctx.RawQuery!,
                ctx.ConnectAt, ctx.Client!, connectionInfo, mappedRpConnections);
            return new MessageContext(connectionContextInfo, messageContent);
        }

        public Task HandleSubscribeAsync(string id, string Channel)
        {
            _logger.LogDebug($"Handling subscribe request for Channel: {Channel}");
            _subscriptionService.AddChannelToSubscription(id, Channel);
            _logger.LogDebug($"Added Channel {Channel} to subscription {id}");
            WebSocketServiceLogger.AddedChannelToSubscription(_logger, Channel, id);

            // Edge service should not subscribe to Redis channels directly
            // It only subscribes to backplane channels via SubscribeToBackplaneChannel
            // The subscription service tracks WebSocket subscriptions, not Redis subscriptions
            _logger.LogDebug($"WebSocket subscription added for Channel {Channel}, no Redis subscription needed in Edge service");
            
            return Task.CompletedTask;
        }

        public Task HandleUnsubscribeAsync(string id, string Channel)
        {
            _subscriptionService.RemoveChannelFromSubscription(id, Channel);
            WebSocketServiceLogger.UnsubscribingFromChannel(_logger, Channel);

            // Edge service doesn't manage Redis subscriptions for edge channels
            // Only WebSocket subscription tracking is needed
            _logger.LogDebug($"WebSocket subscription removed for Channel {Channel}");
            
            return Task.CompletedTask;
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

        private void CleanupProcessedMessages(object? state)
        {
            try
            {
                var cutoff = DateTime.UtcNow.AddMinutes(-5); // Keep processed messages for 5 minutes
                var keysToRemove = _processedMessages
                    .Where(kvp => kvp.Value < cutoff)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    _processedMessages.TryRemove(key, out _);
                }

                if (keysToRemove.Count > 0)
                {
                    _logger.LogDebug("Cleaned up {Count} processed message entries", keysToRemove.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during processed messages cleanup");
            }
        }
    }
}