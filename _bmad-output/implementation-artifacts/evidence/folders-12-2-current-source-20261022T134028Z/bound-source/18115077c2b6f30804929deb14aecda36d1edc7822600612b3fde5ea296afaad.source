using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Hexalith.EventStore.ServiceDefaults;

/// <summary>
/// Configures EventStore service-default endpoint behavior.
/// </summary>
public sealed class EventStoreServiceDefaultsOptions
{
    /// <summary>
    /// Gets or sets the development response writer used for the aggregate health and readiness endpoints.
    /// </summary>
    public Func<HttpContext, HealthReport, Task>? DevelopmentHealthResponseWriter { get; set; }
        = Extensions.WriteHealthCheckJsonResponse;
}
