using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backplane.Providers.Redis
{
    public partial class RedisChannelProducer
    {
        public static partial class Log
        {
            [LoggerMessage(1, LogLevel.Debug, "Channel message {message} was published successfully.",
                EventName = "ChannelMessagePublished")]
            public static partial void ChannelMessagePublished(ILogger<RedisChannelProducer> logger, string message);

            [LoggerMessage(2, LogLevel.Error, "Exception occurred while publishing message.",
                EventName = "ErrorPublishingChannelMessage")]
            public static partial void ErrorPublishingChannelMessage(ILogger logger, Exception exception);
        }
    }
}
