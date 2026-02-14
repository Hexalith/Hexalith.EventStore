namespace Hexalith.EventStore.Server.DomainServices;

/// <summary>
/// Configuration options for domain service resolution and invocation.
/// </summary>
/// <param name="ConfigStoreName">The DAPR config store name. Default: "configstore".</param>
/// <param name="InvocationTimeoutSeconds">DAPR sidecar call timeout in seconds. Default: 5 (enforcement rule #14).</param>
public record DomainServiceOptions(
    string ConfigStoreName = "configstore",
    int InvocationTimeoutSeconds = 5);
