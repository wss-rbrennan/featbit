using Microsoft.AspNetCore.Builder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backplane
{
    public static class BackplaneMiddlewareExtension
    {
        public static IApplicationBuilder UseBackplane(this IApplicationBuilder builder)
        {
            return builder
                .UseMiddleware<BackplaneMiddleware>();
        }
    }
}
