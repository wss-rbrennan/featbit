using Backplane.Messages;
using Application.Services;
using Domain.EndUsers;
using Domain.Messages;
using Domain.Shared;
using Infrastructure.BackplaneMesssages;
using Infrastructure.Scaling.Handlers;
using Infrastructure.Connections;
using Infrastructure.Protocol;
using Infrastructure.Scaling.Types;
using Infrastructure.Scaling.Service;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;


namespace Backplane.Messages
{
    public class DataSyncMessageHandler : IMessageHandler
    {
        public string Type => "data-sync";

        private readonly IDataSyncService _service;
        private readonly IChannelProducer _channelMessageProducer;
        private readonly IMessageProducer _producer;
        private readonly IMessageFactory _messageFactory;
        private readonly IServiceIdentityProvider _serviceIdentityProvider;

        public DataSyncMessageHandler(
            IDataSyncService service,
            IChannelProducer channelMessageProducer,
            IMessageProducer producer,
            IMessageFactory messageFactory,
            IServiceIdentityProvider serviceIdentityProvider)
        {
            _service = service;
            _channelMessageProducer = channelMessageProducer;
            _producer = producer;
            _messageFactory = messageFactory;
            _serviceIdentityProvider = serviceIdentityProvider;
        }

        public async Task HandleAsync(MessageContext ctx)
        {
            var connectionContext = ctx.Connection;

            // First, parse the JSON to extract the "data" property
            using var document = JsonDocument.Parse(ctx.Data.ToString());
            var root = document.RootElement;
            
            DataSyncMessage? message = null;
            if (root.TryGetProperty("data", out var dataElement))
            {
                // Deserialize the "data" property to DataSyncMessage
                message = dataElement.Deserialize<DataSyncMessage>(ReusableJsonSerializerOptions.Web);
            }
            else
            {
                // Fallback: try to deserialize the entire JSON as DataSyncMessage (for backward compatibility)
                message = ctx.Data.Deserialize<DataSyncMessage>(ReusableJsonSerializerOptions.Web);
            }
            
            if (message == null)
            {
                return;
            }

            // handle client sdk prerequisites
            if (connectionContext.Type == ConnectionType.Client)
            {
                // client sdk must attach user info when sync data
                if (message.User == null || !message.User.IsValid())
                {
                    throw new ArgumentException("client sdk must attach valid user info when sync data.");
                }

                var connection = connectionContext.Connection;

                // attach client-side sdk EndUser
                connection.AttachUser(message.User);

                // publish end-user message
                var endUserMessage = new EndUserMessage(connection.EnvId, message.User);
                await _producer.PublishAsync(Topics.EndUser, endUserMessage);
            }

            var payload = await _service.GetPayloadAsync(connectionContext, message);
            var serverMessage = new ServerMessage(MessageTypes.DataSync, payload);

            var serverMessageJson = JsonSerializer.Serialize<ServerMessage>(serverMessage, JsonSerializerOptions.Web);
            var envId = connectionContext.Connection.EnvId.ToString();

            // Check if this is a response to an Edge message (has correlation context)
            // For now, we'll create a new message since we don't have the original Edge message context
            // In a full implementation, you'd want to extract the original correlation ID from the context
            var backplaneMessage = _messageFactory.CreateMessage(
                type: "server",
                channelId: envId,
                channelName: envId,
                messageContent: JsonDocument.Parse(serverMessageJson).RootElement,
                senderId: _serviceIdentityProvider.ServiceId, // Hub uses its service ID
                serviceType: ServiceTypes.Hub
            );

            // TODO: replace the backplane channel id for Redis channel, this should use a config value
            var channelId = Infrastructure.BackplaneMesssages.Channels.GetBackplaneChannel(envId).Replace("featbit-els-backplane-", "featbit:els:backplane:");
            
            // Log the message creation with correlation info
            Console.WriteLine($"Hub creating data-sync response - ServiceType: {backplaneMessage.ServiceType}, SenderId: {backplaneMessage.SenderId}, CorrelationId: {backplaneMessage.CorrelationId}");
            
            await _channelMessageProducer.PublishAsync(channelId, backplaneMessage);
        }
    }
}
