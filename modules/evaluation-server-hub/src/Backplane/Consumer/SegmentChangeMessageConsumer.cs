using System.Text.Json;
using Domain.Messages;
using Backplane.Protocol;
using Infrastructure.Scaling.Manager;
using Backplane.Services;
using Backplane.Messages;
using Domain.Shared;

namespace Backplane.Consumers;

public class SegmentChangeMessageConsumer : IMessageConsumer
{
    public string Topic => Topics.SegmentChange;

    private readonly IDataSyncService _dataSyncService;

    public SegmentChangeMessageConsumer(IDataSyncService dataSyncService)
    {
        _dataSyncService = dataSyncService;
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
        var type = segment.GetProperty("type").ToString();
        var version = segment.GetProperty("version").ToString();
        var connectAt = segment.GetProperty("connectAt").GetInt64();
        
        var edgeMessage = new EdgeMessage(id, secret, type, version, connectAt, flagIdsList);

        var payload = await _dataSyncService.GetSegmentChangePayloadAsync(edgeMessage, segment, flagIds);
        var serverMessage = new ServerMessage(MessageTypes.DataSync, payload);

        // TODO: Publish the response message to the backplane channel
        //await connection.SendAsync(serverMessage, cancellationToken);

    }
}