using System.Globalization;

using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Occurrence key adapter. It grants one encryption lease for the current root and returns an owned key only when an injected delivery does.
/// A second encryption, a missing tombstone, a restored root, or a missing delivery denies before ciphertext.
/// </summary>
public sealed class InteractionOccurrenceKeyProvider : IInteractionOccurrenceKeyProvider
{
    private readonly IInteractionOccurrenceKeyDelivery? _delivery;
    private readonly HashSet<string> _leases = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    /// <summary>Creates the adapter. Production passes no delivery.</summary>
    /// <param name="delivery">Injected current delivery. Null denies every key and lease.</param>
    public InteractionOccurrenceKeyProvider(IInteractionOccurrenceKeyDelivery? delivery = null)
    {
        _delivery = delivery;
    }

    /// <inheritdoc />
    public async Task<InteractionOccurrenceOwnedKey?> ResolveDigestAsync(
        InteractionOccurrenceIdentity identity,
        string version,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_delivery is null || identity is null || string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        try
        {
            InteractionOccurrenceKeyDeliveryResult? result = await _delivery.DeliverAsync(
                identity, "tenant-digest", version, null, false, cancellationToken).ConfigureAwait(false);
            return Own(identity, "tenant-digest", version, result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> AcquireEncryptionLeaseAsync(InteractionOccurrenceRecord record, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_delivery is null || record?.Request?.Identity is null || string.IsNullOrWhiteSpace(record.KeyReference))
        {
            return false;
        }

        try
        {
            InteractionOccurrenceIdentity identity = record.Request.Identity;
            InteractionOccurrenceKeyDeliveryResult? result = await _delivery.DeliverAsync(
                identity, "interaction-root", identity.RootKeyVersion, record.KeyReference, true, cancellationToken).ConfigureAwait(false);
            if (!Current(result) || result!.KeyBytes is not { Length: 32 })
            {
                return false;
            }

            lock (_gate)
            {
                return _leases.Add(LeaseId(record));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<InteractionOccurrenceOwnedKey?> ResolveRootAsync(
        InteractionOccurrenceIdentity identity,
        string keyReference,
        bool writing,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_delivery is null || identity is null || string.IsNullOrWhiteSpace(keyReference))
        {
            return null;
        }

        if (writing && !HoldsLease(identity, keyReference))
        {
            return null;
        }

        try
        {
            InteractionOccurrenceKeyDeliveryResult? result = await _delivery.DeliverAsync(
                identity, "interaction-root", identity.RootKeyVersion, keyReference, writing, cancellationToken).ConfigureAwait(false);
            return Own(identity, "interaction-root", identity.RootKeyVersion, result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsCurrentAsync(InteractionOccurrenceIdentity identity, string operation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_delivery is null || identity is null || operation is not ("Read" or "Write"))
        {
            return false;
        }

        if (operation == "Write" && !HoldsAnyLease(identity))
        {
            return false;
        }

        try
        {
            InteractionOccurrenceKeyDeliveryResult? result = await _delivery.DeliverAsync(
                identity, "interaction-root", identity.RootKeyVersion, null, operation == "Write", cancellationToken).ConfigureAwait(false);
            return Current(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private bool HoldsLease(InteractionOccurrenceIdentity identity, string keyReference)
    {
        lock (_gate)
        {
            return _leases.Contains(LeaseId(identity, keyReference));
        }
    }

    private bool HoldsAnyLease(InteractionOccurrenceIdentity identity)
    {
        string prefix = LeasePrefix(identity);
        lock (_gate)
        {
            return _leases.Any(lease => lease.StartsWith(prefix, StringComparison.Ordinal));
        }
    }

    private static bool Current(InteractionOccurrenceKeyDeliveryResult? result)
        => result is { CurrentRoot: true } && (!result.TombstoneRequired || result.TombstonePresent);

    private static InteractionOccurrenceOwnedKey? Own(
        InteractionOccurrenceIdentity identity,
        string purpose,
        string version,
        InteractionOccurrenceKeyDeliveryResult? result)
    {
        if (!Current(result) || result!.KeyBytes is not { Length: 32 })
        {
            return null;
        }

        return new InteractionOccurrenceOwnedKey(identity.Target, purpose, version, result.KeyBytes.ToArray());
    }

    private static string LeaseId(InteractionOccurrenceRecord record)
        => LeaseId(record.Request.Identity, record.KeyReference);

    private static string LeaseId(InteractionOccurrenceIdentity identity, string keyReference)
        => LeasePrefix(identity) + keyReference;

    private static string LeasePrefix(InteractionOccurrenceIdentity identity)
        => string.Join(
            '\u001f',
            identity.Target.TenantId,
            identity.Target.AgentInteractionId,
            identity.Target.TargetProtectionKeyAlias,
            identity.Owner.TenantId,
            identity.Owner.Domain,
            identity.Owner.AggregateId,
            identity.Kind.ToString(),
            identity.Sequence.ToString(CultureInfo.InvariantCulture),
            identity.RootKeyVersion,
            string.Empty);
}
