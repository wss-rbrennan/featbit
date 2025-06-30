using Microsoft.Extensions.DependencyInjection;
using Infrastructure.Scaling.Metrics;

namespace Infrastructure.Scaling.Service
{
    /// <summary>
    /// Extension methods for adding ApplicationShutdownService
    /// </summary>
    public static class ApplicationShutdownServiceExtensions
    {
        /// <summary>
        /// Adds application shutdown monitoring and lifecycle metrics to the service collection
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddApplicationShutdownMonitoring(this IServiceCollection services)
        {
            services.AddSingleton<IApplicationLifecycleMetrics, ApplicationLifecycleMetrics>();
            return services.AddHostedService<ApplicationShutdownService>();
        }
    }
} 