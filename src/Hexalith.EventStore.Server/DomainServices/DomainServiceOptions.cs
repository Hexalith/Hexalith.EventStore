namespace Hexalith.EventStore.Server.DomainServices;

/// <summary>
/// Configuration options for domain service resolution and invocation.
/// </summary>
/// <remarks>
/// <para><b>DEPLOYMENT SECURITY:</b> The <see cref="ConfigStoreName"/> references a DAPR config store
/// component. Write access to this store must be restricted to admin service accounts only (Red Team H2).
/// Config store poisoning can redirect domain service registrations to malicious endpoints.</para>
/// </remarks>
public record DomainServiceOptions {
    /// <summary>
    /// The DAPR config store name for domain service registration overrides.
    /// Default: null (convention-based routing only — AppId = domain name, MethodName = "process").
    /// Set to a config store name (e.g., "configstore") to enable config store lookups
    /// that override convention-based routing for complex scenarios (e.g., per-tenant routing).
    /// </summary>
    public string? ConfigStoreName { get; init; }

    /// <summary>DAPR sidecar call timeout in seconds. Default: 5 (enforcement rule #14).</summary>
    public int InvocationTimeoutSeconds { get; init; } = 5;

    /// <summary>Maximum number of events allowed in a single domain service result. Default: 1000 (AC #6).</summary>
    public int MaxEventsPerResult { get; init; } = 1000;

    /// <summary>Maximum serialized size in bytes for a single event payload. Default: 1_048_576 (1 MB).</summary>
    public int MaxEventSizeBytes { get; init; } = 1_048_576;

    /// <summary>
    /// Static domain service registrations keyed by "{tenant}:{domain}:{version}".
    /// Checked before the DAPR config store. Useful for local development and testing.
    /// </summary>
    public Dictionary<string, DomainServiceRegistration> Registrations { get; init; } = [];
}
