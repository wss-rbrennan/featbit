using Infrastructure.Scaling.Types;
using Infrastructure.Scaling.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace Streaming.Scaling.Service
{
    public interface ISubscriptionService
    {
        public string AddSubscription(WebSocket ws);


        public void AddChannelToSubscription(string id, string channel);

        public void RemoveChannelFromSubscription(string id, string channel);

        public void RemoveSubscription(string id);

        public bool IsFirstSubscriber(string channel);

        public bool IsLastSubscriber(string channel);

        public Task BroadcastToSubscribersAsync(string channelId, string message, List<string>? subscriberIds = null);

        public Task BroadcastToSubscriberAsync(string subscriberId, string message);

        public Subscriptions GetSubscriptions();
    }
}
