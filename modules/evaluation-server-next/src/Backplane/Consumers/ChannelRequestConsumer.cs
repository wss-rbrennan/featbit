////using Infrastructure.Scaling.Manager;
//using Backplane.Messages;
//using Backplane.Services;
//using Infrastructure.BackplaneMesssages;
//using Domain.Messages;
//using Infrastructure.Protocol;
//using Microsoft.Extensions.Logging;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Text.Json;
//using System.Threading.Tasks;
//using Infrastructure.Scaling.Types;

//namespace Backplane.Consumers
//{
//    public class ChannelRequestConsumer : IChannelConsumer
//    {
//        public string Channel => Infrastructure.BackplaneMesssages.Channels.EnvironmentPattern;

//        public string Type => "data-sync";

//        string IChannelConsumer.Channel { get => Channel; set => throw new NotImplementedException(); }

//        private readonly IDataSyncService _dataSyncService;
//        private readonly ILogger<ChannelRequestConsumer> _logger;

//        public ChannelRequestConsumer(ILogger<ChannelRequestConsumer> logger, IDataSyncService dataSyncService)
//        {
//            _logger = logger;
//            _dataSyncService = dataSyncService;
//        }

//        //public Task HandleAsync(BackplaneMessage message)
//        //{
//        //    //if (message is EnvironmentRequestMessage environmentRequestMessage)
//        //    //{
//        //    //    // Process the environment request message
//        //    //    // For example, log the request or perform some action based on the request
//        //    //    Console.WriteLine($"Received environment request: {environmentRequestMessage.RequestDetails}");
//        //    //}
//        //    //else
//        //    //{
//        //    //    Console.WriteLine("Received an unsupported message type.");
//        //    //}
//        //    return Task.CompletedTask;
//        //}

//        public async Task HandleAsync(MessageContext message, CancellationToken cancellationToken)
//        {
//            using var document = JsonDocument.Parse(message);
//            var root = document.RootElement;
//            if (!root.TryGetProperty("segment", out var segment) ||
//                !root.TryGetProperty("affectedFlagIds", out var affectedFlagIds))
//            {
//                throw new InvalidDataException("invalid segment change data");
//            }

//            //// push change messages to sdk
//            //var envId = segment.GetProperty("envId").GetGuid();
//            //var flagIds = affectedFlagIds.Deserialize<string[]>()!;

//            var payload = await _dataSyncService.GetPayload(message);
//            //var serverMessage = new ServerMessage(MessageTypes.DataSync, payload);

//            //await connection.SendAsync(serverMessage, cancellationToken);
//        }

//    }
//}
