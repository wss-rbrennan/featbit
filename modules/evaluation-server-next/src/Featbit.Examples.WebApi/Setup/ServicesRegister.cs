using Microsoft.Extensions.DependencyInjection;
using Serilog;
using FeatBit.Sdk.Server.DependencyInjection;
using WebApi.Options;

namespace WebApi.Setup;

public static class ServicesRegister
{
    public static WebApplicationBuilder RegisterServices(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        var configuration = builder.Configuration;

        services.AddControllers();

        // serilog
        builder.Services.AddSerilog((_, lc) => ConfigureSerilog.Configure(lc, builder.Configuration));



        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        services.AddEndpointsApiExplorer();
        
        // cors
        builder.Services.AddCors(options => options.AddDefaultPolicy(policyBuilder =>
        {
            policyBuilder
                .AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        }));

        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        services.AddOpenApi();

        // add FeatBit service
        var fbOptions = builder.Configuration.GetSection("FeatBit").Get<FeatbitOptions>();
        services.AddFeatBit(options =>
        {
            options.EnvSecret = fbOptions.ServerKey;
            options.StreamingUri = new Uri(fbOptions.StreamingServerUri);
            options.EventUri = new Uri(fbOptions.EventServerUri);
            options.StartWaitTime = TimeSpan.FromSeconds(fbOptions.StartWaitTimeout);
            options.ConnectTimeout = TimeSpan.FromSeconds(fbOptions.ConnectTimeout);
            options.LoggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            options.ReconnectRetryDelays = new[] { TimeSpan.FromMilliseconds(1000) };
        });




        return builder;
    }
}