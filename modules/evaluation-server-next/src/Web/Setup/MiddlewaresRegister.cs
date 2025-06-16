using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Infrastructure;
using Microsoft.AspNetCore.Builder;

namespace Web.Setup;

public static class MiddlewaresRegister
{
    public static WebApplication SetupMiddleware(this WebApplication app)
    {
        // reference: https://andrewlock.net/deploying-asp-net-core-applications-to-kubernetes-part-6-adding-health-checks-with-liveness-readiness-and-startup-probes/
        // health check endpoints for external use
        app.MapHealthChecks("health/liveness", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("health/readiness", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(HealthCheckBuilderExtensions.ReadinessTag)
        });

        // enable swagger in dev mode
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwagger();
        }

        // enable cors
        app.UseCors();

        app.MapControllers();

        return app;
    }

    public static WebApplication LogServerUrls(this WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        
        // Log the configured URLs
        var urls = app.Configuration.GetSection("Kestrel:EndPoints")
            .GetChildren()
            .Select(endpoint => endpoint.GetValue<string>("Url"))
            .Where(url => !string.IsNullOrEmpty(url))
            .ToList();

        if (urls.Any())
        {
            logger.LogInformation("Web server starting on configured URLs: {Urls}", string.Join(", ", urls));
        }
        else
        {
            // Log the default URLs if no Kestrel configuration
            var defaultUrls = app.Configuration.GetValue<string>("urls") ?? "http://localhost:5000;https://localhost:5001";
            logger.LogInformation("Web server starting on default URLs: {Urls}", defaultUrls);
        }

        return app;
    }
}