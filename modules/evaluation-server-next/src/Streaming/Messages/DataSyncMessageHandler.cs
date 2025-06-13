using Domain.Shared;
using Infrastructure.Channels;
using Infrastructure.Protocol;
using System.Text.Json;

namespace Streaming.Messages;

public class DataSyncMessageHandler : IMessageHandler
{
    public string Type => MessageTypes.DataSync;

    private readonly IChannelPublisher _channelPublisher;

    public DataSyncMessageHandler(IChannelPublisher channelPublisher)
    {
        _channelPublisher = channelPublisher;
    }

    public async Task HandleAsync(MessageContext ctx)
    {
        var connectionContext = ctx.Connection;

        var message = ctx.Data.Deserialize<DataSyncMessage>(ReusableJsonSerializerOptions.Web);
        if (message == null)
        {
            return;
        }

        using var document = JsonDocument.Parse(ctx.Data.ToString());
        var data = document.RootElement;

        var token = new Token(ctx.Connection.Token.AsSpan());   

        var envId = ctx.Connection.Connection.EnvId.ToString();

        var channelId = Infrastructure.BackplaneMesssages.Channels.GetEdgeChannel(envId.ToString()).Replace("featbit-els-", "featbit:els:");

        await _channelPublisher.PublishAsync(channelId, message);
        //await _producer.PublishAsync(channelId, serverMessage, cancellationToken);

        //var payload = await _service.GetPayloadAsync(connectionContext, message);
        //var serverMessage = new ServerMessage(MessageTypes.DataSync, payload);
        //await connectionContext.SendAsync(serverMessage, ctx.CancellationToken);
    }
}