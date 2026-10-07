using Hexalith.EventStore.ServiceDefaults.Authentication;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// The single catalog binding every canonical domain-service route to its workload operation and policy.
/// </summary>
/// <remarks>
/// The SDK maps these routes with their policies, the startup inventory verifies the effective endpoints, and the
/// EventStore outbound handler uses the same catalog to request an assertion for the invoked method.
/// </remarks>
public static class EventStoreDomainServiceRoutes
{
    /// <summary>Gets the only routes that may be anonymous on a domain-service host.</summary>
    public static IReadOnlyList<string> AnonymousProbeRoutes { get; } = ["/health", "/alive", "/ready"];

    /// <summary>Gets every canonical operational route.</summary>
    public static IReadOnlyList<EventStoreDomainServiceRoute> Operational { get; } =
    [
        new("/process", EventStoreWorkloadOperations.DomainServiceProcess, EventStoreDomainServicePolicies.Process),
        new("/replay-state", EventStoreWorkloadOperations.DomainServiceReplayState, EventStoreDomainServicePolicies.ReplayState),
        new("/query", EventStoreWorkloadOperations.DomainServiceQuery, EventStoreDomainServicePolicies.Query),
        new("/project", EventStoreWorkloadOperations.DomainServiceProject, EventStoreDomainServicePolicies.Project),
        new("/project/v2", EventStoreWorkloadOperations.DomainServiceProject, EventStoreDomainServicePolicies.Project),
        new("/project/v2/reconcile", EventStoreWorkloadOperations.DomainServiceProject, EventStoreDomainServicePolicies.Project),
        new("/project/rebuild/v1", EventStoreWorkloadOperations.DomainServiceProject, EventStoreDomainServicePolicies.Project),
        new("/project/rebuild/stage/v1", EventStoreWorkloadOperations.DomainServiceProject, EventStoreDomainServicePolicies.Project),
        new("/project/rebuild/commit/v1", EventStoreWorkloadOperations.DomainServiceProject, EventStoreDomainServicePolicies.Project),
        new("/project/rebuild/abort/v1", EventStoreWorkloadOperations.DomainServiceProject, EventStoreDomainServicePolicies.Project),
        new("/project/rebuild/verify/v1", EventStoreWorkloadOperations.DomainServiceProject, EventStoreDomainServicePolicies.Project),
        new("/project/rebuild/shared/v1", EventStoreWorkloadOperations.DomainServiceProject, EventStoreDomainServicePolicies.Project),
        new("/admin/operational-index-metadata", EventStoreWorkloadOperations.DomainServiceMetadata, EventStoreDomainServicePolicies.Metadata),
    ];

    /// <summary>Gets every operation the catalog grants.</summary>
    public static IReadOnlyList<string> Operations { get; } = [.. Operational.Select(static route => route.Operation).Distinct(StringComparer.Ordinal)];

    /// <summary>
    /// Resolves a route pattern or Dapr invocation method name to its catalog entry.
    /// </summary>
    /// <param name="routeOrMethod">A route such as <c>/project/v2</c> or a method name such as <c>project/v2</c>.</param>
    /// <param name="route">The catalog entry when found.</param>
    /// <returns><see langword="true"/> when the route is a canonical operational route.</returns>
    public static bool TryGet(string? routeOrMethod, out EventStoreDomainServiceRoute? route)
    {
        string normalized = Normalize(routeOrMethod);
        route = Operational.FirstOrDefault(candidate => string.Equals(candidate.Route, normalized, StringComparison.OrdinalIgnoreCase));
        return route is not null;
    }

    /// <summary>
    /// Normalizes a route pattern or method name to the catalog's leading-slash form.
    /// </summary>
    /// <param name="routeOrMethod">The route pattern or method name.</param>
    /// <returns>The normalized route, or an empty string.</returns>
    public static string Normalize(string? routeOrMethod)
    {
        if (string.IsNullOrWhiteSpace(routeOrMethod))
        {
            return string.Empty;
        }

        string trimmed = routeOrMethod.Trim().Trim('/');
        return "/" + trimmed;
    }
}
