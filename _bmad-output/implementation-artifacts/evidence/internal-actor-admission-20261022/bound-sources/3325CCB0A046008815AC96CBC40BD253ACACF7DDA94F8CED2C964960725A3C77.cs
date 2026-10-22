using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Independently authenticated immutable occurrence binding; versions and original operation identities survive response loss and restore.</summary>
/// <param name="Identity">Complete source occurrence and interaction root.</param>
/// <param name="DigestKeyVersion">Retained tenant DigestKey version.</param>
/// <param name="ReservationAttemptOrdinal">Original once-assigned reservation attempt.</param>
/// <param name="KeyReference">Original canonical occurrence key reference.</param>
/// <param name="OperationId">Original independently verifiable source-writer operation.</param>
/// <param name="LifecycleEpoch">Strongly observed root lifecycle epoch.</param>
public sealed record InteractionProtectionBinding(InteractionOccurrenceIdentity Identity, string DigestKeyVersion,
    long ReservationAttemptOrdinal, string KeyReference, string OperationId, ulong LifecycleEpoch)
{
    /// <inheritdoc/>
    public override string ToString() => nameof(InteractionProtectionBinding);
}
