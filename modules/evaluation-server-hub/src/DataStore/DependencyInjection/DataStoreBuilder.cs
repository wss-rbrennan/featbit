using Microsoft.Extensions.DependencyInjection;

namespace DataStore.DependencyInjection;

public class DataStoreBuilder : IDataStoreBuilder
{
    public IServiceCollection Services { get; }

    /// <summary>
    /// Initializes a new <see cref="StreamingBuilder"/> instance.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    public DataStoreBuilder(IServiceCollection services)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }
}