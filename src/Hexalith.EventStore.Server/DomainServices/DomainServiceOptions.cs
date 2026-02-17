namespace Hexalith.EventStore.Server.DomainServices;

/// <summary>
/// Configuration options for domain service resolution and invocation.
/// </summary>
/// <remarks>
/// <para><b>DEPLOYMENT SECURITY:</b> The <see cref="ConfigStoreName"/> references a DAPR config store
/// component. Write access to this store must be restricted to admin service accounts only (Red Team H2).
/// Config store poisoning can redirect domain service registrations to malicious endpoints.</para>
/// </remarks>
/// <param name="ConfigStoreName">The DAPR config store name. Default: "configstore".</param>
/// <param name="InvocationTimeoutSeconds">DAPR sidecar call timeout in seconds. Default: 5 (enforcement rule #14).</param>
/// <param name="MaxEventsPerResult">Maximum number of events allowed in a single domain service result. Default: 1000 (AC #6).</param>
/// <param name="MaxEventSizeBytes">Maximum serialized size in bytes for a single event payload. Default: 1_048_576 (1 MB).</param>
/// <param name="Registrations">
/// Static domain service registrations keyed by "{tenant}:{domain}:{version}".
/// Checked before the DAPR config store. Useful for local development and testing.
/// </param>
public record DomainServiceOptions(
    string ConfigStoreName = "configstore",
    int InvocationTimeoutSeconds = 5,
    int MaxEventsPerResult = 1000,
    int MaxEventSizeBytes = 1_048_576)
{
    /// <summary>
    /// Static domain service registrations keyed by "{tenant}:{domain}:{version}".
    /// Checked before the DAPR config store. Useful for local development and testing.
    /// </summary>
    public Dictionary<string, DomainServiceRegistration> Registrations { get; init; } = [];
}
