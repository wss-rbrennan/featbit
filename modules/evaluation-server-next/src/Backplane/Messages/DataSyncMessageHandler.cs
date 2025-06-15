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

            // Extract correlation information from the enhanced message context
            string? originalCorrelationId = null;
            
            if (ctx is EnhancedMessageContext enhancedCtx)
            {
                originalCorrelationId = enhancedCtx.CorrelationId;
                Console.WriteLine($"Hub processing message with correlation ID: '{originalCorrelationId}'");
            }
            
            Console.WriteLine($"Hub received message structure: {ctx.Data.ToString()}");
            
            // Parse the message data to extract the actual DataSyncMessage
            DataSyncMessage? message = null;
            using var messageDocument = JsonDocument.Parse(ctx.Data.ToString());
            var messageRoot = messageDocument.RootElement;
            
            if (messageRoot.TryGetProperty("data", out var dataElement))
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

            // Create response message preserving correlation ID for Edge-side targeting
            Message backplaneMessage;
            
            if (!string.IsNullOrEmpty(originalCorrelationId))
            {
                // Create response preserving the correlation ID for Edge-side targeting
                backplaneMessage = _messageFactory.CreateResponseMessage(
                    type: "server",
                    channelId: envId,
                    channelName: envId,
                    messageContent: JsonDocument.Parse(serverMessageJson).RootElement,
                    senderId: _serviceIdentityProvider.ServiceId, // Hub uses its service ID
                    serviceType: ServiceTypes.Hub,
                    correlationId: originalCorrelationId // Preserve correlation ID for Edge targeting
                );
                
                Console.WriteLine($"Hub creating data-sync response with correlation ID: {originalCorrelationId}");
            }
            else
            {
                // Fallback to broadcast behavior for backward compatibility
                backplaneMessage = _messageFactory.CreateMessage(
                    type: "server",
                    channelId: envId,
                    channelName: envId,
                    messageContent: JsonDocument.Parse(serverMessageJson).RootElement,
                    senderId: _serviceIdentityProvider.ServiceId,
                    serviceType: ServiceTypes.Hub
                );
                
                Console.WriteLine($"Hub creating BROADCAST data-sync response - ServiceType: {backplaneMessage.ServiceType}, SenderId: {backplaneMessage.SenderId}, CorrelationId: {backplaneMessage.CorrelationId}");
            }

            // TODO: replace the backplane channel id for Redis channel, this should use a config value
            var channelId = Infrastructure.BackplaneMesssages.Channels.GetBackplaneChannel(envId).Replace("featbit-els-backplane-", "featbit:els:backplane:");
            
            await _channelMessageProducer.PublishAsync(channelId, backplaneMessage);
        }
    }
}
