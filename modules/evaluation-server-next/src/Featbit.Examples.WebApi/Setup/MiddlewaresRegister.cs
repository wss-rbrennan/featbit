namespace WebApi.Setup;

public static class MiddlewaresRegister
{
    public static WebApplication SetupMiddleware(this WebApplication app)
    {
        // enable cors
        app.UseCors();

        app.MapControllers();

        return app;
    }
}