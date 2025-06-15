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
using System.Threading.Tasks;

namespace Streaming.Scaling.Service
{
    public class SubscriptionService : ISubscriptionService, IDisposable
    {
        private readonly Subscriptions _subscriptions;
        private readonly ILogger<SubscriptionService> _logger;
        private readonly object _subscriptionsLock = new object();
        private readonly CancellationTokenSource _shutdownTokenSource = new();

        public SubscriptionService(ILogger<SubscriptionService> logger) 
        {
            _subscriptions = new Subscriptions();
            _logger = logger;
        }

        public string AddSubscription(WebSocket ws)
        {
            var id = Helper.GenerateRandomId();
            lock (_subscriptionsLock)
            {
                _subscriptions[id] = new Subscription(ws);
            }
            return id;
        }

        public void AddChannelToSubscription(string id, string channel)
        {
            lock (_subscriptionsLock)
            {
                if (_subscriptions.TryGetValue(id, out var subscription))
                {
                    subscription.Channels.Add(channel);
                }
            }
        }

        public void RemoveChannelFromSubscription(string id, string channel)
        {
            lock (_subscriptionsLock)
            {
                if (_subscriptions.TryGetValue(id, out var subscription))
                {
                    subscription.Channels.Remove(channel);
                }
            }
        }

        public void RemoveSubscription(string id)
        {
            lock (_subscriptionsLock)
            {
                if (_subscriptions.ContainsKey(id))
                {
                    _subscriptions.Remove(id);
                    _logger.LogDebug($"Removed subscription {id}");
                }
            }
        }

        public bool IsFirstSubscriber(string channel)
        {
            lock (_subscriptionsLock)
            {
                return _subscriptions.Values.ToList().Count(subscription => subscription.Channels.Contains(channel)) == 1;
            }
        }

        public bool IsLastSubscriber(string channel)
        {
            lock (_subscriptionsLock)
            {
                return _subscriptions.Values.ToList().Count(subscription => subscription.Channels.Contains(channel)) == 0;
            }
        }

        public async Task BroadcastToSubscribersAsync(string channelId, string message)
        {
            await BroadcastToSubscribersAsync(channelId, message, null);
        }

        public async Task BroadcastToSubscribersAsync(string channelId, string message, List<string>? subscriberIds = null)
        {
            // Check if shutdown has been initiated
            if (_shutdownTokenSource.Token.IsCancellationRequested)
            {
                return;
            }

            List<Subscription> subscriptions;
            lock (_subscriptionsLock)
            {
                subscriptions = _subscriptions.Values
                    .Where(s => s.Channels.Contains(channelId))
                    .ToList();
                    
                // Filter by specific subscriber IDs if provided
                if (subscriberIds != null && subscriberIds.Count > 0)
                {
                    var filteredSubscriptions = new List<Subscription>();
                    foreach (var kvp in _subscriptions)
                    {
                        if (subscriberIds.Contains(kvp.Key) && kvp.Value.Channels.Contains(channelId))
                        {
                            filteredSubscriptions.Add(kvp.Value);
                        }
                    }
                    subscriptions = filteredSubscriptions;
                }
            }

            if (subscriptions.Count == 0)
            {
                return;
            }

            // Use parallel processing for better performance with many connections
            var messageBytes = Encoding.UTF8.GetBytes(message);
            var tasks = new List<Task>();
            var semaphore = new SemaphoreSlim(Environment.ProcessorCount * 4, Environment.ProcessorCount * 4);

            foreach (var subscription in subscriptions)
            {
                tasks.Add(SendToSubscriptionAsync(subscription, messageBytes, semaphore));
            }

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting to subscribers for channel {ChannelId}", channelId);
            }
            finally
            {
                semaphore.Dispose();
            }
        }

        public async Task BroadcastToSubscriberAsync(string subscriberId, string message)
        {
            // Check if shutdown has been initiated
            if (_shutdownTokenSource.Token.IsCancellationRequested)
            {
                return;
            }

            Subscription? subscription;
            lock (_subscriptionsLock)
            {
                _subscriptions.TryGetValue(subscriberId, out subscription);
            }

            if (subscription != null)
            {
                var messageBytes = Encoding.UTF8.GetBytes(message);
                var semaphore = new SemaphoreSlim(1, 1);
                
                try
                {
                    await SendToSubscriptionAsync(subscription, messageBytes, semaphore);
                }
                finally
                {
                    semaphore.Dispose();
                }
            }
        }

