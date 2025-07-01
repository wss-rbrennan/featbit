using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Scaling.Session;

/// <summary>
/// Service registration extensions for legacy client session management
/// </summary>
public static class LegacyClientServiceExtensions
{
    /// <summary>
    /// Add legacy client session management services
    /// </summary>
    public static IServiceCollection AddLegacyClientSessionManagement(this IServiceCollection services)
    {
        // Register the legacy identifier generator
        services.AddScoped<ILegacyClientIdentifierGenerator, LegacyClientIdentifierGenerator>();
        
        // Register the enhanced session manager that wraps the base session manager
        services.AddScoped<IEnhancedClientSessionManager, EnhancedClientSessionManager>();
        
        return services;
    }
} 