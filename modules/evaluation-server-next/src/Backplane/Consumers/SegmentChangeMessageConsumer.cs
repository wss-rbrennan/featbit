using Infrastructure.Channels;
//using Backplane.Connections;
//using Backplane.EdgeConnections;
using Backplane.Messages;
using Infrastructure.Protocol;
using Backplane.Services;
using Domain.Messages;
using Domain.Shared;
//using Infrastructure.Scaling.Manager;
using System.Text.Json;

namespace Backplane.Consumers;

public class SegmentChangeMessageConsumer : IMessageConsumer
{
    public string Topic => Topics.SegmentChange;

    private readonly IDataSyncService _dataSyncService;

    private readonly IChannelPublisher _channelPublisher;


    public SegmentChangeMessageConsumer(IDataSyncService dataSyncService, IChannelPublisher channelPublisher)
    {
        _dataSyncService = dataSyncService;
        _channelPublisher = channelPublisher;
    }

    public async Task HandleAsync(string message, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(message);
        var root = document.RootElement;
        if (!root.TryGetProperty("segment", out var segment) ||
            !root.TryGetProperty("affectedFlagIds", out var affectedFlagIds))
        {
            throw new InvalidDataException("invalid segment change data");
        }

        // push change messages to sdk
        var envId = segment.GetProperty("envId").GetGuid();
        var flagIds = affectedFlagIds.Deserialize<string[]>()!;

        var flagIdsList = flagIds.Select(Guid.Parse);

        var id = segment.GetProperty("envId").ToString();
        var secret = new Secret();
        //var type = ConnectionType.Server; // Assuming this is a server-side change message

       //var type = segment.TryGetProperty("type", out var typeProperty) 
       //     ? typeProperty.GetString() 
       //     : ConnectionType.Server; // Default to Server if not specified

        //var version = segment.TryGetProperty("version", out var versionProperty)
        //    ? versionProperty.GetString()
        //    : EdgeConnectionVersion.V2; // Default to V2 if not specified

        var connectAt = segment.TryGetProperty("connectAt", out var connectAtProperty) 
            ? connectAtProperty.GetInt64() 
            : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); // Default to current time if not specified
        
        //var edgeMessage = new EdgeMessage(id, secret, type, version, connectAt, flagIdsList);

        //var payload = await _dataSyncService.GetSegmentChangePayloadAsync(edgeMessage, segment, flagIds);
        //var serverMessage = new ServerMessage(MessageTypes.DataSync, payload);


        var channelId = Infrastructure.BackplaneMesssages.Channels.GetEdgeChannel(envId.ToString()).Replace("featbit-els-", "featbit:els:");

        //await _channelPublisher.PublishAsync(channelId, serverMessage);
        // TODO: Publish the response message to the backplane channel
        //await connection.SendAsync(serverMessage, cancellationToken);

    }
}