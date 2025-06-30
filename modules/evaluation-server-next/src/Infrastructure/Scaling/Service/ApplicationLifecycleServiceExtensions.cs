using Microsoft.Extensions.DependencyInjection;
using Infrastructure.Scaling.Metrics;

namespace Infrastructure.Scaling.Service
{
    /// <summary>
    /// Extension methods for adding ApplicationLifecycleService
    /// </summary>
    public static class ApplicationLifecycleServiceExtensions
    {
        /// <summary>
        /// Adds application lifecycle monitoring and metrics to the service collection
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddApplicationLifecycleMonitoring(this IServiceCollection services)
        {
            services.AddSingleton<IApplicationLifecycleMetrics, ApplicationLifecycleMetrics>();
            return services.AddHostedService<ApplicationLifecycleService>();
        }
    }
} 