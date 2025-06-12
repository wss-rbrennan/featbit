using Microsoft.Extensions.Logging;
using Streaming.Scaling.Manager;
using Streaming.Scaling.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Infrastructure.Scaling.Types;
using Infrastructure.Connections;

namespace Streaming.Scaling.Service
{
    public interface IWebSocketService : IBackplaneServiceBase
    {
        public Task HandleConnectionAsync(ConnectionContext ctx, CancellationToken token);
       
        public Task HandleMessageAsync(string id, ConnectionContext ctx, string message);

    }
}
