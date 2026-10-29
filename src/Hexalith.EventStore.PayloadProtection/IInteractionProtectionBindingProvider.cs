using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Infrastructure authority for exact target/occurrence enrollment and source-writer completion. No permissive implementation is supplied.</summary>
public interface IInteractionProtectionBindingProvider
{
    /// <summary>Authenticates exact committed source ciphertext after independently proved permanent custody loss. This never treats missing custody or an untrusted stored envelope as erased.</summary>
    Task<InteractionProtectionBinding?> ResolveErasedStoredAsync(AggregateIdentity owner, PayloadProtectionOccurrenceContext occurrence,
        byte[] sealedPayload, string keyReference, CancellationToken cancellationToken = default)
        => Task.FromResult<InteractionProtectionBinding?>(null);
    /// <summary>Resolves the complete independently enrolled mixed-root entry manifest before root issue. Omitted implementations refuse; caller-supplied target/path data grants no authority.</summary>
    Task<IReadOnlyList<InteractionProtectionBinding>?> ResolveSnapshotEntriesAsync(AggregateIdentity owner, PayloadProtectionOccurrenceContext parent,
        string containerDigest, IReadOnlyList<SnapshotProtectionEntry> entries, bool writing, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<InteractionProtectionBinding>?>(null);
    /// <summary>Atomically acquires every original mixed-snapshot entry completion lease. Missing implementations perform no partial acquisition.</summary>
    Task<bool> AcquireSnapshotCompletionLeasesAsync(IReadOnlyList<InteractionProtectionBinding> bindings, CancellationToken cancellationToken = default)
        => Task.FromResult(false);
    /// <summary>Resolves the original binding under current authority. A requested target is independently checked, never accepted as authority; null requests exact retained occurrence lookup, including snapshots.</summary>
    Task<InteractionProtectionBinding?> ResolveAsync(AggregateIdentity owner, PayloadProtectionOccurrenceContext occurrence,
        ProtectionTarget? requestedTarget, bool writing, CancellationToken cancellationToken = default);

    /// <summary>Acquires the current exact source-writer lease immediately before persistence; the operation id is not self-asserted proof.</summary>
    Task<bool> AcquireCompletionLeaseAsync(InteractionProtectionBinding binding, CancellationToken cancellationToken = default);

    /// <summary>Confirms an irreversible complete-target custodian tombstone against independent nonrollback state. Missing keys, outages and absent records return false.</summary>
    Task<bool> IsIrreversiblyErasedAsync(InteractionProtectionBinding binding, CancellationToken cancellationToken = default);
}
