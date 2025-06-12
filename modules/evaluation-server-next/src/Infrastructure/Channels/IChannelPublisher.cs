using Infrastructure.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Channels
{
    public interface IChannelPublisher
    {

        Task PublishAsync<T>(string channelId, T message);

    }
}
