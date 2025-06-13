using Backplane.Messages;
using Infrastructure.Protocol;
using Backplane.Services;
using Infrastructure.Channels;
using Infrastructure.BackplaneMesssages;
using Domain.Messages;
using MongoDB.Driver.Core.Bindings;
using System.Text.Json;
using Infrastructure.Scaling.Types;
using Infrastructure.Scaling.Service;
using Domain.Shared;

namespace Backplane.Consumers;

public class FeatureFlagChangeMessageConsumer : IMessageConsumer
{
    public string Topic => Topics.FeatureFlagChange;

    private readonly IChannelPublisher _channelPublisher;
    private readonly IDataSyncService _dataSyncService;
    private readonly IMessageFactory _messageFactory;
    private readonly IServiceIdentityProvider _serviceIdentityProvider;

    public FeatureFlagChangeMessageConsumer(
        IChannelPublisher channelPublisher, 
        IDataSyncService dataSyncService,
        IMessageFactory messageFactory,
        IServiceIdentityProvider serviceIdentityProvider)
    {
        _channelPublisher = channelPublisher;
        _dataSyncService = dataSyncService;
        _messageFactory = messageFactory;
        _serviceIdentityProvider = serviceIdentityProvider;
    }

    public async Task HandleAsync(string message, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(message);
        var flag = document.RootElement;

        var envId = flag.GetProperty("envId").GetGuid();

        var payload = await _dataSyncService.GetFlagChangePayloadAsync(flag);
        var serverMessage = new ServerMessage(MessageTypes.DataSync, payload);
        var serverMessageJson = JsonSerializer.Serialize(serverMessage, ReusableJsonSerializerOptions.Web);

        var channelId = Infrastructure.BackplaneMesssages.Channels.GetBackplaneChannel(envId.ToString()).Replace("featbit-els-backplane-", "featbit:els:backplane:");

        var backplaneMessage = _messageFactory.CreateMessage(
            type: "server",
            channelId: envId.ToString(),
            channelName: envId.ToString(),
            messageContent: JsonDocument.Parse(serverMessageJson).RootElement,
            senderId: _serviceIdentityProvider.ServiceId,
            serviceType: ServiceTypes.Hub
        );

        Console.WriteLine($"Hub sending flag change notification to Edge - ServiceType: {backplaneMessage.ServiceType}, SenderId: {backplaneMessage.SenderId}, CorrelationId: {backplaneMessage.CorrelationId}, EnvId: {envId}");

        await _channelPublisher.PublishAsync(channelId, backplaneMessage);
    }
}