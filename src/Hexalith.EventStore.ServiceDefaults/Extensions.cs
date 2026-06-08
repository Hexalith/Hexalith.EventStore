using Hexalith.Commons.ServiceDefaults;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace Hexalith.EventStore.ServiceDefaults;

// Adds common Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
// This project should be referenced by each service project in your solution.
// To learn more about using this project, see https://aka.ms/dotnet/aspire/service-defaults
public static class Extensions {
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";
    private const string ReadinessEndpointPath = "/ready";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
        => builder.AddHexalithServiceDefaults(ConfigureEventStoreDefaults);

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
        => builder.ConfigureHexalithOpenTelemetry(ConfigureEventStoreDefaults);

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
        => builder.AddHexalithDefaultHealthChecks(ConfigureEventStoreDefaults);

    /// <summary>
    /// Writes a detailed JSON health check response for development environments.
    /// </summary>
    public static Task WriteHealthCheckJsonResponse(HttpContext httpContext, HealthReport healthReport)
        => HexalithServiceDefaults.WriteDevelopmentHealthJsonResponseAsync(httpContext, healthReport);

    public static bool ShouldTraceHttpRequest(HttpContext httpContext)
        => HexalithServiceDefaults.ShouldTraceHttpRequest(httpContext, ConfigureEventStoreDefaults);

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
        => app.MapHexalithDefaultEndpoints(ConfigureEventStoreDefaults);

    private static void ConfigureEventStoreDefaults(HexalithServiceDefaultsOptions options) {
        options.ActivitySourceNames.Add("Hexalith.EventStore");
        options.ActivitySourceNames.Add("Microsoft.AspNetCore.SignalR.Server");
        options.ActivitySourceNames.Add("Microsoft.AspNetCore.SignalR.Client");
        options.HealthEndpointPath = HealthEndpointPath;
        options.LivenessEndpointPath = AlivenessEndpointPath;
        options.ReadinessEndpointPath = ReadinessEndpointPath;
    }
}
