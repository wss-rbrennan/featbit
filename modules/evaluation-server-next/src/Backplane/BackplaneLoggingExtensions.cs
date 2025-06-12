using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backplane
{
    internal static partial class BackplaneLoggingExtensions
    {
        [LoggerMessage(3, LogLevel.Error, "Exception occurred while validating request: {request}.",
        EventName = "ErrorValidateRequest")]
        public static partial void ErrorValidateRequest(this ILogger logger, string? request, Exception ex);
    }
}
