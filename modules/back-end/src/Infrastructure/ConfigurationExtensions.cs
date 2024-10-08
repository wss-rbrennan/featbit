﻿using Microsoft.Extensions.Configuration;

namespace Infrastructure;

public static class ConfigurationExtensions
{
    public static bool IsProVersion(this IConfiguration configuration)
    {
        var isPro = configuration["IS_PRO"];

        return string.Equals(isPro, bool.TrueString, StringComparison.OrdinalIgnoreCase);
    }
}