using System.Security.Cryptography;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Events;

/// <summary>Reads one addressed actor event and resolves its allow-listed logical current view.</summary>
/// <remarks>Dapr typed readback proves only the logical value returned by the actor state API.</remarks>
internal sealed class DaprLogicalEventReader
{
    private readonly IActorStateManager _stateManager;
    private readonly IEventPayloadProtectionService _protection;
    private readonly EventLogicalViewResolver _resolver;

    /// <summary>Creates the shared actor-state logical reader without provider-specific access.</summary>
    internal DaprLogicalEventReader(IActorStateManager stateManager,
        IEventPayloadProtectionService protection, EventLogicalViewResolver resolver)
    {
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        _protection = protection ?? throw new ArgumentNullException(nameof(protection));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    /// <summary>Returns the resolved current view only after addressed, readable, allow-listed state readback.</summary>
    internal async Task<DaprLogicalEventView> ReadAsync(AggregateIdentity identity, long sequenceNumber,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sequenceNumber);
        cancellationToken.ThrowIfCancellationRequested();
        ConditionalValue<EventEnvelope> result;
        try
        {
            result = await _stateManager.TryGetStateAsync<EventEnvelope>(
                $"{identity.EventStreamKeyPrefix}{sequenceNumber}", cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            throw new EventDeserializationException(sequenceNumber, identity.ActorId, error);
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!result.HasValue)
        {
            throw new MissingEventException(sequenceNumber, identity.TenantId, identity.Domain, identity.AggregateId);
        }

        EventEnvelope source = result.Value;
        if (!string.Equals(source.TenantId, identity.TenantId, StringComparison.Ordinal)
            || !string.Equals(source.Domain, identity.Domain, StringComparison.Ordinal)
            || !string.Equals(source.AggregateId, identity.AggregateId, StringComparison.Ordinal)
            || source.SequenceNumber != sequenceNumber || source.Payload is null)
        {
            throw new InvalidOperationException("AddressMismatch: actor event identity or sequence disagrees with its key.");
        }
        if (source.Payload.Length > 64 * 1024 * 1024)
        {
            throw new InvalidOperationException("ReadableLimit: stored logical event exceeds 64 MiB.");
        }

        byte[] storedHash = SHA256.HashData(source.Payload);
        byte[]? protectedCopy = null;
        try
        {
            protectedCopy = source.Payload.ToArray();
            EventStorePayloadProtectionMetadata metadata = EventStorePayloadProtectionMetadataCarrier.Read(source.Extensions);
            PayloadUnprotectionOutcome outcome = await _protection.TryUnprotectEventPayloadAsync(
                identity, source.EventTypeName, protectedCopy, source.SerializationFormat,
                metadata, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (!outcome.IsReadable || outcome.PayloadBytes is null || outcome.SerializationFormat is null)
            {
                throw new ProtectedDataUnreadableException(
                    outcome.UnreadableReason ?? UnreadableProtectedDataReason.ProviderUnavailable,
                    stage: ProtectedDataReadabilityDecisionStageCodes.Rehydrate,
                    sequenceNumber: sequenceNumber);
            }

            if (source.MetadataVersion == 2 && source.ApplicationPayloadDigest is null)
            {
                throw new InvalidOperationException("LogicalDigestMismatch: a versioned event lacks its application digest.");
            }
            if (source.ApplicationPayloadDigest is not null)
            {
                byte[] applicationHash = EventLogicalDigest.HashPayload(outcome.PayloadBytes);
                try
                {
                    string actual = EventLogicalDigest.Compute(source, outcome.SerializationFormat, applicationHash);
                    if (!string.Equals(source.ApplicationPayloadDigest, actual, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("LogicalDigestMismatch: actor logical event bytes or metadata changed.");
                    }
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(applicationHash);
                }
            }

            ResolvedLogicalEvent resolved = await _resolver.ResolveAsync(identity.Domain,
                source.EventTypeName, source.MetadataVersion, source.EventContractType, source.PayloadVersion,
                outcome.SerializationFormat, outcome.PayloadBytes, cancellationToken).ConfigureAwait(false);
            if (!CryptographicOperations.FixedTimeEquals(storedHash, SHA256.HashData(source.Payload)))
            {
                resolved.Dispose();
                throw new InvalidOperationException("AddressMismatch: actor event bytes changed during logical resolution.");
            }

            return new DaprLogicalEventView(source, resolved);
        }
        finally
        {
            if (protectedCopy is not null)
            {
                CryptographicOperations.ZeroMemory(protectedCopy);
            }

            CryptographicOperations.ZeroMemory(storedHash);
        }
    }
}
