using Infrastructure.Connections;
using System.Threading.Channels;

namespace Streaming.Messages
{
    public interface IMessageProcessor
    {
        Task ProcessAsync(string id, ConnectionContext connection, ChannelReader<string> reader, CancellationToken token);
    }

}
