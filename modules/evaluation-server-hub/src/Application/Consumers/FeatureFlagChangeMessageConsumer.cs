//using Application.Channels;
//using Application.Connections;
//using Application.Protocol;
//using Application.Services;
//using Domain.Messages;
//using MongoDB.Driver.Core.Bindings;
//using System.Text.Json;

//namespace Application.Consumers;

//public class FeatureFlagChangeMessageConsumer : IMessageConsumer
//{
//    public string Topic => Topics.FeatureFlagChange;

//    private readonly IChannelPublisher _channelPublisher;
//    private readonly IDataSyncService _dataSyncService;

//    public FeatureFlagChangeMessageConsumer(IChannelPublisher channelPublisher, IDataSyncService dataSyncService)
//    {
//        _channelPublisher = channelPublisher;
//        _dataSyncService = dataSyncService;
//    }

//    public async Task HandleAsync(string message, CancellationToken cancellationToken)
//    {
//        using var document = JsonDocument.Parse(message);
//        var flag = document.RootElement;

//        // push change messages to sdk
//        var envId = flag.GetProperty("envId").GetGuid();



//        var payload = await _dataSyncService.GetFlagChangePayloadAsync(flag);
//        var serverMessage = new ServerMessage(MessageTypes.DataSync, payload);
//        await _channelPublisher.PublishToChannelAsync(envId.ToString(), serverMessage, cancellationToken);

//    }
//}