
using Microsoft.Extensions.Configuration;
using Backplane.Providers;

namespace Backplane;

public static class ConfigurationExtensions
{
    public static string GetBackplaneProvider(this IConfiguration configuration)
    {
        var provider = configuration.GetValue(BackplaneProvider.SectionName, BackplaneProvider.Redis)!;
        return provider;
    }
}