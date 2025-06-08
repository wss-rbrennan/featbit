using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Infrastructure.Scaling.service;
using Infrastructure.Scaling.Types;
using Infrastructure.Scaling.Utils;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Collections.Generic;
using System.Threading;

namespace Infrastructure.Scaling.Service
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

        public void AddRoomToSubscription(string id, string room)
        {
            if (_subscriptions.TryGetValue(id, out var subscription))
            {
                subscription.Rooms.Add(room);
            }
        }

        public void RemoveRoomFromSubscription(string id, string room)
        {
            if (_subscriptions.TryGetValue(id, out var subscription))
            {
                subscription.Rooms.Remove(room);
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

        public bool IsFirstSubscriber(string room)
        {
            return _subscriptions.Values.Count(subscription => subscription.Rooms.Contains(room)) == 1;
        }

        public bool IsLastSubscriber(string room)
        {
            return _subscriptions.Values.Count(subscription => subscription.Rooms.Contains(room)) == 0;
        }

        public async Task BroadcastToRoomAsync(string roomId, string message, List<string>? subscriberIds = null)
        {
            var subscribers = GetSubscriptions()
                .Where(s => s.Value.Rooms.Contains(roomId))
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
    }
} 