using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Candidate occurrence-safe hierarchy: one custodied interaction root and ephemeral per-invocation subkeys, not another persisted DEK.</summary>
/// <param name="Target">Complete interaction/explicit-alias root.</param>
/// <param name="Owner">Authenticated source occurrence owner, including directory outbox.</param>
/// <param name="Kind">Event or snapshot; cache copies retain their original occurrence.</param>
/// <param name="Sequence">Original aggregate-local occurrence sequence.</param>
/// <param name="PayloadTypeId">Exact persisted event/snapshot type.</param>
/// <param name="RootKeyVersion">Original retained interaction-root version.</param>
/// <param name="DerivationProfileVersion">Exact candidate hierarchy version.</param>
public sealed record InteractionOccurrenceIdentity(ProtectionTarget Target, AggregateIdentity Owner, PayloadProtectionPayloadKind Kind, ulong Sequence, string PayloadTypeId, string RootKeyVersion, string DerivationProfileVersion)
{
    /// <inheritdoc/>
    public override string ToString() => nameof(InteractionOccurrenceIdentity);
}
