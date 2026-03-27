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

    /// <summary>
    /// Gets or sets the file path to the DAPR resiliency YAML configuration.
    /// In Aspire development: set via configuration injection (e.g., "DaprComponents/resiliency.yaml").
    /// In production: set to the mounted resiliency YAML path (e.g., "/dapr/components/resiliency.yaml").
    /// When null, the resiliency viewer shows "configuration not available" with setup guidance.
    /// </summary>
    public string? ResiliencyConfigPath { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether background health history collection is enabled. Default: true.
    /// </summary>
    public bool HealthHistoryEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the interval in seconds between health snapshot captures. Default: 60.
    /// Minimum: 10. Values below 10 are clamped to 10 to prevent excessive state store writes.
    /// </summary>
    public int HealthHistoryCaptureIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets the number of days to retain health history in state store. Default: 7.
    /// Older entries are cleaned up daily. Minimum: 1. Maximum: 30.
    /// </summary>
    public int HealthHistoryRetentionDays { get; set; } = 7;

    /// <summary>
    /// Gets or sets the maximum number of history entries returned per query. Default: 50,000.
    /// Prevents excessive memory usage on large time-range queries with many components.
    /// Results exceeding this cap are truncated to the most recent entries.
    /// </summary>
    public int MaxHealthHistoryEntriesPerQuery { get; set; } = 50_000;

    /// <summary>
    /// Gets or sets the maximum number of events to replay for blame computation. Default: 10,000.
    /// When a stream exceeds this limit, blame is computed from a partial window and IsTruncated is set.
    /// </summary>
    public int MaxBlameEvents { get; set; } = 10_000;

    /// <summary>
    /// Gets or sets the maximum number of fields to include in blame results. Default: 5,000.
    /// When state has more leaf fields, only the most recently changed fields are included and IsFieldsTruncated is set.
    /// </summary>
    public int MaxBlameFields { get; set; } = 5_000;
}
