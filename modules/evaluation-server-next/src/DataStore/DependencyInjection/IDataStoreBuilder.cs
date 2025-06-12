using Microsoft.Extensions.DependencyInjection;

namespace DataStore.DependencyInjection;

/// <summary>
/// An interface for configuring Streaming services.
/// </summary>
public interface IDataStoreBuilder
{
    /// <summary>
    /// Gets the <see cref="IServiceCollection"/> where Streaming services are configured.
    /// </summary>
    IServiceCollection Services { get; }
}