
namespace Hexalith.EventStore.HealthChecks;

/// <summary>
/// Snapshot of the local DAPR sidecar's actor-runtime placement state, read from the
/// sidecar metadata endpoint (<c>/v1.0/metadata</c> → <c>actorRuntime</c>).
/// </summary>
/// <param name="MetadataReachable">Whether the sidecar metadata endpoint responded successfully.</param>
/// <param name="HostReady">
/// The sidecar's <c>actorRuntime.hostReady</c> flag. <see langword="false"/> when the actor host
/// has not (yet) joined the placement table — actor invocations cannot be routed and will hang.
/// </param>
/// <param name="Placement">The raw <c>actorRuntime.placement</c> string (e.g. <c>"placement: connected"</c>).</param>
/// <param name="RuntimeStatus">The raw <c>actorRuntime.runtimeStatus</c> string (e.g. <c>"RUNNING"</c>).</param>
public readonly record struct DaprActorPlacementStatus(
    bool MetadataReachable,
    bool HostReady,
    string? Placement,
    string? RuntimeStatus);

/// <summary>
/// Probes the local DAPR sidecar for actor-runtime placement readiness. Abstracted behind an
/// interface so the placement health check can be unit-tested without a live sidecar.
/// </summary>
public interface IDaprActorPlacementProbe {
    /// <summary>
    /// Reads the local sidecar's actor-runtime placement state.
    /// </summary>
    /// <param name="cancellationToken">The token to observe while contacting the sidecar.</param>
    /// <returns>The current <see cref="DaprActorPlacementStatus"/>.</returns>
    Task<DaprActorPlacementStatus> CheckAsync(CancellationToken cancellationToken);
}
