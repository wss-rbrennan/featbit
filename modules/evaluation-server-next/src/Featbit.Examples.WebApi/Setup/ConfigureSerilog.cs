using Serilog;
using Serilog.Settings.Configuration;


namespace WebApi.Setup;

public static class ConfigureSerilog
{
    public static void Configure(LoggerConfiguration lc, IConfiguration configuration)
    {
        var readerOptions = new ConfigurationReaderOptions
        {
            SectionName = "Logging"
        };

        lc
          .ReadFrom.Configuration(configuration, readerOptions)
          .Enrich.FromLogContext();
    }
}