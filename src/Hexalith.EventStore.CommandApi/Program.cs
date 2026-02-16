using Hexalith.EventStore.CommandApi.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddDaprClient();
builder.Services.AddHealthChecks()
    .AddEventStoreDaprHealthChecks();
builder.Services.AddCommandApi();
builder.Services.AddEventStoreServer(builder.Configuration);

var app = builder.Build();

// Middleware order per architecture: CorrelationIdMiddleware FIRST (before everything)
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.MapDefaultEndpoints();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

// OpenAPI/Swagger UI (gated by configuration, H13)
if (app.Configuration.GetValue("EventStore:OpenApi:Enabled", true))
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Hexalith EventStore API v1");
        options.RoutePrefix = "swagger";
    });
}

app.MapControllers();
app.MapActorsHandlers();

// Configure global request body size limit (1MB)
app.Lifetime.ApplicationStarted.Register(() =>
{
    var kestrelOptions = app.Services.GetService<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>();
    if (kestrelOptions != null)
    {
        kestrelOptions.Limits.MaxRequestBodySize = 1_048_576;
    }
});

app.Run();

/// <summary>
/// Entry point class, made partial for WebApplicationFactory test access.
/// </summary>
public partial class Program;
