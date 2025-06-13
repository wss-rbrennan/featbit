using Infrastructure.Connections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Infrastructure.Scaling.Types;

namespace Streaming.Scaling.Service
{
    public interface IBackplaneServiceBase
    {
        public void LogSubscriptions(object? state);

        public Task HandleSubscribeAsync(string id, string channel);

        public Task HandleUnsubscribeAsync(string id, string channel);
    }
}