        private async Task SendToSubscriptionAsync(Subscription subscription, byte[] messageBytes, SemaphoreSlim semaphore)
        {
            await semaphore.WaitAsync();
            try
            {
                // Check if WebSocket is disposed before accessing State property
                try
                {
                    if (subscription.WebSocket.State == WebSocketState.Open)
                    {
                        await subscription.WebSocket.SendAsync(
                            new ArraySegment<byte>(messageBytes),
                            WebSocketMessageType.Text,
                            true,
                            _shutdownTokenSource.Token);
                    }
                    else
                    {
                        // Remove dead connection
                        RemoveSubscriptionFromTracking(subscription);
                    }
                }
                catch (ObjectDisposedException)
                {
                    // WebSocket has been disposed (likely during shutdown) - silently remove from tracking
                    RemoveSubscriptionFromTracking(subscription);
                }
            }
            catch (WebSocketException ex)
            {
                _logger.LogDebug(ex, "WebSocket error sending message to subscriber");
                // Remove the subscription if the WebSocket is in a bad state
                RemoveSubscriptionFromTracking(subscription);
            }
            catch (OperationCanceledException)
            {
                // Operation was cancelled (likely during shutdown) - silently remove from tracking
                RemoveSubscriptionFromTracking(subscription);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error sending message to subscriber");
                RemoveSubscriptionFromTracking(subscription);
            }
            finally
            {
                semaphore.Release();
            }
        }

        private void RemoveSubscriptionFromTracking(Subscription subscription)
        {
            lock (_subscriptionsLock)
            {
                var toRemove = _subscriptions.FirstOrDefault(kvp => kvp.Value == subscription);
                if (toRemove.Key != null)
                {
                    _subscriptions.Remove(toRemove.Key);
                }
            }
        }

        public Subscriptions GetSubscriptions()
        {
            lock (_subscriptionsLock)
            {
                var copy = new Subscriptions();
                foreach (var kvp in _subscriptions)
                {
                    copy[kvp.Key] = kvp.Value;
                }
                return copy;
            }
        }

        public async Task DisconnectAllAsync(WebSocketCloseStatus closeStatus = WebSocketCloseStatus.NormalClosure, string statusDescription = "Server shutdown")
        {
            // Signal shutdown to prevent new broadcasts
            _shutdownTokenSource.Cancel();

            List<KeyValuePair<string, Subscription>> subscriptionsSnapshot;
            lock (_subscriptionsLock)
            {
                subscriptionsSnapshot = _subscriptions.ToList();
            }

            var connectionCount = subscriptionsSnapshot.Count;
            WebSocketServiceLogger.WebSocketShutdownInitiated(_logger, connectionCount);

            if (connectionCount == 0)
            {
                WebSocketServiceLogger.WebSocketShutdownCompleted(_logger);
                return;
            }

            var disconnectTasks = new List<Task>();

            foreach (var kvp in subscriptionsSnapshot)
            {
                var subscriptionId = kvp.Key;
                var subscription = kvp.Value;
                var webSocket = subscription.WebSocket;

                disconnectTasks.Add(DisconnectWebSocketAsync(subscriptionId, webSocket, closeStatus, statusDescription));
            }

            // Wait for all disconnections to complete with a reasonable timeout
            try
            {
                await Task.WhenAll(disconnectTasks).WaitAsync(TimeSpan.FromSeconds(10));
                WebSocketServiceLogger.WebSocketShutdownCompleted(_logger);
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Timeout occurred while disconnecting WebSocket connections, forcing shutdown");
                // Force clear all subscriptions on timeout
                lock (_subscriptionsLock)
                {
                    _subscriptions.Clear();
                }
            }
            catch (Exception ex)
            {
                WebSocketServiceLogger.WebSocketShutdownError(_logger, ex);
                // Force clear all subscriptions on error
                lock (_subscriptionsLock)
                {
                    _subscriptions.Clear();
                }
            }

            // Ensure all subscriptions are cleared
            lock (_subscriptionsLock)
            {
                if (_subscriptions.Count > 0)
                {
                    _logger.LogInformation("Clearing {Count} remaining subscriptions", _subscriptions.Count);
                    _subscriptions.Clear();
                }
            }
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
                lock (_subscriptionsLock)
                {
                    _subscriptions.Remove(subscriptionId);
                }
                _logger.LogDebug("Removed subscription {SubscriptionId} from tracking", subscriptionId);
            }
        }

        public void Dispose()
        {
            _shutdownTokenSource?.Dispose();
        }
    }
} 