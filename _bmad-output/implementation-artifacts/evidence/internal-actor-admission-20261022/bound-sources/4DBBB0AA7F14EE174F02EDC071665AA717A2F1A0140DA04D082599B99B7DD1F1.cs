using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Infrastructure authority for exact target/occurrence enrollment and source-writer completion. No permissive implementation is supplied.</summary>
public interface IInteractionProtectionBindingProvider
{
    /// <summary>Resolves the original binding under current authority. A requested target is independently checked, never accepted as authority; null requests exact retained occurrence lookup, including snapshots.</summary>
    Task<InteractionProtectionBinding?> ResolveAsync(AggregateIdentity owner, PayloadProtectionOccurrenceContext occurrence,
        ProtectionTarget? requestedTarget, bool writing, CancellationToken cancellationToken = default);

    /// <summary>Acquires the current exact source-writer lease immediately before persistence; the operation id is not self-asserted proof.</summary>
    Task<bool> AcquireCompletionLeaseAsync(InteractionProtectionBinding binding, CancellationToken cancellationToken = default);

    /// <summary>Confirms an irreversible complete-target custodian tombstone against independent nonrollback state. Missing keys, outages and absent records return false.</summary>
    Task<bool> IsIrreversiblyErasedAsync(InteractionProtectionBinding binding, CancellationToken cancellationToken = default);
}
