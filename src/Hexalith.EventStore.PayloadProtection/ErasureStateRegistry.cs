// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base sections 9-10, 13-15; lifecycle amendment sections 1-8.
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Enforces exact domain ownership and monotonic strongly observed erasure epochs.</summary>
/// <remarks>Observations are bounded by least-recently-observed eviction. An evicted scope loses only this in-memory
/// regression check; every admission still requires its durable <see cref="LifecycleV1"/> epoch and state to match.</remarks>
internal sealed class ErasureStateRegistry
{
    private readonly Dictionary<string, IErasureStateProvider> _providers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, LinkedListNode<KeyValuePair<string, PayloadErasureStateResult>>> _observations = new(StringComparer.Ordinal);
    private readonly LinkedList<KeyValuePair<string, PayloadErasureStateResult>> _recency = new();
    private readonly int _observationLimit;
    private readonly object _gate = new();

    /// <summary>Freezes one explicitly keyed provider per protected domain without wildcard or default ownership.</summary>
    internal ErasureStateRegistry(PayloadProtectionScopeRegistry protectedScopes,
        IEnumerable<KeyValuePair<string, IErasureStateProvider>> registrations)
        : this(protectedScopes, registrations, PayloadProtectionOperationalLimits.ErasureObservations)
    {
    }

    /// <summary>Freezes the providers with an explicit observation bound; production uses the qualified operational limit.</summary>
    internal ErasureStateRegistry(PayloadProtectionScopeRegistry protectedScopes,
        IEnumerable<KeyValuePair<string, IErasureStateProvider>> registrations, int observationLimit)
    {
        ArgumentNullException.ThrowIfNull(protectedScopes);
        ArgumentNullException.ThrowIfNull(registrations);
        if (observationLimit < 1) { throw new PayloadProtectionFormatException(); }
        _observationLimit = observationLimit;
        var domains = protectedScopes.Roots.Select(scope => scope.Domain).ToHashSet(StringComparer.Ordinal);
        foreach ((string domain, IErasureStateProvider provider) in registrations)
        {
            _ = CanonicalText.GetByteCount(domain, 1, 128);
            try
            {
                if (new AggregateIdentity("enrollment", domain, "enrollment").Domain != domain) { throw new PayloadProtectionFormatException(); }
            }
            catch (ArgumentException) { throw new PayloadProtectionFormatException(); }
            int contractVersion;
            try { contractVersion = provider?.ContractVersion ?? 0; }
            catch { throw new PayloadProtectionFormatException(); }
            if (domain == "*" || !domains.Contains(domain) || provider is null || contractVersion != 1 || !_providers.TryAdd(domain, provider))
            {
                throw new PayloadProtectionFormatException();
            }
        }
        if (!domains.SetEquals(_providers.Keys)) { throw new PayloadProtectionFormatException(); }
    }

    /// <summary>Queries the domain owner on every admission and retains only monotonic scope state.</summary>
    internal async ValueTask<PayloadErasureStateResult> ObserveAsync(AggregateIdentity identity, string? keyRef = null,
        uint? version = null, CancellationToken cancellationToken = default)
    {
        using var budget = new PayloadOperationBudget(TimeProvider.System, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (!_providers.TryGetValue(identity.Domain, out IErasureStateProvider? provider))
        {
            throw new PayloadProtectionStateException(UnreadableProtectedDataReason.ConsistencyMismatch);
        }

        string scope = PayloadProtectionStateKeys.ScopeDigest(identity);
        PayloadErasureStateResult result;
        try
        {
            result = await PayloadOperationBudget.AwaitAsync(ct => provider.GetStateAsync(new(identity, keyRef, version), ct), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { cancellationToken.ThrowIfCancellationRequested(); throw new PayloadProtectionStateException(UnreadableProtectedDataReason.ProviderUnavailable); }
        cancellationToken.ThrowIfCancellationRequested();
        if (result is null || !Enum.IsDefined(result.State) || !Enum.IsDefined(result.ReasonCode) || result.ObservedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new PayloadProtectionStateException(UnreadableProtectedDataReason.ConsistencyMismatch);
        }

        PayloadErasureReasonCode expected = result.State switch
        {
            PayloadErasureState.Active => PayloadErasureReasonCode.None,
            PayloadErasureState.Pending => PayloadErasureReasonCode.DomainPending,
            PayloadErasureState.Invalidating => PayloadErasureReasonCode.DomainInvalidating,
            PayloadErasureState.Invalidated => PayloadErasureReasonCode.DomainInvalidated,
            PayloadErasureState.Deleted => PayloadErasureReasonCode.DomainDeleted,
            PayloadErasureState.Unavailable => PayloadErasureReasonCode.StateUnavailable,
            PayloadErasureState.Denied => PayloadErasureReasonCode.StateDenied,
            _ => PayloadErasureReasonCode.StateUnknown,
        };
        if (result.ReasonCode != expected) { throw new PayloadProtectionStateException(UnreadableProtectedDataReason.ConsistencyMismatch); }
        if (result.State == PayloadErasureState.Unavailable) { throw new PayloadProtectionStateException(UnreadableProtectedDataReason.ProviderUnavailable); }
        if (result.State == PayloadErasureState.Denied) { throw new PayloadProtectionStateException(UnreadableProtectedDataReason.ProviderDenied); }
        if (result.State == PayloadErasureState.Unknown) { throw new PayloadProtectionStateException(UnreadableProtectedDataReason.ConsistencyMismatch); }
        lock (_gate)
        {
            if (_observations.TryGetValue(scope, out LinkedListNode<KeyValuePair<string, PayloadErasureStateResult>>? node))
            {
                PayloadErasureStateResult previous = node.Value.Value;
                if (result.LifecycleEpoch < previous.LifecycleEpoch
                    || (result.State == previous.State) != (result.LifecycleEpoch == previous.LifecycleEpoch)
                    || (previous.State >= PayloadErasureState.Invalidating && result.State < previous.State))
                {
                    throw new PayloadProtectionStateException(UnreadableProtectedDataReason.ConsistencyMismatch);
                }

                node.Value = new(scope, result);
                _recency.Remove(node);
                _recency.AddFirst(node);
            }
            else
            {
                // A new aggregate is always admitted; the least-recently-observed scope gives up its in-memory evidence.
                if (_observations.Count >= _observationLimit)
                {
                    LinkedListNode<KeyValuePair<string, PayloadErasureStateResult>> oldest = _recency.Last!;
                    _recency.RemoveLast();
                    _ = _observations.Remove(oldest.Value.Key);
                }

                _observations.Add(scope, _recency.AddFirst(new KeyValuePair<string, PayloadErasureStateResult>(scope, result)));
            }
        }

        return result;
    }
}
