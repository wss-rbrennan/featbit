//using Domain.Shared;
//using Infrastructure.Connections;
//using Infrastructure.Scaling.Manager;
//using Infrastructure.Scaling.Types;
//using Infrastructure.Protocol;
//using Microsoft.Extensions.Hosting;
//using Microsoft.Extensions.Logging;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Text.Json;
//using System.Threading;
//using System.Threading.Tasks;

//namespace Backplane.Services
//{
//    public class HubService : BackgroundService, IHubService
//    {
//        private readonly IBackplaneManager _backplaneManager;
//        //private readonly ISubscriptionService _subscriptionService;
//        private readonly CancellationTokenSource _cancellationTokenSource;
//        private readonly Timer _subscriptionLogger;
//        private readonly ILogger<HubService> _logger;
//        private const string FEATBIT_ELS_PATTERN = "featbit:els:*";
        
//        public HubService(ILogger<HubService> logger,
//                           IBackplaneManager backplaneManager)
//        {
//            _backplaneManager = backplaneManager;
//            //_subscriptionService = subscriptionService;
//            _cancellationTokenSource = new CancellationTokenSource();
//            _subscriptionLogger = new Timer(LogSubscriptions, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
//            _logger = logger;
//            // Subscribe to all featbit:els:* channels
//            SubscribeToFeatbitChannels().ConfigureAwait(false);

            
//        }

//        public Task HandleConnectionAsync(CancellationToken token)
//        {
//            //var id = _subscriptionService.AddSubscription();

//            throw new NotImplementedException();
//        }

//        public Task HandleMessageAsync(string id, ConnectionContext ctx, string message)
//        {
//            throw new NotImplementedException();
//        }

//        public Task HandleSendMessageAsync(Message message)
//        {
//            throw new NotImplementedException();
//        }

//        public async Task HandleSubscribeAsync(string id, string Channel)
//        {
//            _logger.LogDebug($"Handling subscribe request for Channel: {Channel}");
//            _subscriptionService.AddChannelToSubscription(id, Channel);
//            _logger.LogDebug($"Added Channel {Channel} to subscription {id}");
//            WebSocketServiceLogger.AddedChannelToSubscription(_logger, Channel, id);

//            if (_subscriptionService.IsFirstSubscriber(Channel))
//            {
//                _logger.LogDebug($"First subscriber for Channel: {Channel}, setting up Redis subscription");
//                try
//                {
//                    await _backplaneManager.SubscribeAsync(Channel, async (message) =>
//                    {
//                        _logger.LogDebug($"Received message from Redis for Channel {Channel}: {message}");
//                        try
//                        {
//                            var parsedMessage = JsonSerializer.Deserialize<Message>(message);
//                            _logger.LogDebug($"Parsed message: {JsonSerializer.Serialize(parsedMessage)}");

//                            if (parsedMessage?.ChannelId != null)
//                            {
//                                _logger.LogDebug($"Broadcasting message to Channel {parsedMessage.ChannelId}");
//                                var subscribers = _subscriptionService.GetSubscriptions()
//                                    .Where(s => s.Value.Channels.Contains(parsedMessage.ChannelId))
//                                    .ToList();
//                                _logger.LogDebug($"Found {subscribers.Count} subscribers for Channel {parsedMessage.ChannelId}");
//                                _logger.LogDebug($"Subscriber IDs: {string.Join(", ", subscribers.Select(s => s.Key))}");

//                                await _subscriptionService.BroadcastToSubscribersAsync(parsedMessage.ChannelId, message);
//                                _logger.LogDebug($"Message broadcast complete");
//                            }
//                            else
//                            {
//                                _logger.LogDebug($"Failed to parse message or ChannelId is null: {message}");
//                            }
//                        }
//                        catch (Exception ex)
//                        {
//                            _logger.LogError($"Error processing Redis message: {ex}");
//                        }
//                    });
//                    _logger.LogDebug($"Redis subscription for Channel {Channel} set up successfully");
//                }
//                catch (Exception ex)
//                {
//                    _logger.LogError($"Error setting up Redis subscription: {ex}");
//                }
//            }
//            else
//            {
//                _logger.LogDebug($"Not first subscriber for Channel {Channel}, skipping Redis subscription");
//            }
//        }

//        public Task HandleUnsubscribeAsync(string id, string room)
//        {
//            throw new NotImplementedException();
//        }

//        public void LogSubscriptions(object? state)
//        {
//            throw new NotImplementedException();
//        }

//        public async Task SubscribeToFeatbitChannels()
//        {
//            WebSocketServiceLogger.SettingUpSubscription(_logger, FEATBIT_ELS_PATTERN);
//            try
//            {
//                await _backplaneManager.SubscribeAsync(FEATBIT_ELS_PATTERN, async (message) =>
//                {
//                    WebSocketServiceLogger.ReceivedChannelMessage(_logger, FEATBIT_ELS_PATTERN, message);

//                    try
//                    {
//                        var parsedMessage = JsonSerializer.Deserialize<Message>(message);

//                        if (parsedMessage?.ChannelId != null)
//                        {
                            
//                            using var document = JsonDocument.Parse(parsedMessage?.MessageContent.GetRawText());
//                            var root = document.RootElement;

//                            if (!root.TryGetProperty("messageType", out var messageType))
//                            {
//                                {
//                                    throw new InvalidDataException("invalid message type");
//                                }
//                            }

//                            switch (messageType.ToString())
//                            {
//                                case "data-sync":
//                                    WebSocketServiceLogger.BroadcastToSubscribers(_logger, parsedMessage.ChannelId);
//                                    await _subscriptionService.BroadcastToSubscribersAsync(parsedMessage.ChannelId, parsedMessage?.MessageContent.GetRawText());
//                                    break;
//                                default:
//                                    WebSocketServiceLogger.UnknownMessageType(_logger, messageType.GetString());
//                                    break;
//                            }
//                        }
//                        else
//                        {
//                            WebSocketServiceLogger.FailedToParseMessageOrChannelIdIsNull(_logger, message);
//                        }
//                    }
//                    catch (Exception ex)
//                    {
//                        _logger.LogDebug($"Error processing Redis message: {ex}");
//                    }
//                });
//                _logger.LogDebug($"Successfully subscribed to Redis channels matching pattern: {FEATBIT_ELS_PATTERN}");
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError($"Error setting up Redis subscription for pattern {FEATBIT_ELS_PATTERN}: {ex}");
//                WebSocketServiceLogger.ErrorProcessingMessage(_logger, FEATBIT_ELS_PATTERN, ex);
//            }
//        }

//        public Task HandleMessageAsync(string message)
//        {
//            throw new NotImplementedException();
//        }

//        protected override Task ExecuteAsync(CancellationToken stoppingToken)
//        {
//            throw new NotImplementedException();
//        }
//    }
//}
