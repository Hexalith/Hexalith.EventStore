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
        EventBufferBudget? sharedBudget = null, string? aggregateType = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.Equals(domain, _registry.Domain, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("UnknownEventContract: event domain is not the registered domain.");
        }

        string canonicalType;
        int sourceVersion;
        string sourceFormat;
        if (metadataVersion == 1 && eventContractType is null && payloadVersion is null)
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
            using var source = new ImmutablePayload(sourceBytes, sourceBytes.Length, cancellationToken, reservation);
            sourceOwnsReservation = true;
            sourceBytes = null;
            ImmutablePayload effective = await _executor.UpcastAsync(
                canonicalType, sourceVersion, source, budget, cancellationToken).ConfigureAwait(false);
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

                return new ResolvedLogicalEvent(canonicalType, sourceVersion,
                    _registry.GetCurrentVersion(canonicalType),
                    _registry.GetVersion(canonicalType, _registry.GetCurrentVersion(canonicalType)).GetTextField(7),
                    effective);
            }
            catch
            {
                effective.Dispose();
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

    /// <summary>Refuses an addressed route that disagrees with the immutable domain manifest.</summary>
    internal void RequireSourceRoute(string domain, string eventTypeName, int metadataVersion,
        string? eventContractType, int? payloadVersion, string aggregateType)
    {
        if (!string.Equals(domain, _registry.Domain, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("UnknownEventContract: event domain is not the registered domain.");
        }

        string canonicalType;
        if (metadataVersion == 1 && eventContractType is null && payloadVersion is null)
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
