using Backplane.Protocol;
using Infrastructure.Scaling.Manager;
using Backplane.Services;
using Domain.BackplaneMesssages;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Backplane.Consumer
{
    public class EnvironmentRequestConsumer : IChannelConsumer
    {
        public string Channel => Domain.BackplaneMesssages.Channels.EnvironmentPattern;

        string IChannelConsumer.Channel { get => Channel; set => throw new NotImplementedException(); }

        private readonly RedisManager _redisManager;
        private readonly IDataSyncService _dataSyncService;
        private readonly ILogger<EnvironmentRequestConsumer> _logger;

        public EnvironmentRequestConsumer(ILogger<EnvironmentRequestConsumer> logger, RedisManager redisManager, IDataSyncService dataSyncService)
        {
            _logger = logger;
            _redisManager = redisManager;
            _dataSyncService = dataSyncService;
        }

        //public Task HandleAsync(BackplaneMessage message)
        //{
        //    //if (message is EnvironmentRequestMessage environmentRequestMessage)
        //    //{
        //    //    // Process the environment request message
        //    //    // For example, log the request or perform some action based on the request
        //    //    Console.WriteLine($"Received environment request: {environmentRequestMessage.RequestDetails}");
        //    //}
        //    //else
        //    //{
        //    //    Console.WriteLine("Received an unsupported message type.");
        //    //}
        //    return Task.CompletedTask;
        //}

        public async Task HandleAsync(string message, CancellationToken cancellationToken)
        {
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

            //var payload = await _dataSyncService.GetSegmentChangePayloadAsync(segment, flagIds);
            //var serverMessage = new ServerMessage(MessageTypes.DataSync, payload);

            //await connection.SendAsync(serverMessage, cancellationToken);
        }
    }
}
