using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Synthetic current scoped custody and exclusive lease with retained owned buffers; no live authority.</summary>
internal sealed class InteractionOccurrenceTestKeys : IInteractionOccurrenceKeyProvider
{
    private readonly HashSet<string> _used = new(StringComparer.Ordinal);
    /// <summary>Gets or sets the exact root/digest allocator.</summary>
    internal Func<InteractionOccurrenceIdentity, string, string, byte, InteractionOccurrenceOwnedKey> Allocate { get; set; } = null!;
    /// <summary>Gets or sets an optional controlled stalled root resolver.</summary>
    internal Func<InteractionOccurrenceIdentity, Task<InteractionOccurrenceOwnedKey?>>? RootResolver { get; set; }
    /// <summary>Gets or sets current authority.</summary>
    internal Func<bool> Current { get; set; } = () => true;
    /// <summary>Gets the actual write key resolutions.</summary>
    internal int WriteRoots { get; private set; }
    /// <summary>Gets the actual lease requests.</summary>
    internal int Leases { get; private set; }
    /// <inheritdoc/>
    public Task<InteractionOccurrenceOwnedKey?> ResolveDigestAsync(InteractionOccurrenceIdentity identity, string version, CancellationToken cancellationToken)
        => Task.FromResult<InteractionOccurrenceOwnedKey?>(Allocate(identity, "tenant-digest", version, 7));
    /// <inheritdoc/>
    public Task<bool> AcquireEncryptionLeaseAsync(InteractionOccurrenceRecord record, CancellationToken cancellationToken)
    { Leases++; return Task.FromResult(_used.Add(record.KeyReference)); }
    /// <inheritdoc/>
    public Task<InteractionOccurrenceOwnedKey?> ResolveRootAsync(InteractionOccurrenceIdentity identity, string keyReference, bool writing, CancellationToken cancellationToken)
    {
        if (writing) { WriteRoots++; }
        return RootResolver?.Invoke(identity) ?? Task.FromResult<InteractionOccurrenceOwnedKey?>(Allocate(identity, "interaction-root", identity.RootKeyVersion, 9));
    }
    /// <inheritdoc/>
    public Task<bool> IsCurrentAsync(InteractionOccurrenceIdentity identity, string operation, CancellationToken cancellationToken) => Task.FromResult(Current());
}
