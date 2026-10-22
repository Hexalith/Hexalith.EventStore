using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Binding adapter. It resolves the retained target rather than the caller alias, requires one completion lease, and treats erasure as true only from an independent witness receipt.
/// </summary>
public sealed class InteractionProtectionBindingProvider : IInteractionProtectionBindingProvider
{
    private readonly IInteractionProtectionBindingSource? _source;
    private readonly HashSet<string> _completionLeases = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    /// <summary>Creates the adapter. Production passes no source.</summary>
    /// <param name="source">Injected retained binding source. Null denies resolve, lease, and erasure.</param>
    public InteractionProtectionBindingProvider(IInteractionProtectionBindingSource? source = null)
    {
        _source = source;
    }

    /// <summary>Computes the retained-binding digest a witness must match. This is not an installed anchor.</summary>
    /// <param name="binding">Retained binding.</param>
    /// <returns>Uppercase SHA-256 of the retained target and original reference.</returns>
    public static string ManifestDigest(InteractionProtectionBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        string text = string.Join(
            '\u001f',
            TargetId(binding.Identity.Target),
            binding.KeyReference,
            binding.OperationId,
            binding.DigestKeyVersion);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    /// <summary>Computes the order digest of the admitted targets and original receipts without sorting either list.</summary>
    /// <param name="admittedTargets">Admitted target ids in original order.</param>
    /// <param name="originalReceipts">Original receipts in that same order.</param>
    /// <returns>Uppercase SHA-256 of the two lists in the supplied order.</returns>
    public static string ReceiptOrderDigest(IReadOnlyList<string> admittedTargets, IReadOnlyList<string> originalReceipts)
    {
        ArgumentNullException.ThrowIfNull(admittedTargets);
        ArgumentNullException.ThrowIfNull(originalReceipts);
        string text = string.Join('\n', admittedTargets) + "\n--\n" + string.Join('\n', originalReceipts);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    /// <summary>Formats one retained target id. Caller aliases are not rewritten into this id.</summary>
    /// <param name="target">Retained protection target.</param>
    /// <returns>The stable target id.</returns>
    public static string TargetId(ProtectionTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return string.Join('\u001f', target.TenantId, target.AgentInteractionId, target.TargetProtectionKeyAlias);
    }

    /// <inheritdoc />
    public async Task<InteractionProtectionBinding?> ResolveAsync(
        AggregateIdentity owner,
        PayloadProtectionOccurrenceContext occurrence,
        ProtectionTarget? requestedTarget,
        bool writing,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_source is null || owner is null || occurrence is null)
        {
            return null;
        }

        try
        {
            InteractionProtectionBinding? retained = await _source.FindRetainedAsync(owner, occurrence, cancellationToken).ConfigureAwait(false);
            if (retained?.Identity is null || retained.Identity.Owner != owner || retained.Identity.Sequence != occurrence.RecordSequence
                || retained.Identity.Kind != occurrence.PayloadKind || retained.Identity.PayloadTypeId != occurrence.PayloadTypeId)
            {
                return null;
            }

            if (requestedTarget is not null && retained.Identity.Target != requestedTarget)
            {
                return null;
            }

            return retained;
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
    public async Task<bool> AcquireCompletionLeaseAsync(InteractionProtectionBinding binding, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_source is null || binding?.Identity is null)
        {
            return false;
        }

        string id = CompletionId(binding);
        try
        {
            PayloadProtectionOccurrenceContext occurrence = new(binding.Identity.Sequence, binding.Identity.Kind, binding.Identity.PayloadTypeId);
            InteractionProtectionBinding? retained = await _source.FindRetainedAsync(binding.Identity.Owner, occurrence, cancellationToken).ConfigureAwait(false);
            if (retained != binding)
            {
                return false;
            }

            lock (_gate)
            {
                if (!_completionLeases.Add(id))
                {
                    return false;
                }
            }

            if (await _source.TryAcquireCompletionLeaseAsync(binding, cancellationToken).ConfigureAwait(false))
            {
                return true;
            }

            Release(id);
            return false;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Release(id);
            throw;
        }
        catch (Exception)
        {
            Release(id);
            return false;
        }
    }

    /// <summary>
    /// Returns the retained original receipt vector. A lost transport still returns that vector.
    /// This read does not acquire a lease and does not pin, unpin, or destroy.
    /// </summary>
    /// <param name="binding">Exact retained binding.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The original receipts, or null when the retained vector is partial, conflicting, or absent.</returns>
    public async Task<IReadOnlyList<string>?> LookupOriginalReceiptsAsync(InteractionProtectionBinding binding, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_source is null || binding?.Identity is null)
        {
            return null;
        }

        try
        {
            InteractionProtectionErasureWitness? witness = await _source.ReadErasureAsync(binding, cancellationToken).ConfigureAwait(false);
            return RetainedReceipts(binding, witness);
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
    public async Task<bool> IsIrreversiblyErasedAsync(InteractionProtectionBinding binding, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_source is null || binding?.Identity is null)
        {
            return false;
        }

        try
        {
            InteractionProtectionErasureWitness? witness = await _source.ReadErasureAsync(binding, cancellationToken).ConfigureAwait(false);
            return WitnessErases(binding, witness);
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

    private static bool WitnessErases(InteractionProtectionBinding binding, InteractionProtectionErasureWitness? witness)
    {
        if (witness is null || !witness.TransportKnown || witness.Partial || witness.UnknownPin
            || witness.AdmittedTargets is null || witness.OriginalReceipts is null)
        {
            return false;
        }

        if (!string.Equals(witness.ManifestDigest, ManifestDigest(binding), StringComparison.Ordinal))
        {
            return false;
        }

        if (witness.AdmittedTargets.Count == 0 || witness.AdmittedTargets.Count != witness.OriginalReceipts.Count)
        {
            return false;
        }

        if (!string.Equals(witness.OrderDigest, ReceiptOrderDigest(witness.AdmittedTargets, witness.OriginalReceipts), StringComparison.Ordinal))
        {
            return false;
        }

        var targets = new HashSet<string>(StringComparer.Ordinal);
        var receipts = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < witness.OriginalReceipts.Count; index++)
        {
            if (string.IsNullOrWhiteSpace(witness.AdmittedTargets[index]) || string.IsNullOrWhiteSpace(witness.OriginalReceipts[index])
                || !targets.Add(witness.AdmittedTargets[index]) || !receipts.Add(witness.OriginalReceipts[index]))
            {
                return false;
            }
        }

        if (!witness.AdmittedTargets.Contains(TargetId(binding.Identity.Target), StringComparer.Ordinal))
        {
            return false;
        }

        return string.IsNullOrEmpty(witness.EffectiveHoldId) || witness.UnpinnedHoldId == witness.EffectiveHoldId;
    }

    private static IReadOnlyList<string>? RetainedReceipts(InteractionProtectionBinding binding, InteractionProtectionErasureWitness? witness)
    {
        if (witness is null || witness.Partial || witness.AdmittedTargets is null || witness.OriginalReceipts is null)
        {
            return null;
        }

        if (!string.Equals(witness.ManifestDigest, ManifestDigest(binding), StringComparison.Ordinal)
            || witness.AdmittedTargets.Count == 0
            || witness.AdmittedTargets.Count != witness.OriginalReceipts.Count
            || !string.Equals(witness.OrderDigest, ReceiptOrderDigest(witness.AdmittedTargets, witness.OriginalReceipts), StringComparison.Ordinal))
        {
            return null;
        }

        var receipts = new string[witness.OriginalReceipts.Count];
        var targets = new HashSet<string>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < witness.OriginalReceipts.Count; index++)
        {
            if (string.IsNullOrWhiteSpace(witness.AdmittedTargets[index]) || string.IsNullOrWhiteSpace(witness.OriginalReceipts[index])
                || !targets.Add(witness.AdmittedTargets[index]) || !seen.Add(witness.OriginalReceipts[index]))
            {
                return null;
            }

            receipts[index] = witness.OriginalReceipts[index];
        }

        return witness.AdmittedTargets.Contains(TargetId(binding.Identity.Target), StringComparer.Ordinal) ? receipts : null;
    }

    private void Release(string id)
    {
        lock (_gate)
        {
            _ = _completionLeases.Remove(id);
        }
    }

    private static string CompletionId(InteractionProtectionBinding binding)
        => string.Join('\u001f', TargetId(binding.Identity.Target), binding.KeyReference, binding.OperationId);
}
