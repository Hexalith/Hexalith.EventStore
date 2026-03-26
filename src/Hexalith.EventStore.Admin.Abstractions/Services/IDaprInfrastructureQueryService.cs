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
}
