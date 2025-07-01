using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Scaling.Session;

/// <summary>
/// Extension methods for registering client session services
/// </summary>
public static class ClientSessionServiceExtensions
{
    /// <summary>
    /// Adds client session management services to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddClientSessionManagement(this IServiceCollection services)
    {
        services.AddSingleton<IClientSessionStore, RedisClientSessionStore>();
        services.AddSingleton<IClientSessionManager, ClientSessionManager>();
        
        return services;
    }
    
    /// <summary>
    /// Adds enhanced client session management services with legacy client support
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddEnhancedClientSessionManagement(this IServiceCollection services)
    {
        // Add base session management first
        services.AddClientSessionManagement();
        
        // Add legacy client support
        services.AddLegacyClientSessionManagement();
        
        return services;
    }
} 