using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http;

namespace Backplane
{
    public class BackplaneMiddleware
    {
        private readonly IHostApplicationLifetime _applicationLifetime;
        private readonly RequestDelegate _next;

        public BackplaneMiddleware(IHostApplicationLifetime applicationLifetime, RequestDelegate next)
        {
            _applicationLifetime = applicationLifetime;
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            await _next(context);
        }
    }
}
