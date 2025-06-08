using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Backplane.Redis
{
    public partial class RedisChannelConsumer
    {
        public static partial class Log
        {
            [LoggerMessage(1, LogLevel.Warning, "No message handler for channel: {Channel}", EventName = "NoHandlerForChannel")]
            public static partial void NoHandlerForChannel(ILogger logger, string channel);

            [LoggerMessage(2, LogLevel.Debug, "Channel Message {channelMessage} was handled successfully.", EventName = "ChannelMessageHandled")]
            public static partial void ChannelMessageHandled(ILogger logger, string channelMessage);

            [LoggerMessage(3, LogLevel.Error, "Exception occurred while handling message: {channelMessage}.", EventName = "ErrorHandlingChannelMessage")]
            public static partial void ErrorHandlingChannelMessage(ILogger logger, string channelMessage, Exception exception);
        }
    }
}
