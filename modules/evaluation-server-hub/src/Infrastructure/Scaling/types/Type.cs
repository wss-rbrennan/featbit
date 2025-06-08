using System.Net.WebSockets;
using System.Collections.Generic;

namespace Infrastructure.Scaling.Types
{
    public class Subscription
    {
        public WebSocket WebSocket { get; set; }
        public List<string> Rooms { get; set; }

        public Subscription(WebSocket webSocket)
        {
            WebSocket = webSocket;
            Rooms = new List<string>();
        }
    }

    public class Subscriptions : Dictionary<string, Subscription>
    {
    }
} 