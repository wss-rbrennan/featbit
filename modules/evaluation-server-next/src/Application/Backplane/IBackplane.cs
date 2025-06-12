using Application.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Backplane
{
    public interface IBackplane
    {
        Task PublishToChannelAsync(string channelId, ServerMessage serverMessage, CancellationToken cancellationToken);

        Task PublishToNamespaceAsync(string channelId, ServerMessage serverMessage, CancellationToken cancellationToken);

        Task SubscribeToChannelAsync(string channelId, CancellationToken cancellationToken);

        Task SubscribeToNamespaceAsync(string channelId, CancellationToken cancellationToken);

    }
}
