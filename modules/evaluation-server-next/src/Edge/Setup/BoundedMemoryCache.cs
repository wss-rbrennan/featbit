using Microsoft.Extensions.Caching.Memory;

namespace Edge.Setup;

public class BoundedMemoryCache
{
    public MemoryCache Instance { get; } = new(
        new MemoryCacheOptions
        {
            SizeLimit = 1024 * 1024,
            ExpirationScanFrequency = TimeSpan.FromMinutes(1)
        });
}