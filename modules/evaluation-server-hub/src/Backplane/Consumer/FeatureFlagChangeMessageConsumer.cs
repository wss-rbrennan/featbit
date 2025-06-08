using Backplane.Messages;
using Backplane.Protocol;
using Backplane.Services;
using Backplane.Channels;
using Domain.BackplaneMesssages;
using Domain.Messages;
using MongoDB.Driver.Core.Bindings;
using System.Text.Json;
using Streaming.Protocol;

namespace Backplane.Consumers;

public class FeatureFlagChangeMessageConsumer : IMessageConsumer
{
    public string Topic => Topics.FeatureFlagChange;

    private readonly IChannelPublisher _channelPublisher;
    private readonly IDataSyncService _dataSyncService;

    public FeatureFlagChangeMessageConsumer(IChannelPublisher channelPublisher, IDataSyncService dataSyncService)
    {
        _channelPublisher = channelPublisher;
        _dataSyncService = dataSyncService;
    }

    public async Task HandleAsync(string message, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(message);
        var flag = document.RootElement;

        var envId = flag.GetProperty("envId").GetGuid();

        var payload = await _dataSyncService.GetFlagChangePayloadAsync(flag);
        var serverMessage = new ServerMessage(MessageTypes.DataSync, payload);
        
        var channelId = Domain.BackplaneMesssages.Channels.GetEnvironmentChannel(envId.ToString()).Replace("featbit-els-", "featbit:els:");

        await _channelPublisher.PublishToChannelAsync(channelId, serverMessage, cancellationToken);
        
    }
}