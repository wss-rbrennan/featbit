using Backplane.Messages;
using Backplane.Services;
using Domain.EndUsers;
using Domain.Messages;
using Domain.Shared;
using Infrastructure.BackplaneMesssages;
using Infrastructure.Scaling.Handlers;
using Infrastructure.Connections;
using Infrastructure.Protocol;
using Infrastructure.Scaling.Types;
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

        public DataSyncMessageHandler(
            IDataSyncService service,
            IChannelProducer channelMessageProducer,
            IMessageProducer producer)
        {
            _service = service;
            _channelMessageProducer = channelMessageProducer;
            _producer = producer;
        }

        public async Task HandleAsync(MessageContext ctx)
        {

            var connectionContext = ctx.Connection;

            var message = ctx.Data.Deserialize<DataSyncMessage>(ReusableJsonSerializerOptions.Web);
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

            var backplaneMessage = new Message
            {
                ChannelId = envId,
                Type = "server",
                ChannelName = envId,
                MessageContent = JsonDocument.Parse(serverMessageJson).RootElement
            };

            // TODO: replace the backplane channel id for Redis channel, this should use a config value
            var channelId = Infrastructure.BackplaneMesssages.Channels.GetBackplaneChannel(envId).Replace("featbit-els-backplane-", "featbit:els:bankplane:");
            await _channelMessageProducer.PublishAsync(channelId, backplaneMessage);

        }
    }
}
