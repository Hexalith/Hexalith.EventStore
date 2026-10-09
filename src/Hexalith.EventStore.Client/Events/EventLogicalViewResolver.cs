using System.Security.Cryptography;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Resolves V1 aliases and V2 metadata through one closed registry and bounded upcast chain.</summary>
/// <remarks>The caller must first obtain and unprotect a logically addressed actor value.</remarks>
internal sealed class EventLogicalViewResolver
{
    private readonly EventDomainRegistry _registry;
    private readonly EventUpcastChainExecutor _executor;

    /// <summary>Captures the exact registry and its registered chain executor.</summary>
    internal EventLogicalViewResolver(EventDomainRegistry registry, EventUpcastChainExecutor executor)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        if (!ReferenceEquals(_registry, _executor.Registry))
        {
            throw new InvalidOperationException("CapabilityMismatch: logical resolver and upcast executor use different registries.");
        }
    }

    /// <summary>Returns an owned current view while retaining the original application bytes unchanged.</summary>
    internal async ValueTask<ResolvedLogicalEvent> ResolveAsync(string domain, string eventTypeName,
        int metadataVersion, string? eventContractType, int? payloadVersion, string serializationFormat,
        ReadOnlyMemory<byte> originalApplicationPayload, CancellationToken cancellationToken,
        EventBufferBudget? sharedBudget = null, string? aggregateType = null,
        Func<CancellationToken, Task>? sourceFence = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _registry.CapabilityLoss.RequireNoObservedLoss();
        _ = ResolveSource(domain, eventTypeName, metadataVersion, eventContractType, payloadVersion,
            serializationFormat, aggregateType);
        if (originalApplicationPayload.Length > 64 * 1024 * 1024)
        {
            throw new InvalidOperationException("ReadableLimit: logical source payload exceeds 64 MiB.");
        }

        EventBufferBudget budget = sharedBudget ?? new EventBufferBudget();
        EventBufferReservation reservation = budget.Reserve(originalApplicationPayload.Length);
        byte[]? sourceBytes = null;
        byte[]? before = null;
        bool sourceOwnsReservation = false;
        try
        {
            before = SHA256.HashData(originalApplicationPayload.Span);
            sourceBytes = originalApplicationPayload.ToArray();
            var source = new ImmutablePayload(sourceBytes, sourceBytes.Length, cancellationToken, reservation);
            sourceOwnsReservation = true;
            sourceBytes = null;
            ResolvedLogicalEvent resolved = await ResolveOwnedAsync(domain, eventTypeName, metadataVersion,
                eventContractType, payloadVersion, serializationFormat, source, budget,
                cancellationToken, aggregateType, sourceFence).ConfigureAwait(false);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                byte[] after = SHA256.HashData(originalApplicationPayload.Span);
                try
                {
                    if (!CryptographicOperations.FixedTimeEquals(before, after))
                    {
                        throw new InvalidOperationException("UpcasterContractViolation: original application payload changed during resolution.");
                    }
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(after);
                }

                cancellationToken.ThrowIfCancellationRequested();
                _registry.CapabilityLoss.RequireNoObservedLoss();
                return resolved;
            }
            catch
            {
                resolved.Dispose();
                throw;
            }
        }
        catch
        {
            if (!sourceOwnsReservation)
            {
                if (sourceBytes is not null)
                {
                    CryptographicOperations.ZeroMemory(sourceBytes);
                }

                reservation.Dispose();
            }

            throw;
        }
        finally
        {
            if (before is not null)
            {
                CryptographicOperations.ZeroMemory(before);
            }
        }
    }

    /// <summary>Consumes a prepared charged readable owner without making a second page-sized copy.</summary>
    /// <remarks>Ownership transfers at entry, including cancellation and admission refusal.</remarks>
    internal async ValueTask<ResolvedLogicalEvent> ResolveOwnedAsync(string domain, string eventTypeName,
        int metadataVersion, string? eventContractType, int? payloadVersion, string serializationFormat,
        ImmutablePayload readablePayload, EventBufferBudget budget, CancellationToken cancellationToken,
        string? aggregateType = null, Func<CancellationToken, Task>? sourceFence = null)
    {
        ArgumentNullException.ThrowIfNull(readablePayload);
        try
        {
            ArgumentNullException.ThrowIfNull(budget);
            cancellationToken.ThrowIfCancellationRequested();
            _registry.RequireActive(cancellationToken);
            (string canonicalType, int sourceVersion) = ResolveSource(domain, eventTypeName, metadataVersion,
                eventContractType, payloadVersion, serializationFormat, aggregateType);
            if (readablePayload.Length > 64 * 1024 * 1024)
            {
                throw new InvalidOperationException("ReadableLimit: logical source payload exceeds 64 MiB.");
            }

            ImmutablePayload effective = await _executor.UpcastOwnedAsync(canonicalType, sourceVersion,
                readablePayload, budget, cancellationToken, sourceFence).ConfigureAwait(false);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                _registry.RequireActive(cancellationToken);
                int currentVersion = _registry.GetCurrentVersion(canonicalType);
                return new ResolvedLogicalEvent(canonicalType, sourceVersion, currentVersion,
                    _registry.GetVersion(canonicalType, currentVersion).GetTextField(7), effective);
            }
            catch
            {
                effective.Dispose();
                throw;
            }
        }
        catch
        {
            readablePayload.Dispose();
            throw;
        }
    }

    private (string Type, int Version) ResolveSource(string domain, string eventTypeName, int metadataVersion,
        string? eventContractType, int? payloadVersion, string serializationFormat, string? aggregateType)
    {
        if (!string.Equals(domain, _registry.Domain, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("UnknownEventContract: event domain is not the registered domain.");
        }

        string canonicalType;
        int sourceVersion;
        string sourceFormat;
        if (metadataVersion == 1 && eventContractType is null && payloadVersion is (null or >= 1 and <= 1024))
        {
            (canonicalType, sourceVersion, sourceFormat) = _registry.ResolveAlias(eventTypeName);
        }
        else if (metadataVersion == 2 && eventContractType is not null && payloadVersion is >= 1 and <= 1024
            && string.Equals(eventTypeName, eventContractType, StringComparison.Ordinal))
        {
            canonicalType = eventContractType;
            sourceVersion = payloadVersion.Value;
            sourceFormat = _registry.GetVersion(canonicalType, sourceVersion).GetTextField(7);
        }
        else
        {
            throw new InvalidOperationException("UnknownEventContract: incomplete or unsupported event metadata.");
        }

        RequireAggregateRoute(canonicalType, aggregateType);
        if (!string.Equals(serializationFormat, sourceFormat, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("UnknownEventContract: source format disagrees with its registered version.");
        }

        return (canonicalType, sourceVersion);
    }

    /// <summary>Checks readable identity, format and complete callable admission without executing catalog code.</summary>
    internal void RequireReadableSource(string domain, string eventTypeName, int metadataVersion,
        string? eventContractType, int? payloadVersion, string serializationFormat,
        string aggregateType, CancellationToken cancellationToken)
    {
        RequireNoObservedLoss(cancellationToken);
        (string canonicalType, int sourceVersion) = ResolveSource(domain, eventTypeName, metadataVersion,
            eventContractType, payloadVersion, serializationFormat, aggregateType);
        _ = _executor.RequireChain(canonicalType, sourceVersion, cancellationToken);
    }

    /// <summary>Checks original cancellation and the catalog's shared loss control at outer owner boundaries.</summary>
    internal void RequireNoObservedLoss(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _registry.RequireActive(cancellationToken);
        _registry.CapabilityLoss.RequireNoObservedLoss();
    }

    /// <summary>Refuses an addressed route that disagrees with the immutable domain manifest.</summary>
    internal void RequireSourceRoute(string domain, string eventTypeName, int metadataVersion,
        string? eventContractType, int? payloadVersion, string aggregateType)
    {
        _registry.CapabilityLoss.RequireNoObservedLoss();
        if (!string.Equals(domain, _registry.Domain, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("UnknownEventContract: event domain is not the registered domain.");
        }

        string canonicalType;
        if (metadataVersion == 1 && eventContractType is null && payloadVersion is (null or >= 1 and <= 1024))
        {
            (canonicalType, _, _) = _registry.ResolveAlias(eventTypeName);
        }
        else if (metadataVersion == 2 && eventContractType is not null && payloadVersion is >= 1 and <= 1024
            && string.Equals(eventTypeName, eventContractType, StringComparison.Ordinal))
        {
            canonicalType = eventContractType;
            _ = _registry.GetVersion(canonicalType, payloadVersion.Value);
        }
        else
        {
            throw new InvalidOperationException("UnknownEventContract: incomplete or unsupported event metadata.");
        }

        RequireAggregateRoute(canonicalType, aggregateType);
    }

    private void RequireAggregateRoute(string canonicalType, string? aggregateType)
    {
        if (aggregateType is not null
            && !string.Equals(_registry.GetAggregateRoute(canonicalType), aggregateType, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("AddressMismatch: event contract belongs to another aggregate route.");
        }
    }
}
