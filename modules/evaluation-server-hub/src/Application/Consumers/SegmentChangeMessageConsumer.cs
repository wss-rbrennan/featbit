using System.Text.Json;
using Domain.Messages;
using Application.Connections;
using Application.Protocol;
using Infrastructure.Scaling.Manager;
using Backplane.Services;


namespace Application.Consumers;

public class SegmentChangeMessageConsumer : IMessageConsumer
{
    public string Topic => Topics.SegmentChange;

    private readonly IDataSyncService _dataSyncService;

    public SegmentChangeMessageConsumer(RedisManager redisManager, IDataSyncService dataSyncService)
    {
        _dataSyncService = dataSyncService;
    }

    public async Task HandleAsync(string message, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("SegmentChangeMessageConsumer is not implemented yet.");
        //using var document = JsonDocument.Parse(message);
        //var root = document.RootElement;
        //if (!root.TryGetProperty("segment", out var segment) ||
        //    !root.TryGetProperty("affectedFlagIds", out var affectedFlagIds))
        //{
        //    throw new InvalidDataException("invalid segment change data");
        //}

        //// push change messages to sdk
        //var envId = segment.GetProperty("envId").GetGuid();
        //var flagIds = affectedFlagIds.Deserialize<string[]>()!;



        //    var payload = await _dataSyncService.GetSegmentChangePayloadAsync(segment, flagIds);
        //    var serverMessage = new ServerMessage(MessageTypes.DataSync, payload);

        //    await connection.SendAsync(serverMessage, cancellationToken);

    }
}