using System.Collections.Frozen;
using System.Security.Cryptography;

using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Executes an admitted adjacent chain using shared charged owners and nonescaping invocation leases.</summary>
/// <remarks>This local kernel does not authenticate source evidence or attest validator/options/dependency readiness.</remarks>
internal sealed class EventUpcastChainExecutor
{
    private readonly EventDomainRegistry _registry;
    private readonly FrozenDictionary<(string Type, int Version), RegisteredEventUpcaster> _upcasters;
    private readonly EventVersionValidator _validateVersion;
    private readonly Action<string, int, CancellationToken>? _requireVersionBindings;

    /// <summary>Gets the exact registry instance admitted by this executor.</summary>
    internal EventDomainRegistry Registry => _registry;

    /// <summary>Captures immutable registrations; source authentication and complete runtime attestation remain caller prerequisites.</summary>
    internal EventUpcastChainExecutor(EventDomainRegistry registry,
        IReadOnlyDictionary<(string Type, int Version), RegisteredEventUpcaster> upcasters,
        EventVersionValidator validateVersion,
        Action<string, int, CancellationToken>? requireVersionBindings = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(upcasters);
        ArgumentNullException.ThrowIfNull(validateVersion);
        _registry = registry;
        _registry.CapabilityLoss.RequireNoObservedLoss();
        _upcasters = upcasters.ToFrozenDictionary();
        _validateVersion = validateVersion;
        _requireVersionBindings = requireVersionBindings;
        foreach (((string type, int source), RegisteredEventUpcaster binding) in _upcasters)
        {
            binding.RequireDescriptor(registry.GetEdge(type, source), registry.CapabilityLoss);
        }
    }

    /// <summary>Returns a charged exclusive current owner only after every hop has passed descriptor/schema/identity checks.</summary>
    internal ValueTask<ImmutablePayload> UpcastAsync(string canonicalType, int sourceVersion, IReadOnlyPayload source,
        EventBufferBudget budget, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(budget);
        RequireChain(canonicalType, sourceVersion, cancellationToken);
        return UpcastOwnedAsync(canonicalType, sourceVersion,
            ImmutablePayload.CopyFrom(source, budget, cancellationToken), budget, cancellationToken);
    }

