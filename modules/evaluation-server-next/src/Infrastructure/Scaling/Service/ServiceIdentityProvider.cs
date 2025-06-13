using Microsoft.Extensions.Logging;

namespace Infrastructure.Scaling.Service
{
    /// <summary>
    /// Provides a unique identifier for the service instance
    /// </summary>
    public interface IServiceIdentityProvider
    {
        /// <summary>
        /// Gets the unique identifier for this service instance
        /// </summary>
        string ServiceId { get; }
    }

    /// <summary>
    /// Implementation of service identity provider that generates a unique GUID on startup
    /// </summary>
    public class ServiceIdentityProvider : IServiceIdentityProvider
    {
        private readonly ILogger<ServiceIdentityProvider> _logger;

        public string ServiceId { get; }

        public ServiceIdentityProvider(ILogger<ServiceIdentityProvider> logger)
        {
            _logger = logger;
            ServiceId = Guid.NewGuid().ToString();
            _logger.LogInformation("Service identity initialized with ID: {ServiceId}", ServiceId);
        }
    }
} 