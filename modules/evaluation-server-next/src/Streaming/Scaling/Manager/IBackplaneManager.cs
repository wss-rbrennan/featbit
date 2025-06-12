using Streaming.Scaling.Manager;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Streaming.Scaling.Manager
{
    public interface IBackplaneManager
    {

        Task ConnectAsync();

        Task<long> PublishAsync(string channel, string message);

        Task SubscribeAsync(string channel, Action<string> callback);

        Task UnsubscribeAsync(string channel);

        Task DisconnectAsync();

    }
}