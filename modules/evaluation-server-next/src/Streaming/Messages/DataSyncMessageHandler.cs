using Domain.EndUsers;
using Domain.Messages;
using Domain.Shared;
using Infrastructure.Channels;
using MongoDB.Bson;
using Streaming.Connections;
using Infrastructure.Protocol;
using Streaming.Services;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading;

namespace Streaming.Messages;

public class DataSyncMessageHandler : IMessageHandler
{
    public string Type => MessageTypes.DataSync;

    //private readonly IMessageProducer _producer;
    private readonly IDataSyncService _service;
    private readonly IChannelPublisher _channelPublisher;

    public DataSyncMessageHandler(IDataSyncService service, IChannelPublisher channelPublisher)
    {
        //_producer = producer;
        _service = service;
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

        // TODO: remove this check when web endpoint is ready

        // handle client sdk prerequisites
        //if (connectionContext.Type == ConnectionType.Client)
        //{
        //    // client sdk must attach user info when sync data
        //    if (message.User == null || !message.User.IsValid())
        //    {
        //        throw new ArgumentException("client sdk must attach valid user info when sync data.");
        //    }

        //    var connection = connectionContext.Connection;

        //    // attach client-side sdk EndUser
        //    connection.AttachUser(message.User);

        //    // publish end-user message
        //    var endUserMessage = new EndUserMessage(connection.EnvId, message.User);
        //    await _producer.PublishAsync(Topics.EndUser, endUserMessage);
        //}

        // publish to env topic

        using var document = JsonDocument.Parse(ctx.Data.ToString());
        var data = document.RootElement;

        // envid needs to be extracted from the token, we should also grab the message type now.

        // raw query
        //((Streaming.Connections.DefaultConnectionContext)ctx.Connection).RawQuery
        // "?type=server&token=QBQBSOr5anyLAEUBXPUPZWZXPDZXOEXt3unTvMRw6v_wkXPf5kWaC7_KQu4wbQ"

        //token
        //        ((Streaming.Connections.DefaultConnectionContext)ctx.Connection).Token
        //"QBQBSOr5anyLAEUBXPUPZWZXPDZXOEXt3unTvMRw6v_wkXPf5kWaC7_KQu4wbQ"

        //Connection Id
        //((Streaming.Connections.DefaultConnectionContext)ctx.Connection).Connection.Id
        //12ee6f46 - ff0d - 4d24 - a7bd - 792dc2a4342d

        // Connection Type
        //((Streaming.Connections.DefaultConnectionContext)ctx.Connection).Type
        //"server"

        //var token = ctx.Connection.Token
        
        //var token = new Token(tokenString.AsSpan());

        var token = new Token(ctx.Connection.Token.AsSpan());   

        
        
        // push change messages to sdk
        var envId = ctx.Connection.Connection.EnvId.ToString();

        var channelId = Infrastructure.BackplaneMesssages.Channels.GetEdgeChannel(envId.ToString()).Replace("featbit-els-", "featbit:els:");

        await _channelPublisher.PublishAsync(channelId, message);
        //await _producer.PublishAsync(channelId, serverMessage, cancellationToken);

        //var payload = await _service.GetPayloadAsync(connectionContext, message);
        //var serverMessage = new ServerMessage(MessageTypes.DataSync, payload);
        //await connectionContext.SendAsync(serverMessage, ctx.CancellationToken);
    }
}