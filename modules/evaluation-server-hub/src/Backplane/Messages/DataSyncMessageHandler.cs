using Domain.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Domain.BackplaneMesssages;
using Backplane.Messages;
using Backplane.Protocol;
using Backplane.Services;


namespace Backplane.Messages
{
    public class DataSyncMessageHandler : IMessageHandler
    {
        public string Type => "data-sync";

        private readonly IDataSyncService _service;
        private readonly IChannelProducer _channelMessageProducer;

        public DataSyncMessageHandler(
            IDataSyncService service,
            IChannelProducer channelMessageProducer)
        {
            _service = service;
            _channelMessageProducer = channelMessageProducer;
        }

        public async Task HandleAsync(EdgeMessage edgeMessage)
        {
            var payload = await _service.GetPayloadAsync(edgeMessage);
            var serverMessage = new ServerMessage(Type, payload);

            var envId = edgeMessage.EnvId;
            var channelId = Domain.BackplaneMesssages.Channels.GetEnvironmentChannel(envId.ToString());
            await _channelMessageProducer.PublishAsync(channelId, serverMessage);
        }


    }
}
