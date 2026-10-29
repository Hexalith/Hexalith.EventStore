using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>One atomically replaced Dapr cell; leases never expire into an unsafe commit.</summary>
/// <param name="Epoch">Monotonic activation fence.</param>
/// <param name="Committed">Last complete generation.</param>
/// <param name="Previous">Prior complete generation retained for rollback.</param>
/// <param name="Pending">Prepared proposal, fencing new dispatch.</param>
/// <param name="Ready">Exact required-host readiness receipts.</param>
/// <param name="LastLease">Monotonic dispatch lease sequence.</param>
/// <param name="Leases">Durable outstanding leases. A failed host requires recovery or refusal, never expiry.</param>
public sealed record RouteCatalogActivationState(long Epoch, RouteCatalogActivation? Committed,
    RouteCatalogActivation? Previous, RouteCatalogActivation? Pending,
    IReadOnlyList<RouteCatalogHostAttestation> Ready, long LastLease, IReadOnlyList<RouteCatalogDispatchFence> Leases)
{
    /// <summary>Gets immutable identities, including terminal attempts that cannot be prepared again.</summary>
    public IReadOnlyList<RouteCatalogAttemptBinding> Attempts { get; init; } = [];
    /// <summary>Gets completed exact abort/rollback requests for idempotent response-loss retries.</summary>
    public IReadOnlyList<RouteCatalogRollbackRequest> CompletedTransitions { get; init; } = [];

    /// <summary>Gets the unactivated initial state.</summary>
    public static RouteCatalogActivationState Empty { get; } = new(0, null, null, null, [], 0, []);
}
