using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Infrastructure;
using Backplane;
using System.Diagnostics.Metrics;
using System.Text;

namespace Api.Setup;

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

        // Add metrics endpoint for Prometheus scraping
        app.MapGet("/metrics", async context =>
        {
            var meterFactory = context.RequestServices.GetRequiredService<IMeterFactory>();
            var response = new StringBuilder();
            
            // For now, return a simple response indicating metrics are available
            // The actual metrics will be collected by OTEL auto-instrumentation
            response.AppendLine("# HELP featbit_hub_metrics_available FeatBit Hub metrics endpoint");
            response.AppendLine("# TYPE featbit_hub_metrics_available gauge");
            response.AppendLine("featbit_hub_metrics_available 1");
            
            context.Response.ContentType = "text/plain; charset=utf-8";
            await context.Response.WriteAsync(response.ToString());
        });

        // enable swagger in dev mode
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        // enable backplane
        app.UseBackplane();

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
            logger.LogInformation("Hub server starting on configured URLs: {Urls}", string.Join(", ", urls));
        }
        else
        {
            // Log the default URLs if no Kestrel configuration
            var defaultUrls = app.Configuration.GetValue<string>("urls") ?? "http://localhost:5000;https://localhost:5001";
            logger.LogInformation("Hub server starting on default URLs: {Urls}", defaultUrls);
        }

        return app;
    }
}