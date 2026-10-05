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
        CancellationToken cancellationToken, string? expectedAggregateType = null)
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
            || (expectedAggregateType is not null
                && !string.Equals(source.AggregateType, expectedAggregateType, StringComparison.Ordinal))
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

            EventLogicalDigest.RequireMatching(source, outcome.SerializationFormat, outcome.PayloadBytes);

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

    /// <summary>Reads a bounded contiguous logical page pinned to one actor metadata head.</summary>
    /// <remarks>The two metadata reads are Dapr logical observations, not provider attestations.</remarks>
    internal async Task<DaprLogicalEventPage> ReadPageAsync(AggregateIdentity identity, string aggregateType,
        long startSequence, int maxCount, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(startSequence);
        if (maxCount is < 1 or > 256)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCount));
        }

        cancellationToken.ThrowIfCancellationRequested();
        ConditionalValue<AggregateMetadata> before = await ReadMetadataAsync(identity, cancellationToken).ConfigureAwait(false);
        long head = before.HasValue ? before.Value.CurrentSequence : 0;
        if (head < 0)
        {
            throw new InvalidOperationException("SourceHeadChanged: actor metadata has an invalid negative head.");
        }

        int count = startSequence > head ? 0 : (int)Math.Min(maxCount, head - startSequence + 1);
        var views = new DaprLogicalEventView[count];
        long storedBytes = 0;
        long readableBytes = 0;
        try
        {
            for (int index = 0; index < count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                DaprLogicalEventView view = await ReadAsync(identity, startSequence + index,
                    cancellationToken, aggregateType).ConfigureAwait(false);
                views[index] = view;
                storedBytes = checked(storedBytes + view.StoredPayloadLength);
                if (storedBytes > 128L * 1024 * 1024)
                {
                    throw new InvalidOperationException("RawEnvelopeLimit: a logical event page exceeds 128 MiB of stored payload bytes.");
                }

                readableBytes = checked(readableBytes + view.Resolved.Payload.Length);
                if (readableBytes > 64L * 1024 * 1024)
                {
                    throw new InvalidOperationException("ReadableLimit: a logical event page exceeds 64 MiB.");
                }
            }

            ConditionalValue<AggregateMetadata> after = await ReadMetadataAsync(identity, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (after.HasValue != before.HasValue
                || (after.HasValue && (after.Value.CurrentSequence != head
                    || after.Value.RetainedFloor != before.Value.RetainedFloor)))
            {
                throw new InvalidOperationException("SourceHeadChanged: actor metadata changed during the logical page read.");
            }

            return new DaprLogicalEventPage(startSequence, head, views);
        }
        catch
        {
            foreach (DaprLogicalEventView? view in views)
            {
                view?.Dispose();
            }

            throw;
        }
    }

    private async Task<ConditionalValue<AggregateMetadata>> ReadMetadataAsync(
        AggregateIdentity identity, CancellationToken cancellationToken)
    {
        try
        {
            return await _stateManager.TryGetStateAsync<AggregateMetadata>(
                identity.MetadataKey, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            throw new EventDeserializationException(-1, identity.ActorId, error);
        }
    }
}
