namespace Hexalith.EventStore.Admin.Server.Configuration;

/// <summary>
/// Configuration options for the Admin.Server DAPR-backed service implementations.
/// Bound from configuration section "AdminServer".
/// </summary>
public sealed class AdminServerOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "AdminServer";

    /// <summary>
    /// Gets or sets the DAPR state store component name.
    /// </summary>
    public string StateStoreName { get; set; } = "statestore";

    /// <summary>
    /// Gets or sets the CommandApi DAPR app ID for service invocation.
    /// </summary>
    public string CommandApiAppId { get; set; } = "commandapi";

    /// <summary>
    /// Gets or sets the Tenants service DAPR app ID for service invocation.
    /// </summary>
    public string TenantServiceAppId { get; set; } = "tenants";

    /// <summary>
    /// Gets or sets the maximum number of timeline events to return. Guards against OOM on large streams.
    /// </summary>
    public int MaxTimelineEvents { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the timeout in seconds for DAPR service invocation calls.
    /// </summary>
    public int ServiceInvocationTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the observability trace dashboard URL. Null disables the link.
    /// </summary>
    public string? TraceUrl { get; set; }

    /// <summary>
    /// Gets or sets the observability metrics dashboard URL. Null disables the link.
    /// </summary>
    public string? MetricsUrl { get; set; }

    /// <summary>
    /// Gets or sets the observability logs dashboard URL. Null disables the link.
    /// </summary>
    public string? LogsUrl { get; set; }

    /// <summary>
    /// Gets or sets the DAPR HTTP endpoint of the EventStore server sidecar for cross-sidecar metadata queries.
    /// When null, only local sidecar metadata is attempted.
    /// </summary>
    /// <remarks>
    /// In Aspire orchestration, this endpoint is injected automatically via the Aspire extension.
    /// </remarks>
    // TODO: production deployment may require DAPR service invocation for cross-sidecar metadata
    public string? EventStoreDaprHttpEndpoint { get; set; }
}
