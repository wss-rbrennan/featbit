using Microsoft.Extensions.Logging;
using Infrastructure.Scaling.Manager;
using Infrastructure.Scaling.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Infrastructure.Scaling.Types;

namespace Infrastructure.Scaling.service
{
    public interface IWebSockerService
    {
        public Task SubscribeToFeatbitChannels();
        public void LogSubscriptions(object? state);
        
        public Task HandleConnectionAsync(WebSocket webSocket);
       
        public Task HandleMessageAsync(string id, Message message);
        
        public Task HandleSubscribeAsync(string id, string room);
  
        public Task HandleUnsubscribeAsync(string id, string room);
        
        public Task HandleSendMessageAsync(Message message);
        
    }
}
