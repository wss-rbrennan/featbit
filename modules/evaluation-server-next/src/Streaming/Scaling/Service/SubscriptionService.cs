using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Streaming.Scaling.Service;
using Infrastructure.Scaling.Types;
using Infrastructure.Scaling.Utils;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Collections.Generic;
using System.Threading;

namespace Streaming.Scaling.Service
{
    public class SubscriptionService : ISubscriptionService
    {
        private readonly Subscriptions _subscriptions;
        private readonly ILogger<SubscriptionService> _logger;

        public SubscriptionService(ILogger<SubscriptionService> logger) 
        {
            _subscriptions = new Subscriptions();
            _logger = logger;
        }

        public string AddSubscription(WebSocket ws)
        {
            var id = Helper.GenerateRandomId();
            _subscriptions[id] = new Subscription(ws);
            return id;
        }

        public void AddChannelToSubscription(string id, string channel)
        {
            if (_subscriptions.TryGetValue(id, out var subscription))
            {
                subscription.Channels.Add(channel);
            }
        }

        public void RemoveChannelFromSubscription(string id, string channel)
        {
            if (_subscriptions.TryGetValue(id, out var subscription))
            {
                subscription.Channels.Remove(channel);
            }
        }

        public void RemoveSubscription(string id)
        {
            if (_subscriptions.ContainsKey(id))
            {
                _subscriptions.Remove(id);
                _logger.LogDebug($"Removed subscription {id}");
            }
        }

        public bool IsFirstSubscriber(string channel)
        {
            return _subscriptions.Values.Count(subscription => subscription.Channels.Contains(channel)) == 1;
        }

        public bool IsLastSubscriber(string channel)
        {
            return _subscriptions.Values.Count(subscription => subscription.Channels.Contains(channel)) == 0;
        }

        public async Task BroadcastToSubscribersAsync(string channel, string message, List<string>? subscriberIds = null)
        {
            var subscribers = GetSubscriptions()
                .Where(s => s.Value.Channels.Contains(channel))
                .ToList();

            if (subscriberIds != null)
            {
                subscribers = subscribers.Where(s => subscriberIds.Contains(s.Key)).ToList();
            }

            foreach (var subscriber in subscribers)
            {
                await BroadcastToSubscriberAsync(subscriber.Key, message);
            }
        }

        public async Task BroadcastToSubscriberAsync(string subscriberId, string message)
        {
            if (_subscriptions.TryGetValue(subscriberId, out var subscription))
            {
                await subscription.WebSocket.SendAsync(
                    new ArraySegment<byte>(Encoding.UTF8.GetBytes(message)),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None);
            }
        }

        public Subscriptions GetSubscriptions()
        {
            return _subscriptions;
        }

        public async Task DisconnectAllAsync(WebSocketCloseStatus closeStatus = WebSocketCloseStatus.NormalClosure, string statusDescription = "Server shutdown")
        {
            var connectionCount = _subscriptions.Count;
            WebSocketServiceLogger.WebSocketShutdownInitiated(_logger, connectionCount);

            var disconnectTasks = new List<Task>();

            foreach (var kvp in _subscriptions.ToList()) // ToList() to avoid modification during enumeration
            {
                var subscriptionId = kvp.Key;
                var subscription = kvp.Value;
                var webSocket = subscription.WebSocket;

                disconnectTasks.Add(DisconnectWebSocketAsync(subscriptionId, webSocket, closeStatus, statusDescription));
            }

            // Wait for all disconnections to complete with a reasonable timeout
            try
            {
                await Task.WhenAll(disconnectTasks).WaitAsync(TimeSpan.FromSeconds(30));
                WebSocketServiceLogger.WebSocketShutdownCompleted(_logger);
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Timeout occurred while disconnecting WebSocket connections");
            }
            catch (Exception ex)
            {
                WebSocketServiceLogger.WebSocketShutdownError(_logger, ex);
            }

            // Clear all subscriptions after attempting to disconnect
            _subscriptions.Clear();
            _logger.LogInformation("All subscriptions cleared");
        }

        private async Task DisconnectWebSocketAsync(string subscriptionId, WebSocket webSocket, WebSocketCloseStatus closeStatus, string statusDescription)
        {
            try
            {
                if (webSocket.State == WebSocketState.Open || webSocket.State == WebSocketState.CloseReceived)
                {
                    WebSocketServiceLogger.ClosingWebSocketConnection(_logger, subscriptionId);
                    await webSocket.CloseAsync(closeStatus, statusDescription, CancellationToken.None);
                }
                else
                {
                    _logger.LogDebug("WebSocket connection for subscription {SubscriptionId} is already in state {State}", subscriptionId, webSocket.State);
                }
            }
            catch (WebSocketException ex)
            {
                _logger.LogWarning(ex, "WebSocket exception while closing connection for subscription {SubscriptionId}", subscriptionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while closing WebSocket connection for subscription {SubscriptionId}", subscriptionId);
            }
            finally
            {
                // Remove the subscription from our tracking
                _subscriptions.Remove(subscriptionId);
                _logger.LogDebug("Removed subscription {SubscriptionId} from tracking", subscriptionId);
            }
        }
    }
} 