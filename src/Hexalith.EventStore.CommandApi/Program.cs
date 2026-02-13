using Hexalith.EventStore.CommandApi.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddCommandApi();

var app = builder.Build();

// Middleware order per architecture: CorrelationIdMiddleware FIRST (before everything)
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.MapDefaultEndpoints();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

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
