using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;

namespace Hexalith.EventStore.Admin.Abstractions.Services;

/// <summary>
/// Service interface for querying DAPR infrastructure details — components, sidecar info, and health probes.
/// </summary>
public interface IDaprInfrastructureQueryService
{
    /// <summary>
    /// Gets detailed information about all registered DAPR components, including health probes.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of DAPR component details.</returns>
    Task<IReadOnlyList<DaprComponentDetail>> GetComponentsAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets summary information about the DAPR sidecar runtime.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The sidecar info, or null if the sidecar is unavailable.</returns>
    Task<DaprSidecarInfo?> GetSidecarInfoAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets actor runtime information including registered types, active counts, and configuration.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The actor runtime info.</returns>
    Task<DaprActorRuntimeInfo> GetActorRuntimeInfoAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the state of a specific actor instance by reading known state keys from the DAPR state store.
    /// </summary>
    /// <param name="actorType">The actor type name.</param>
    /// <param name="actorId">The actor instance ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The actor instance state, or null if the actor type is unknown.</returns>
    Task<DaprActorInstanceState?> GetActorInstanceStateAsync(string actorType, string actorId, CancellationToken ct = default);

    /// <summary>
    /// Gets an overview of pub/sub infrastructure including components, subscriptions, and remote metadata availability.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The pub/sub overview.</returns>
    Task<DaprPubSubOverview> GetPubSubOverviewAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the DAPR resiliency specification by reading and parsing the resiliency YAML configuration file.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The resiliency spec. Never null — returns <see cref="DaprResiliencySpec.Unavailable"/> when not configured.</returns>
    Task<DaprResiliencySpec> GetResiliencySpecAsync(CancellationToken ct = default);
}
