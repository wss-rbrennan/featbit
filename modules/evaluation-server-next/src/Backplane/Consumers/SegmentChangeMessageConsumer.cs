using Infrastructure.Channels;
using Backplane.Messages;
using Infrastructure.Protocol;
using Application.Services;
using Domain.Messages;
using Domain.Shared;
using Infrastructure.Scaling.Service;
using Infrastructure.Scaling.Types;
using System.Text.Json;

namespace Backplane.Consumers;

public class SegmentChangeMessageConsumer : IMessageConsumer
{
    public string Topic => Topics.SegmentChange;

    private readonly IDataSyncService _dataSyncService;
    private readonly IChannelPublisher _channelPublisher;
    private readonly IMessageFactory _messageFactory;
    private readonly IServiceIdentityProvider _serviceIdentityProvider;

    public SegmentChangeMessageConsumer(
        IDataSyncService dataSyncService, 
        IChannelPublisher channelPublisher,
        IMessageFactory messageFactory,
        IServiceIdentityProvider serviceIdentityProvider)
    {
        _dataSyncService = dataSyncService;
        _channelPublisher = channelPublisher;
        _messageFactory = messageFactory;
        _serviceIdentityProvider = serviceIdentityProvider;
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
        var connectAt = segment.TryGetProperty("connectAt", out var connectAtProperty) 
            ? connectAtProperty.GetInt64() 
            : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); // Default to current time if not specified
        
        // Create a basic payload for segment change notification
        var payload = new
        {
            messageType = "segment-change",
            data = new
            {
                envId = envId,
                segmentId = segment.GetProperty("id").GetString(),
                affectedFlagIds = flagIds,
                timestamp = connectAt
            }
        };

        var serverMessage = new ServerMessage(MessageTypes.DataSync, payload);
        var serverMessageJson = JsonSerializer.Serialize(serverMessage, ReusableJsonSerializerOptions.Web);

        var channelId = Infrastructure.BackplaneMesssages.Channels.GetBackplaneChannel(envId.ToString()).Replace("featbit-els-backplane-", "featbit:els:backplane:");

        // Use MessageFactory to create message with proper correlation and sender IDs
        var backplaneMessage = _messageFactory.CreateMessage(
            type: "server",
            channelId: envId.ToString(),
            channelName: envId.ToString(),
            messageContent: JsonDocument.Parse(serverMessageJson).RootElement,
            senderId: _serviceIdentityProvider.ServiceId, // Hub uses its service ID for segment change notifications
            serviceType: ServiceTypes.Hub
        );

        // Log the message creation with correlation info
        Console.WriteLine($"Hub sending segment change notification to Edge - ServiceType: {backplaneMessage.ServiceType}, SenderId: {backplaneMessage.SenderId}, CorrelationId: {backplaneMessage.CorrelationId}, EnvId: {envId}");

        await _channelPublisher.PublishAsync(channelId, backplaneMessage);
    }
}