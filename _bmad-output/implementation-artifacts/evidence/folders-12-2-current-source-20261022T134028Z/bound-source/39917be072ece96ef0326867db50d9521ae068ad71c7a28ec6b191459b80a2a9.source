using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Injected retained-target, completion-lease, and erasure-witness source. Production passes none.
/// Lookup methods start no pin, unpin, or destroy.
/// </summary>
public interface IInteractionProtectionBindingSource
{
    /// <summary>Resolves the retained binding for the owner and occurrence. The caller alias is not an argument.</summary>
    /// <param name="owner">Aggregate owner.</param>
    /// <param name="occurrence">Storage occurrence.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The retained binding, or null when it is not installed.</returns>
    Task<InteractionProtectionBinding?> FindRetainedAsync(
        AggregateIdentity owner,
        PayloadProtectionOccurrenceContext occurrence,
        CancellationToken cancellationToken);

    /// <summary>Grants the one completion lease for the exact retained binding.</summary>
    /// <param name="binding">Exact retained binding.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>True only when this call acquires that lease.</returns>
    Task<bool> TryAcquireCompletionLeaseAsync(InteractionProtectionBinding binding, CancellationToken cancellationToken);

    /// <summary>Reads the independent erasure witness. This read starts no effect.</summary>
    /// <param name="binding">Exact retained binding.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The retained witness, or null when no receipt is installed.</returns>
    Task<InteractionProtectionErasureWitness?> ReadErasureAsync(
        InteractionProtectionBinding binding,
        CancellationToken cancellationToken);
}
