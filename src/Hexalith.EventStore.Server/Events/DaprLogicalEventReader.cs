using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Events;

/// <summary>Reads one addressed actor event and resolves its allow-listed logical current view.</summary>
/// <remarks>Dapr typed readback proves only the logical value returned by the actor state API.</remarks>
internal sealed class DaprLogicalEventReader
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly IActorStateManager _stateManager;
    private readonly IEventPayloadProtectionService _protection;
    private readonly EventLogicalViewResolver _resolver;
    private readonly int _maximumReadablePageBytes;

    /// <summary>Creates the shared actor-state logical reader without provider-specific access.</summary>
    internal DaprLogicalEventReader(IActorStateManager stateManager,
        IEventPayloadProtectionService protection, EventLogicalViewResolver resolver,
        int maximumReadablePageBytes = 64 * 1024 * 1024)
    {
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        _protection = protection ?? throw new ArgumentNullException(nameof(protection));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        if (maximumReadablePageBytes is < 1 or > 64 * 1024 * 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumReadablePageBytes));
        }

        _maximumReadablePageBytes = maximumReadablePageBytes;
    }

    /// <summary>Returns the resolved current view only after addressed, readable, allow-listed state readback.</summary>
    internal async Task<DaprLogicalEventView> ReadAsync(AggregateIdentity identity, long sequenceNumber,
        CancellationToken cancellationToken, string? expectedAggregateType = null)
        => await ReadCoreAsync(identity, sequenceNumber, cancellationToken, expectedAggregateType,
            new EventBufferBudget(), 128L * 1024 * 1024, _maximumReadablePageBytes,
            requireUnversioned: false, arrayBudget: null).ConfigureAwait(false);

    private async Task<DaprLogicalEventView> ReadCoreAsync(AggregateIdentity identity, long sequenceNumber,
        CancellationToken cancellationToken, string? expectedAggregateType, EventBufferBudget budget,
        long maximumStoredBytes, long maximumReadableBytes, bool requireUnversioned, LegacyEventArrayBudget? arrayBudget)
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
        if (source.Payload.Length > maximumStoredBytes)
        {
            throw new InvalidOperationException("RawEnvelopeLimit: the next logical event exceeds the remaining page capacity.");
        }

        if (requireUnversioned)
        {
            LegacyEventReadGuard.RequireUnversioned(source);
        }

        arrayBudget?.Add(source);

        _resolver.RequireSourceRoute(source.Domain, source.EventTypeName, source.MetadataVersion,
            source.EventContractType, source.PayloadVersion, source.AggregateType);

        // Snapshot mutable extension metadata before calling protection or domain code.
        // Its source and private dictionary capacities have a separate bounded admission.
        using EventBufferReservation metadataReservation = budget.Reserve(512 * 1024);
        source = SnapshotMetadata(source);

        byte[] storedHash = SHA256.HashData(source.Payload);
        byte[]? protectedCopy = null;
        byte[]? readablePayload = null;
        EventBufferReservation? protectedReservation = null;
        EventBufferReservation? readableReservation = null;
        try
        {
            EventStorePayloadProtectionMetadata metadata = EventStorePayloadProtectionMetadataCarrier.Read(source.Extensions);
            if (metadata.State == PayloadProtectionState.ProviderOpaque)
            {
                throw new ProtectedDataUnreadableException(
                    UnreadableProtectedDataReason.ProviderOpaqueUnsupportedOperation,
                    stage: ProtectedDataReadabilityDecisionStageCodes.Rehydrate,
                    sequenceNumber: sequenceNumber);
            }

            byte[] protectionInput;
            if (_protection is NoOpEventPayloadProtectionService)
            {
                // The sealed platform no-op returns this same array and invokes no
                // user code. The resolver takes the sole charged private copy below.
                protectionInput = source.Payload;
            }
            else
            {
                protectedReservation = budget.Reserve(source.Payload.Length);
                // An arbitrary array-returning provider retains its conservative
                // output reservation; the no-op's alias needs no second capacity.
                readableReservation = budget.Reserve(checked((int)maximumReadableBytes));
                protectedCopy = source.Payload.ToArray();
                protectionInput = protectedCopy;
            }

            PayloadUnprotectionOutcome outcome;
            try
            {
                outcome = await _protection.TryUnprotectEventPayloadAsync(
                    identity, source.EventTypeName, protectionInput, source.SerializationFormat,
                    metadata, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                throw new ProtectedDataUnreadableException(
                    UnreadableProtectedDataReason.ProviderUnavailable,
                    stage: ProtectedDataReadabilityDecisionStageCodes.Rehydrate,
                    sequenceNumber: sequenceNumber);
            }

            readablePayload = outcome.PayloadBytes;
            cancellationToken.ThrowIfCancellationRequested();
            if (!outcome.IsReadable || outcome.PayloadBytes is null || outcome.SerializationFormat is null)
            {
                throw new ProtectedDataUnreadableException(
                    outcome.UnreadableReason ?? UnreadableProtectedDataReason.ProviderUnavailable,
                    stage: ProtectedDataReadabilityDecisionStageCodes.Rehydrate,
                    sequenceNumber: sequenceNumber);
            }

            if (outcome.PayloadBytes.Length > maximumReadableBytes)
            {
                throw new InvalidOperationException("ReadableLimit: the next logical payload exceeds the remaining page capacity.");
            }

            // The provider has returned: only its distinct actual array remains live.
            // An alias of the protected input already has that input's reservation.
            readableReservation?.ShrinkTo(ReferenceEquals(outcome.PayloadBytes, protectedCopy)
                || ReferenceEquals(outcome.PayloadBytes, source.Payload) ? 0 : outcome.PayloadBytes.Length);

            EventLogicalDigest.RequireMatching(source, outcome.SerializationFormat, outcome.PayloadBytes);

            ResolvedLogicalEvent resolved = await _resolver.ResolveAsync(identity.Domain,
                source.EventTypeName, source.MetadataVersion, source.EventContractType, source.PayloadVersion,
                outcome.SerializationFormat, outcome.PayloadBytes, cancellationToken,
                budget, source.AggregateType).ConfigureAwait(false);
            if (!CryptographicOperations.FixedTimeEquals(storedHash, SHA256.HashData(source.Payload)))
            {
                resolved.Dispose();
                throw new InvalidOperationException("AddressMismatch: actor event bytes changed during logical resolution.");
            }

            return new DaprLogicalEventView(source, resolved, outcome.PayloadBytes.Length);
        }
        finally
        {
            if (readablePayload is not null && !ReferenceEquals(readablePayload, protectedCopy)
                && !ReferenceEquals(readablePayload, source.Payload))
            {
                CryptographicOperations.ZeroMemory(readablePayload);
            }

            if (protectedCopy is not null)
            {
                CryptographicOperations.ZeroMemory(protectedCopy);
            }

            CryptographicOperations.ZeroMemory(storedHash);
            readableReservation?.Dispose();
            protectedReservation?.Dispose();
        }
    }

    /// <summary>Reads a bounded contiguous logical page pinned to one actor metadata head.</summary>
    /// <remarks>The two metadata reads are Dapr logical observations, not provider attestations.</remarks>
    internal async Task<DaprLogicalEventPage> ReadPageAsync(AggregateIdentity identity, string aggregateType,
        long startSequence, int maxCount, CancellationToken cancellationToken,
        long? expectedActorHead = null, long? expectedRetainedFloor = null, EventBufferBudget? sharedBudget = null,
        bool requireUnversioned = false, LegacyEventArrayBudget? arrayBudget = null)
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
        long floor = before.HasValue ? before.Value.RetainedFloor : 1;
        if (head < 0 || floor < 1 || (head > 0 && floor > head && floor - head > 1)
            || (head == 0 && floor != 1)
            || (expectedActorHead.HasValue && expectedActorHead.Value != head)
            || (expectedRetainedFloor.HasValue && expectedRetainedFloor.Value != floor))
        {
            throw new InvalidOperationException("SourceHeadChanged: actor head or retained floor disagrees with the fixed logical read.");
        }
        if (startSequence < floor && startSequence <= head)
        {
            throw new InvalidOperationException("ReplayRestartRequired: the requested prefix is below the retained floor.");
        }

        int count = startSequence > head ? 0 : (int)Math.Min(maxCount, head - startSequence + 1);
        var views = new DaprLogicalEventView[count];
        long storedBytes = 0;
        long readableBytes = 0;
        EventBufferBudget budget = sharedBudget ?? new EventBufferBudget();
        try
        {
            for (int index = 0; index < count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                DaprLogicalEventView view = await ReadCoreAsync(identity, startSequence + index,
                    cancellationToken, aggregateType, budget,
                    128L * 1024 * 1024 - storedBytes, _maximumReadablePageBytes - readableBytes,
                    requireUnversioned, arrayBudget).ConfigureAwait(false);
                views[index] = view;
                storedBytes = checked(storedBytes + view.StoredPayloadLength);
                if (storedBytes > 128L * 1024 * 1024)
                {
                    throw new InvalidOperationException("RawEnvelopeLimit: a logical event page exceeds 128 MiB of stored payload bytes.");
                }

                readableBytes = checked(readableBytes + view.ReadablePayloadLength);
                if (readableBytes > _maximumReadablePageBytes)
                {
                    throw new InvalidOperationException("ReadableLimit: a logical event page exceeds 64 MiB.");
                }
            }

            ConditionalValue<AggregateMetadata> after = await ReadMetadataAsync(identity, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (after.HasValue != before.HasValue
                || (after.HasValue && (after.Value.CurrentSequence != head
                    || after.Value.RetainedFloor != floor
                    || !string.Equals(after.Value.ETag, before.Value.ETag, StringComparison.Ordinal))))
            {
                throw new InvalidOperationException("SourceHeadChanged: actor metadata changed during the logical page read.");
            }

            return new DaprLogicalEventPage(startSequence, head, views, floor);
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

    private static EventEnvelope SnapshotMetadata(EventEnvelope source)
    {
        long encodedBytes = 0;
        foreach (string value in new[] { source.MessageId, source.AggregateId, source.AggregateType,
            source.TenantId, source.Domain, source.CorrelationId, source.CausationId, source.UserId,
            source.DomainServiceVersion, source.EventTypeName, source.SerializationFormat,
            source.EventContractType ?? string.Empty, source.ApplicationPayloadDigest ?? string.Empty })
        {
            encodedBytes = checked(encodedBytes + StrictUtf8.GetByteCount(value) * 6L + 64);
        }

        Dictionary<string, string>? extensions = null;
        if (source.Extensions is not null)
        {
            extensions = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach ((string key, string value) in source.Extensions)
            {
                encodedBytes = checked(encodedBytes + 6L * StrictUtf8.GetByteCount(key)
                    + 6L * StrictUtf8.GetByteCount(value) + 64);
                RequireMetadataCapacity(encodedBytes);
                extensions.Add(key, value);
            }
        }

        RequireMetadataCapacity(encodedBytes);
        return source with { Extensions = extensions is null ? null : new ReadOnlyDictionary<string, string>(extensions) };
    }

    private static void RequireMetadataCapacity(long encodedBytes)
    {
        if (encodedBytes > 512 * 1024)
        {
            throw new InvalidOperationException("MetadataLimit: logical event metadata exceeds 512 KiB.");
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
