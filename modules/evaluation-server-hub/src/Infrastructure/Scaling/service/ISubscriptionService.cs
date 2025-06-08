using Infrastructure.Scaling.Types;
using Infrastructure.Scaling.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Scaling.service
{
    public interface ISubscriptionService
    {
        public string AddSubscription(WebSocket ws);
        
        public void AddRoomToSubscription(string id, string room);

        public void RemoveRoomFromSubscription(string id, string room);

        public void RemoveSubscription(string id);

        public bool IsFirstSubscriber(string room);

        public bool IsLastSubscriber(string room);

        public Task BroadcastToRoomAsync(string roomId, string message, List<string>? subscriberIds = null);
        
        public Task BroadcastToSubscriberAsync(string subscriberId, string message);
        
        public Subscriptions GetSubscriptions();
    }
}
