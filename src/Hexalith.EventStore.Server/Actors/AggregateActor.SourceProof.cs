using Dapr.Actors.Runtime;
using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Events;

namespace Hexalith.EventStore.Server.Actors;

public partial class AggregateActor
{
    /// <inheritdoc/>
    public async Task<SourceProofInspection> InspectSourceProofAsync()
    {
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SourceInspections);
        // Recovery may finalize commands or reconcile counters. It belongs to the
        // existing recovery turn, and cannot be hidden inside a read-only Current probe.
        if (_stateCacheUnsafe || _pendingFinalizerRecoveryRequired || _pendingCountReconciliationRequired)
            throw new InvalidOperationException("delivery_state_unavailable");
        await EnsureStateCacheBarrierAsync(correlationId: null, activity: null).ConfigureAwait(false);
        await ClearSourceReadCacheAsync().ConfigureAwait(false);
        AggregateIdentity identity = GetAggregateIdentityFromActorId();
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SourceMetadataReads);
        ConditionalValue<AggregateMetadata> stored = await StateManager
            .TryGetStateAsync<AggregateMetadata>(identity.MetadataKey).ConfigureAwait(false);
        if (stored.HasValue && MeasureSourceMetadata(stored.Value) > 4096)
            return new(SourceProofInspectionStatus.Invalid, true, stored.Value.CurrentSequence, false, null);
        if (!stored.HasValue)
        {
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SourceMetadataReads);
            ConditionalValue<TrustedEffectErasureProgress> erasure = await StateManager
                .TryGetStateAsync<TrustedEffectErasureProgress>(TrustedEffectErasureProgress.StateName)
                .ConfigureAwait(false);
            if (erasure.HasValue)
            {
                long bytes = SourceActorStateBodySize.Count(erasure.Value, ActorStateJsonSerializerOptions);
                ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MetadataSizingBytes, bytes);
                ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SourceMetadataBytes, bytes);
            }
            return new(erasure.HasValue && !erasure.Value.Completed ? SourceProofInspectionStatus.Invalid : SourceProofInspectionStatus.Unsupported,
                false, 0, erasure.HasValue, null);
        }

        AggregateMetadata metadata = stored.Value;
        if (metadata.SourceErasurePending)
            return new(SourceProofInspectionStatus.Invalid, true, metadata.CurrentSequence, true, null);
        if (metadata.CurrentSequence < 0 || metadata.RetainedFloor < 1 || metadata.RetainedFloor - 1 > metadata.CurrentSequence)
            return new(SourceProofInspectionStatus.Invalid, true, metadata.CurrentSequence, false, null);
        if (metadata.SourceProof is not SourceProof proof || proof.CodecVersion != SourceProofCodec.Version)
        {
            return new(SourceProofInspectionStatus.Unsupported, true, metadata.CurrentSequence, false, null);
        }

        bool valid = !proof.Erased && SourceProofCodec.IsValid(proof, identity,
            metadata.CurrentSequence, metadata.RetainedFloor);
        return new(valid ? SourceProofInspectionStatus.Available : SourceProofInspectionStatus.Invalid,
            true, metadata.CurrentSequence, false, valid ? proof : null);
    }

    /// <inheritdoc/>
    public async Task<SourceEventRange> ReadNativeEventsBoundedRangeAsync(long fromSequence, long toSequence, int maxCount, int maxBytes)
    {
        SourceProofInspection inspection = await InspectSourceProofAsync().ConfigureAwait(false);
        if (inspection.Status != SourceProofInspectionStatus.Available || inspection.Erased)
            return EmptySourceRange("source_conflict");
        return await ReadMeasuredEventsBoundedRangeAsync(fromSequence, toSequence, maxCount, maxBytes).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<EventEnvelope[]> ReadEventsBoundedRangeAsync(
        long fromSequence, long toSequence, int maxCount, int maxBytes)
    {
        SourceEventRange range = await ReadMeasuredEventsBoundedRangeAsync(fromSequence, toSequence, maxCount, maxBytes).ConfigureAwait(false);
        if (range.ReasonCode is not null) throw new InvalidOperationException(range.ReasonCode);
        return range.Events;
    }

    /// <inheritdoc/>
    public async Task<SourceEventRange> ReadMeasuredEventsBoundedRangeAsync(
        long fromSequence, long toSequence, int maxCount, int maxBytes)
    {
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SourceRanges);
        ArgumentOutOfRangeException.ThrowIfNegative(fromSequence);
        ArgumentOutOfRangeException.ThrowIfLessThan(toSequence, fromSequence);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCount);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxCount, 256);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxBytes, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxBytes, 2_097_152);
        if (toSequence == fromSequence)
        {
            return EmptySourceRange();
        }

        await EnsureStateCacheBarrierAsync(correlationId: null, activity: null).ConfigureAwait(false);
        // The SDK tracker otherwise keeps every loaded envelope across pages and
        // later passes. This read-only turn owns no pending writes after its
        // existing barrier; unload the read tracker on both success and failure.
        await ClearSourceReadCacheAsync().ConfigureAwait(false);
        try
        {
            return await ReadMeasuredSourceRangeAsync(fromSequence, toSequence, maxCount, maxBytes).ConfigureAwait(false);
        }
        finally
        {
            await ClearSourceReadCacheAsync().ConfigureAwait(false);
        }
    }

    private async Task<SourceEventRange> ReadMeasuredSourceRangeAsync(long fromSequence, long toSequence, int maxCount, int maxBytes)
    {
        AggregateIdentity identity = GetAggregateIdentityFromActorId();
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SourceMetadataReads);
        ConditionalValue<AggregateMetadata> stored = await StateManager
            .TryGetStateAsync<AggregateMetadata>(identity.MetadataKey).ConfigureAwait(false);
        if (stored.HasValue) MeasureSourceMetadata(stored.Value);
        if (!stored.HasValue)
        {
            return EmptySourceRange();
        }

        AggregateMetadata metadata = stored.Value;
        if (metadata.SourceErasurePending)
            return EmptySourceRange("history_unavailable");
        if (metadata.CurrentSequence < 0 || metadata.RetainedFloor < 1
            || metadata.RetainedFloor - 1 > metadata.CurrentSequence)
        {
            return EmptySourceRange("source_conflict");
        }

        if (fromSequence < metadata.RetainedFloor - 1)
        {
            return EmptySourceRange("history_unavailable");
        }

        long upper = Math.Min(toSequence, metadata.CurrentSequence);
        var events = new List<EventEnvelope>(maxCount);
        long bytes = 2;
        for (long sequence = fromSequence; sequence < upper && events.Count < maxCount;)
        {
            sequence = checked(sequence + 1);
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SourceEventReads);
            ConditionalValue<EventEnvelope> result = await StateManager
                .TryGetStateAsync<EventEnvelope>(identity.EventStreamKeyPrefix + sequence)
                .ConfigureAwait(false);
            if (!result.HasValue)
            {
                return EmptySourceRange("delivery_gap");
            }

            EventEnvelope envelope = result.Value;
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SourceDeserializations);
            long eventBytes = SourceEnvelopeResponseSize.Count(envelope, ActorStateJsonSerializerOptions);
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SourceDeserializationBytes, eventBytes);
            if (envelope.SequenceNumber != sequence
                || !string.Equals(envelope.TenantId, identity.TenantId, StringComparison.Ordinal)
                || !string.Equals(envelope.Domain, identity.Domain, StringComparison.Ordinal)
                || !string.Equals(envelope.AggregateId, identity.AggregateId, StringComparison.Ordinal))
            {
                return EmptySourceRange("source_conflict");
            }

            if (eventBytes > maxBytes - 2)
            {
                return EmptySourceRange("delivery_page_limit_exceeded");
            }

            long nextBytes = checked(bytes + eventBytes + (events.Count == 0 ? 0 : 1));
            if (nextBytes > maxBytes)
            {
                // This is the sole unreturned lookahead. It is never decrypted or route-decoded.
                ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SourceLookaheadEvents);
                ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SourceLookaheadBytes, eventBytes);
                break;
            }

            events.Add(envelope);
            bytes = nextBytes;
        }

        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SourceReturnedEvents, events.Count);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SourceResponses);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SourceResponseBytes, bytes);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SourceResponseSerializationBytes, bytes);
        return new([.. events], bytes);
    }

    private static SourceEventRange EmptySourceRange(string? reason = null)
    {
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SourceResponses);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SourceResponseBytes, 2);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SourceResponseSerializationBytes, 2);
        return new([], 2) { ReasonCode = reason };
    }

    private async Task ClearSourceReadCacheAsync()
    {
        try { await StateManager.ClearCacheAsync().ConfigureAwait(false); }
        catch
        {
            _stateCacheUnsafe = true;
            throw;
        }
    }

    private long MeasureSourceMetadata(AggregateMetadata metadata)
    {
        long bytes = SourceActorStateBodySize.Count(metadata, ActorStateJsonSerializerOptions);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MetadataSizingBytes, bytes);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SourceMetadataBytes, bytes);
        return bytes;
    }
}