    /// <summary>Consumes an exclusive charged source owner without allocating a second copy.</summary>
    /// <remarks>Ownership transfers at entry, including refusal and cancellation; success transfers the final owner to the caller.</remarks>
    internal async ValueTask<ImmutablePayload> UpcastOwnedAsync(string canonicalType, int sourceVersion, ImmutablePayload owned,
        EventBufferBudget budget, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(owned);
        try
        {
            ArgumentNullException.ThrowIfNull(budget);
            int current = RequireChain(canonicalType, sourceVersion, cancellationToken);
            Validate(owned, canonicalType, sourceVersion, cancellationToken);
            byte[] sealedDigest = owned.ComputeSha256();
            for (int version = sourceVersion; version < current; version++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _registry.CapabilityLoss.RequireNoObservedLoss();
                if (!CryptographicOperations.FixedTimeEquals(sealedDigest, owned.ComputeSha256()))
                {
                    throw new InvalidOperationException("UpcasterContractViolation: sealed input changed before the next hop.");
                }
                EventRegistryRow edge = _registry.GetEdge(canonicalType, version);
                RegisteredEventUpcaster binding = GetBinding(canonicalType, version);
                byte[] before = owned.ComputeSha256();
                using var writer = new BoundedPayloadWriter(1024 * 1024, cancellationToken, budget);
                using var scratch = new BoundedScratchAllocator(128 * 1024 * 1024, budget, cancellationToken);
                EventUpcastResult result;
                using (var lease = new InvocationPayloadLease(owned, cancellationToken))
                {
                    try
                    {
                        _registry.CapabilityLoss.RequireNoObservedLoss();
                        result = await binding.Upcaster.UpcastAsync(lease, writer, scratch, cancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        lease.Dispose();
                        scratch.Dispose();
                        if (!CryptographicOperations.FixedTimeEquals(before, owned.ComputeSha256()))
                        {
                            throw new InvalidOperationException("UpcasterContractViolation: immutable input changed during invocation.");
                        }
                    }
                }

                scratch.RequireValidInvocation();
                cancellationToken.ThrowIfCancellationRequested();
                _registry.CapabilityLoss.RequireNoObservedLoss();
                if (result is null || !string.Equals(result.Domain, _registry.Domain, StringComparison.Ordinal)
                    || !string.Equals(result.EventContractType, canonicalType, StringComparison.Ordinal)
                    || result.PayloadVersion != version + 1
                    || !string.Equals(result.SerializationFormat, edge.GetTextField(3), StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("UpcasterContractViolation: returned identity or format disagrees with the admitted edge.");
                }

                ImmutablePayload next;
                try
                {
                    next = writer.TakeCompletedPayload();
                }
                catch (InvalidOperationException exception)
                {
                    throw new InvalidOperationException("UpcasterContractViolation: output writer was not completed exactly once.", exception);
                }

                try
                {
                    Validate(next, canonicalType, version + 1, cancellationToken);
                    scratch.RequireValidInvocation();
                }
                catch
                {
                    next.Dispose();
                    throw;
                }

                owned.Dispose();
                owned = next;
                sealedDigest = owned.ComputeSha256();
            }

            cancellationToken.ThrowIfCancellationRequested();
            _registry.CapabilityLoss.RequireNoObservedLoss();
            if (!CryptographicOperations.FixedTimeEquals(sealedDigest, owned.ComputeSha256()))
            {
                throw new InvalidOperationException("UpcasterContractViolation: sealed output changed before use.");
            }

            return owned;
        }
        catch
        {
            owned.Dispose();
            throw;
        }
    }

    /// <summary>Checks the entire bounded callable chain without invoking validators or upcasters.</summary>
    internal int RequireChain(string canonicalType, int sourceVersion, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _registry.CapabilityLoss.RequireNoObservedLoss();
        int current = _registry.GetCurrentVersion(canonicalType);
        _ = _registry.GetVersion(canonicalType, sourceVersion);
        if (sourceVersion > current || current - sourceVersion > 16)
        {
            throw new InvalidOperationException("UpcasterContractViolation: source does not have an admitted bounded chain.");
        }

        // Validate every callable before allocating or invoking an earlier hop.
        for (int version = sourceVersion; version <= current; version++)
        {
            _requireVersionBindings?.Invoke(canonicalType, version, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            _registry.CapabilityLoss.RequireNoObservedLoss();
        }

        for (int version = sourceVersion; version < current; version++)
        {
            GetBinding(canonicalType, version).RequireDescriptor(_registry.GetEdge(canonicalType, version), _registry.CapabilityLoss);
        }

        return current;
    }

    private RegisteredEventUpcaster GetBinding(string canonicalType, int sourceVersion)
        => _upcasters.TryGetValue((canonicalType, sourceVersion), out RegisteredEventUpcaster? binding)
            ? binding : throw new InvalidOperationException("CapabilityMismatch: an admitted edge lacks an allow-listed callable.");

    private void Validate(ImmutablePayload payload, string canonicalType, int version, CancellationToken cancellationToken)
    {
        byte[] before = payload.ComputeSha256();
        using var lease = new InvocationPayloadLease(payload, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        _registry.CapabilityLoss.RequireNoObservedLoss();
        try
        {
            _validateVersion(_registry.Domain, canonicalType, version,
                _registry.GetVersion(canonicalType, version).GetTextField(7), lease, cancellationToken);
        }
        finally
        {
            lease.Dispose();
            if (!CryptographicOperations.FixedTimeEquals(before, payload.ComputeSha256()))
            {
                throw new InvalidOperationException("UpcasterContractViolation: validation changed immutable payload bytes.");
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        _registry.CapabilityLoss.RequireNoObservedLoss();
    }
}
