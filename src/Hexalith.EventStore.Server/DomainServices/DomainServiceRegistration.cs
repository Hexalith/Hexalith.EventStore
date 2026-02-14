namespace Hexalith.EventStore.Server.DomainServices;

/// <summary>
/// Registration data for a domain service resolved from the DAPR config store.
/// Maps a tenant+domain combination to a DAPR app-id and method name.
/// </summary>
/// <param name="AppId">The DAPR app-id of the domain service.</param>
/// <param name="MethodName">The HTTP method name to invoke (e.g., "process-command").</param>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="Domain">The domain name.</param>
/// <param name="Version">Optional service version.</param>
public record DomainServiceRegistration(
    string AppId,
    string MethodName,
    string TenantId,
    string Domain,
    string? Version);
