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
    private readonly EventVersionValidatorAsync? _validateVersionAsync;
    private readonly Action<string, int, CancellationToken>? _requireVersionBindings;

    /// <summary>Gets the exact registry instance admitted by this executor.</summary>
    internal EventDomainRegistry Registry => _registry;

    /// <summary>Captures immutable registrations; source authentication and complete runtime attestation remain caller prerequisites.</summary>
    internal EventUpcastChainExecutor(EventDomainRegistry registry,
        IReadOnlyDictionary<(string Type, int Version), RegisteredEventUpcaster> upcasters,
        EventVersionValidator validateVersion,
        Action<string, int, CancellationToken>? requireVersionBindings = null,
        EventVersionValidatorAsync? validateVersionAsync = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(upcasters);
        ArgumentNullException.ThrowIfNull(validateVersion);
        _registry = registry;
        _registry.CapabilityLoss.RequireNoObservedLoss();
        _upcasters = upcasters.ToFrozenDictionary();
        _validateVersion = validateVersion;
        _validateVersionAsync = validateVersionAsync;
        _requireVersionBindings = requireVersionBindings;
        foreach (((string type, int source), RegisteredEventUpcaster binding) in _upcasters)
        {
            binding.RequireDescriptor(registry.GetEdge(type, source), registry.CapabilityLoss);
        }
    }

    /// <summary>Returns a charged exclusive current owner only after every hop has passed descriptor/schema/identity checks.</summary>
    internal ValueTask<ImmutablePayload> UpcastAsync(string canonicalType, int sourceVersion, IReadOnlyPayload source,
        EventBufferBudget budget, CancellationToken cancellationToken, Func<CancellationToken, Task>? sourceFence = null)
    {
        ArgumentNullException.ThrowIfNull(budget);
        RequireChain(canonicalType, sourceVersion, cancellationToken);
        return UpcastOwnedAsync(canonicalType, sourceVersion,
            ImmutablePayload.CopyFrom(source, budget, cancellationToken), budget, cancellationToken, sourceFence);
    }

    /// <summary>Consumes an exclusive charged source owner without allocating a second copy.</summary>
    /// <remarks>Ownership transfers at entry, including refusal and cancellation; success transfers the final owner to the caller.</remarks>
    internal async ValueTask<ImmutablePayload> UpcastOwnedAsync(string canonicalType, int sourceVersion, ImmutablePayload owned,
        EventBufferBudget budget, CancellationToken cancellationToken, Func<CancellationToken, Task>? sourceFence = null)
    {
        ArgumentNullException.ThrowIfNull(owned);
        try
        {
            ArgumentNullException.ThrowIfNull(budget);
            int current = RequireChain(canonicalType, sourceVersion, cancellationToken);
            await ValidateAsync(owned, canonicalType, sourceVersion, sourceFence, cancellationToken).ConfigureAwait(false);
            byte[] sealedDigest = owned.ComputeSha256();
            for (int version = sourceVersion; version < current; version++)
            {
                await RequireBoundaryAsync(canonicalType, sourceVersion, sourceFence, cancellationToken).ConfigureAwait(false);
                if (!CryptographicOperations.FixedTimeEquals(sealedDigest, owned.ComputeSha256()))
                {
                    throw new InvalidOperationException("UpcasterContractViolation: sealed input changed before the next hop.");
                }
                EventRegistryRow edge = _registry.GetEdge(canonicalType, version);
                RegisteredEventUpcaster binding = GetBinding(canonicalType, version);
                byte[] before = owned.ComputeSha256();
                var scratch = new BoundedScratchAllocator(128 * 1024 * 1024, budget, cancellationToken);
                ImmutablePayload? next = null;
                try
                {
                    using (var writer = new BoundedPayloadWriter(1024 * 1024, cancellationToken, budget))
                    using (scratch)
                    {
                        EventUpcastResult result;
                        using (var lease = new InvocationPayloadLease(owned, cancellationToken))
                        {
                            try
                            {
                                result = await binding.Upcaster.UpcastAsync(lease, writer, scratch, cancellationToken).ConfigureAwait(false);
                            }
                            finally
                            {
                                lease.Dispose();
                                scratch.Dispose();
                                cancellationToken.ThrowIfCancellationRequested();
                                if (!CryptographicOperations.FixedTimeEquals(before, owned.ComputeSha256()))
                                {
                                    throw new InvalidOperationException("UpcasterContractViolation: immutable input changed during invocation.");
                                }
                            }
                        }

                        cancellationToken.ThrowIfCancellationRequested();
                        _registry.RequireActive(cancellationToken);
                        _registry.CapabilityLoss.RequireNoObservedLoss();
                        binding.RequireDescriptor(edge, _registry.CapabilityLoss);
                        if (!CryptographicOperations.FixedTimeEquals(before, owned.ComputeSha256()))
                        {
                            throw new InvalidOperationException("UpcasterContractViolation: immutable input changed during invocation.");
                        }
                        scratch.RequireValidInvocation();
                        if (result is null || !string.Equals(result.Domain, _registry.Domain, StringComparison.Ordinal)
                            || !string.Equals(result.EventContractType, canonicalType, StringComparison.Ordinal)
                            || result.PayloadVersion != version + 1
                            || !string.Equals(result.SerializationFormat, edge.GetTextField(3), StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException("UpcasterContractViolation: returned identity or format disagrees with the admitted edge.");
                        }

                        try
                        {
                            next = writer.TakeCompletedPayload();
                        }
                        catch (InvalidOperationException exception)
                        {
                            throw new InvalidOperationException("UpcasterContractViolation: output writer was not completed exactly once.", exception);
                        }
                    }

                    // Input, output and scratch facades expire before addressed readback yields.
                    await RequireBoundaryAsync(canonicalType, sourceVersion, sourceFence, cancellationToken).ConfigureAwait(false);
                    await ValidateAsync(next, canonicalType, version + 1, sourceFence, cancellationToken).ConfigureAwait(false);
                    scratch.RequireValidInvocation();
                    owned.Dispose();
                    owned = next;
                    next = null;
                    sealedDigest = owned.ComputeSha256();
                }
                finally
                {
                    scratch.Dispose();
                    next?.Dispose();
                }
            }

            await RequireBoundaryAsync(canonicalType, sourceVersion, sourceFence, cancellationToken).ConfigureAwait(false);
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
        _registry.RequireActive(cancellationToken);
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

    private ValueTask RequireBoundaryAsync(string canonicalType, int sourceVersion,
        Func<CancellationToken, Task>? sourceFence, CancellationToken token)
        => EventCallbackFence.RequireAsync(_registry, sourceFence, token,
            () => RequireChain(canonicalType, sourceVersion, token));

    private async ValueTask ValidateAsync(ImmutablePayload payload, string canonicalType, int version,
        Func<CancellationToken, Task>? sourceFence, CancellationToken cancellationToken)
    {
        await RequireBoundaryAsync(canonicalType, version, sourceFence, cancellationToken).ConfigureAwait(false);
        byte[] before = payload.ComputeSha256();
        try
        {
            string format = _registry.GetVersion(canonicalType, version).GetTextField(7);
            if (_validateVersionAsync is not null)
            {
                await _validateVersionAsync(_registry.Domain, canonicalType, version, format,
                    payload, sourceFence, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                using var lease = new InvocationPayloadLease(payload, cancellationToken);
                _validateVersion(_registry.Domain, canonicalType, version, format, lease, cancellationToken);
            }
        }
        finally
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!CryptographicOperations.FixedTimeEquals(before, payload.ComputeSha256()))
                {
                    throw new InvalidOperationException("UpcasterContractViolation: validation changed immutable payload bytes.");
                }
            }
            finally { CryptographicOperations.ZeroMemory(before); }
        }

        await RequireBoundaryAsync(canonicalType, version, sourceFence, cancellationToken).ConfigureAwait(false);
    }
}
