using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Infrastructure.Scaling.Manager;
using Infrastructure.Scaling.Types;
using System.Linq;
using Microsoft.Extensions.Logging;
using Infrastructure.Scaling.service;

namespace Infrastructure.Scaling.Service
{
    public class WebSocketService
    {
        private readonly RedisManager _redisManager;
        private readonly ISubscriptionService _subscriptionService;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Timer _subscriptionLogger;
        private readonly ILogger<WebSocketService> _logger;
        private const string FEATBIT_ELS_PATTERN = "featbit:els:*";

        public WebSocketService(ILogger<WebSocketService> logger, ISubscriptionService subscriptionService, RedisManager redisManager)
        {
            _redisManager = redisManager;
            _subscriptionService = subscriptionService;
            _cancellationTokenSource = new CancellationTokenSource();
            _subscriptionLogger = new Timer(LogSubscriptions, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
            _logger = logger;
            // Subscribe to all featbit:els:* channels
            SubscribeToFeatbitChannels().ConfigureAwait(false);
        }

        private async Task SubscribeToFeatbitChannels()
        {
            _logger.LogDebug($"Setting up subscription to Redis channels matching pattern: {FEATBIT_ELS_PATTERN}");
            try
            {
                await _redisManager.SubscribeAsync(FEATBIT_ELS_PATTERN, async (message) =>
                {
                    _logger.LogDebug($"Received message from Redis channel matching {FEATBIT_ELS_PATTERN}: {message}");
                    try
                    {
                        var parsedMessage = JsonSerializer.Deserialize<Message>(message);
                        _logger.LogDebug($"Parsed message: {JsonSerializer.Serialize(parsedMessage)}");

                        if (parsedMessage?.RoomId != null)
                        {
                            _logger.LogDebug($"Broadcasting message to room {parsedMessage.RoomId}");
                            await _subscriptionService.BroadcastToRoomAsync(parsedMessage.RoomId, message);
                            _logger.LogDebug($"Message broadcast complete");
                        }
                        else
                        {
                            _logger.LogDebug($"Failed to parse message or RoomId is null: {message}");
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
            }
        }

        private void LogSubscriptions(object? state)
        {
            _logger.LogDebug(JsonSerializer.Serialize(_subscriptionService.GetSubscriptions()));
        }

        public async Task HandleConnectionAsync(WebSocket webSocket)
        {
            _logger.LogDebug("New WebSocket connection received");
            var id = _subscriptionService.AddSubscription(webSocket);
            _logger.LogDebug($"Created new subscription with ID: {id}");
            var buffer = new byte[1024 * 4];

            try
            {
                while (webSocket.State == WebSocketState.Open)
                {
                    _logger.LogDebug($"Waiting for message from client {id}...");
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellationTokenSource.Token);
                    _logger.LogDebug($"Received message of type: {result.MessageType}, length: {result.Count}");

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
                        var message = JsonSerializer.Deserialize<Message>(messageJson);
                        if (message != null)
                        {
                            _logger.LogDebug($"Successfully parsed message from client {id}: {JsonSerializer.Serialize(message)}");
                            await HandleMessageAsync(id, message);
                        }
                        else
                        {
                            _logger.LogDebug($"Failed to parse message from client {id}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error processing message from client {id}: {ex}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error handling WebSocket connection for client {id}: {ex}");
            }
            finally
            {
                _logger.LogDebug($"Cleaning up connection for client {id}");
                if (webSocket.State == WebSocketState.Open)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                }
                _subscriptionService.RemoveSubscription(id);
                _logger.LogDebug($"Connection cleanup complete for client {id}");
            }
        }

        private async Task HandleMessageAsync(string id, Message message)
        {
            _logger.LogDebug($"Received message of type: {message.Type}");
            _logger.LogDebug($"Message content: {JsonSerializer.Serialize(message)}");

            switch (message.Type)
            {
                case "SUBSCRIBE":
                    if (string.IsNullOrEmpty(message.Room))
                    {
                        _logger.LogDebug("Error: Room is null or empty in SUBSCRIBE message");
                        return;
                    }
                    await HandleSubscribeAsync(id, message.Room);
                    break;
                case "UNSUBSCRIBE":
                    if (string.IsNullOrEmpty(message.Room))
                    {
                        _logger.LogDebug("Error: Room is null or empty in UNSUBSCRIBE message");
                        return;
                    }
                    await HandleUnsubscribeAsync(id, message.Room);
                    break;
                case "SEND_MESSAGE":
                    if (string.IsNullOrEmpty(message.RoomId))
                    {
                        _logger.LogDebug("Error: RoomId is null or empty in SEND_MESSAGE");
                        return;
                    }
                    await HandleSendMessageAsync(message);
                    break;
                case "data-sync":
                    if (string.IsNullOrEmpty(message.RoomId))
                    {
                        _logger.LogDebug("Error: RoomId is null or empty in SEND_MESSAGE");
                        return;
                    }
                    //await HandleSendMessageAsync(message);
                    break;
                case "patch":
                    if (string.IsNullOrEmpty(message.RoomId))
                    {
                        _logger.LogDebug("Error: RoomId is null or empty in SEND_MESSAGE");
                        return;
                    }
                    //await HandleSendMessageAsync(message);
                    break;
                default:
                    _logger.LogDebug($"Unknown message type: {message.Type}");
                    break;
            }
        }

        private async Task HandleSubscribeAsync(string id, string room)
        {
            _logger.LogDebug($"Handling subscribe request for room: {room}");
            _subscriptionService.AddRoomToSubscription(id, room);
            _logger.LogDebug($"Added room {room} to subscription {id}");
            _logger.LogDebug($"Current subscription state: {JsonSerializer.Serialize(_subscriptionService.GetSubscriptions())}");

            if (_subscriptionService.IsFirstSubscriber(room))
            {
                _logger.LogDebug($"First subscriber for room: {room}, setting up Redis subscription");
                try
                {
                    await _redisManager.SubscribeAsync(room, async (message) =>
                    {
                        _logger.LogDebug($"Received message from Redis for room {room}: {message}");
                        try
                        {
                            var parsedMessage = JsonSerializer.Deserialize<Message>(message);
                            _logger.LogDebug($"Parsed message: {JsonSerializer.Serialize(parsedMessage)}");

                            if (parsedMessage?.RoomId != null)
                            {
                                _logger.LogDebug($"Broadcasting message to room {parsedMessage.RoomId}");
                                var subscribers = _subscriptionService.GetSubscriptions()
                                    .Where(s => s.Value.Rooms.Contains(parsedMessage.RoomId))
                                    .ToList();
                                _logger.LogDebug($"Found {subscribers.Count} subscribers for room {parsedMessage.RoomId}");
                                _logger.LogDebug($"Subscriber IDs: {string.Join(", ", subscribers.Select(s => s.Key))}");

                                await _subscriptionService.BroadcastToRoomAsync(parsedMessage.RoomId, message);
                                _logger.LogDebug($"Message broadcast complete");
                            }
                            else
                            {
                                _logger.LogDebug($"Failed to parse message or RoomId is null: {message}");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Error processing Redis message: {ex}");
                        }
                    });
                    _logger.LogDebug($"Redis subscription for room {room} set up successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error setting up Redis subscription: {ex}");
                }
            }
            else
            {
                _logger.LogDebug($"Not first subscriber for room {room}, skipping Redis subscription");
            }
        }

        private async Task HandleUnsubscribeAsync(string id, string room)
        {
            _subscriptionService.RemoveRoomFromSubscription(id, room);

            if (_subscriptionService.IsLastSubscriber(room))
            {
                _logger.LogDebug($"Unsubscribing from room: {room}");
                await _redisManager.UnsubscribeAsync(room);
            }
        }

        private async Task HandleSendMessageAsync(Message message)
        {
            _logger.LogDebug($"Handling send message request for room: {message.RoomId}");
            var messageJson = JsonSerializer.Serialize(new
            {
                type = "SEND_MESSAGE",
                roomId = message.RoomId,
                message = message.MessageContent
            });
            _logger.LogDebug($"Publishing message to Redis: {messageJson}");
            try
            {
                await _redisManager.PublishAsync(message.RoomId, messageJson);
                _logger.LogDebug($"Message published to Redis successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error publishing message to Redis: {ex}");
            }
        }
    }
}