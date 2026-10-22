using Hexalith.EventStore.ServiceDefaults.Authentication;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Authorization-policy names that protect the canonical domain-service routes.
/// </summary>
/// <remarks>
/// A host that maps its own handler for an SDK route (for example a bespoke <c>/project</c>) must attach the same
/// policy, such as <c>app.MapPost("/project", handler).RequireAuthorization(EventStoreDomainServicePolicies.Project)</c>.
/// The startup route inventory fails the host when an effective SDK route carries a weaker policy.
/// </remarks>
public static class EventStoreDomainServicePolicies
{
    /// <summary>Requires an EventStore workload assertion granting <c>/process</c>.</summary>
    public const string Process = EventStoreWorkloadAuthenticationDefaults.WorkloadScheme + ":" + EventStoreWorkloadOperations.DomainServiceProcess;

    /// <summary>Requires an EventStore workload assertion granting <c>/replay-state</c>.</summary>
    public const string ReplayState = EventStoreWorkloadAuthenticationDefaults.WorkloadScheme + ":" + EventStoreWorkloadOperations.DomainServiceReplayState;

    /// <summary>Requires an EventStore workload assertion granting <c>/query</c>.</summary>
    public const string Query = EventStoreWorkloadAuthenticationDefaults.WorkloadScheme + ":" + EventStoreWorkloadOperations.DomainServiceQuery;

    /// <summary>Requires an EventStore workload assertion granting the <c>/project</c> family.</summary>
    public const string Project = EventStoreWorkloadAuthenticationDefaults.WorkloadScheme + ":" + EventStoreWorkloadOperations.DomainServiceProject;

    /// <summary>Requires an EventStore workload assertion granting <c>/admin/operational-index-metadata</c>.</summary>
    public const string Metadata = EventStoreWorkloadAuthenticationDefaults.WorkloadScheme + ":" + EventStoreWorkloadOperations.DomainServiceMetadata;

    /// <summary>Requires any authenticated EventStore workload; this is the domain-service fallback policy.</summary>
    public const string AnyWorkload = EventStoreWorkloadAuthenticationDefaults.WorkloadScheme + ":any";

    /// <summary>Requires an authenticated Dapr application channel; reserved for sidecar-originated deliveries.</summary>
    public const string SidecarChannel = EventStoreWorkloadAuthenticationDefaults.SidecarChannelPolicy;
}
