using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Private candidate custody/current authorization adapter. Defaults are absent; no key value crosses the domain boundary.</summary>
internal interface IInteractionOccurrenceKeyProvider
{
    /// <summary>Resolves retained tenant DigestKey under the exact authenticated occurrence; ownership transfers to the caller.</summary>
    Task<InteractionOccurrenceOwnedKey?> ResolveDigestAsync(InteractionOccurrenceIdentity identity, string version, CancellationToken cancellationToken);
    /// <summary>Acquires an independently atomic, exclusive once-per-reference encryption lease. Unknown/prior invocation must deny; a lease never authorizes re-encryption.</summary>
    Task<bool> AcquireEncryptionLeaseAsync(InteractionOccurrenceRecord record, CancellationToken cancellationToken);
    /// <summary>Resolves the exact interaction root under current write lease or current read authority; ownership transfers to the caller.</summary>
    Task<InteractionOccurrenceOwnedKey?> ResolveRootAsync(InteractionOccurrenceIdentity identity, string keyReference, bool writing, CancellationToken cancellationToken);
    /// <summary>Reconfirms current operation/root/writer authority before any ciphertext/plaintext release.</summary>
    Task<bool> IsCurrentAsync(InteractionOccurrenceIdentity identity, string operation, CancellationToken cancellationToken);
}
