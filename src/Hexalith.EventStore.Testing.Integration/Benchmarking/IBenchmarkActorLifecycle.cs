using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Actors;

namespace Hexalith.EventStore.Testing.Integration.Benchmarking;

/// <summary>
/// Controls the production aggregate actor lifecycle required by Dapr actor-state transactions.
/// </summary>
/// <remarks>
/// Dapr 1.18.1 rejects actor-state transaction writes with <c>ERR_ACTOR_INSTANCE_MISSING</c>
/// unless the target actor instance is active. Implementations must activate through a read-only
/// production actor method and deactivate through the actor host after quiescent external writes.
/// </remarks>
public interface IBenchmarkActorLifecycle {
    /// <summary>
    /// Activates an aggregate actor through its production metadata reader.
    /// </summary>
    /// <param name="identity">The aggregate identity to activate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The production actor's stream metadata.</returns>
    Task<AggregateStreamMetadata> ActivateAsync(
        AggregateIdentity identity,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Explicitly deactivates an aggregate actor through the actor host endpoint.
    /// </summary>
    /// <param name="identity">The aggregate identity to deactivate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the actor host does not confirm deactivation.</exception>
    Task DeactivateAsync(
        AggregateIdentity identity,
        CancellationToken cancellationToken = default);
}
