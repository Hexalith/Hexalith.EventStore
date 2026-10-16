using System.Text.Json;

using Dapr.Actors.Runtime;

using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.Server.Actors;

public partial class AggregateActor
{
    /// <inheritdoc/>
    public async Task<SourceProofInspection> BootstrapSourceProofAsync(SourceProofBootstrapRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        AggregateIdentity identity = GetAggregateIdentityFromActorId();
        SourceProofBootstrapAuthority authority = serviceProvider?.GetService<SourceProofBootstrapAuthority>()
            ?? throw new InvalidOperationException("guard_activation_required");
        await authority.ValidateAsync(request, identity).ConfigureAwait(false);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SourceBootstrapAttempts);
        await EnsureStateCacheBarrierAsync(correlationId: null, activity: null).ConfigureAwait(false);
        await StateManager.ClearCacheAsync().ConfigureAwait(false);
        ConditionalValue<AggregateMetadata> stored = await ReadBootstrapMetadataAsync(identity).ConfigureAwait(false);
        if (stored.HasValue && stored.Value.SourceErasurePending)
            return new(SourceProofInspectionStatus.Invalid, true, stored.Value.CurrentSequence, true, null);
        // Bootstrap is an exclusive cold operation and must also fence legacy interrupted
        // erasure progress that predates SourceErasurePending on nonempty metadata.
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SourceMetadataReads);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.BootstrapStateReads);
        ConditionalValue<TrustedEffectErasureProgress> erased = await StateManager
            .TryGetStateAsync<TrustedEffectErasureProgress>(TrustedEffectErasureProgress.StateName).ConfigureAwait(false);
        if (erased.HasValue)
        {
            using var progressBytes = new SizeLimitedWriteStream(long.MaxValue);
            JsonSerializer.Serialize(progressBytes, erased.Value, ActorStateJsonSerializerOptions);
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MetadataSizingBytes, progressBytes.Length);
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SourceMetadataBytes, progressBytes.Length);
            return new(erased.Value.Completed ? SourceProofInspectionStatus.Unsupported : SourceProofInspectionStatus.Invalid,
                stored.HasValue, stored.HasValue ? stored.Value.CurrentSequence : 0, true, null);
        }
        if (!stored.HasValue || stored.Value.CurrentSequence == 0)
        {
            if (request.ExpectedHead != 0) throw new InvalidOperationException("head_changed");
        }

        AggregateMetadata metadata = stored.HasValue ? stored.Value : new AggregateMetadata(0, DateTimeOffset.UtcNow, null);
        if (metadata.CurrentSequence != request.ExpectedHead || metadata.CurrentSequence is < 0 or > 10_000)
            throw new InvalidOperationException("head_changed");
        if (metadata.RetainedFloor != 1) throw new InvalidOperationException("history_unavailable");
        if (metadata.SourceProof is not null)
        {
            if (!SourceProofCodec.IsValid(metadata.SourceProof, identity, metadata.CurrentSequence, metadata.RetainedFloor)
                || metadata.SourceProof.Erased)
                throw new InvalidOperationException("source_conflict");
            return new(SourceProofInspectionStatus.Available, true, metadata.CurrentSequence, false, metadata.SourceProof);
        }

        SourceProof proof = SourceProofCodec.CreateSeed(identity, UniqueIdHelper.GenerateSortableUniqueStringId());
        long sourceBytes = 0;
        long wireBytes = 0;
        for (long sequence = 1; sequence <= metadata.CurrentSequence; sequence++)
        {
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SourceEventReads);
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.BootstrapStateReads);
            ConditionalValue<EventEnvelope> value = await StateManager
                .TryGetStateAsync<EventEnvelope>(identity.EventStreamKeyPrefix + sequence).ConfigureAwait(false);
            if (!value.HasValue)
                throw new MissingEventException(sequence, identity.TenantId, identity.Domain, identity.AggregateId);
            EventEnvelope envelope = value.Value;
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SourceDeserializations);
            long eventBytes = SourceEnvelopeResponseSize.Count(envelope, ActorStateJsonSerializerOptions);
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SourceDeserializationBytes, eventBytes);
            if (envelope.SequenceNumber != sequence) throw new InvalidOperationException("source_conflict");
            if (eventBytes + 2L > 2_097_152) throw new InvalidOperationException("delivery_page_limit_exceeded");
            sourceBytes = checked(sourceBytes + eventBytes + 2L);
            if (sourceBytes > 67_108_864) throw new InvalidOperationException("delivery_byte_limit_exceeded");
            proof = SourceProofCodec.Extend(proof, envelope);
            ProjectionEventReadabilityResult readable = await ProjectionEventWireBuilder.BuildAsync(
                payloadProtectionService, identity, [envelope], CancellationToken.None).ConfigureAwait(false);
            if (readable.Events is not { Length: 1 }) throw new InvalidOperationException("history_unavailable");
            using var wireSize = new SizeLimitedWriteStream(long.MaxValue);
            try { JsonSerializer.Serialize(wireSize, readable.Events, JsonSerializerOptions.Web); }
            finally { ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.WireSizingBytes, wireSize.AttemptedBytes); }
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.WireEvents, readable.Events.Length);
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.WireBytes, wireSize.Length);
            wireBytes = checked(wireBytes + wireSize.Length);
            if (wireBytes > 67_108_864) throw new InvalidOperationException("delivery_byte_limit_exceeded");
        }

        await StateManager.ClearCacheAsync().ConfigureAwait(false);
        ConditionalValue<AggregateMetadata> observed = await ReadBootstrapMetadataAsync(identity).ConfigureAwait(false);
        if (observed.HasValue != stored.HasValue || observed.HasValue && observed.Value != metadata)
            throw new InvalidOperationException("head_changed");
        AggregateMetadata anchored = metadata with { SourceProof = proof };
        using var anchorBytes = new SizeLimitedWriteStream(long.MaxValue);
        JsonSerializer.Serialize(anchorBytes, anchored, ActorStateJsonSerializerOptions);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MetadataSerializationBytes, anchorBytes.Length);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SourceAnchorWriteBytes, anchorBytes.Length);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SourceAnchorWriteAttempts);
        await StateManager.SetStateAsync(identity.MetadataKey, anchored).ConfigureAwait(false);
        try
        {
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.BootstrapSaveAttempts);
            await StateManager.SaveStateAsync().ConfigureAwait(false);
        }
        catch
        {
            _stateCacheUnsafe = true;
            await StateManager.ClearCacheAsync().ConfigureAwait(false);
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SourceAnchorReadbacks);
            ConditionalValue<AggregateMetadata> durable = await ReadBootstrapMetadataAsync(identity).ConfigureAwait(false);
            if (!durable.HasValue || durable.Value != anchored) throw;
            _stateCacheUnsafe = false;
            return new(SourceProofInspectionStatus.Available, true, metadata.CurrentSequence, false, proof);
        }

        await StateManager.ClearCacheAsync().ConfigureAwait(false);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SourceAnchorReadbacks);
        ConditionalValue<AggregateMetadata> saved = await ReadBootstrapMetadataAsync(identity).ConfigureAwait(false);
        if (!saved.HasValue || saved.Value != anchored)
        {
            _stateCacheUnsafe = true;
            throw new InvalidOperationException("source_conflict");
        }

        return new(SourceProofInspectionStatus.Available, true, metadata.CurrentSequence, false, proof);
    }

    private async Task<ConditionalValue<AggregateMetadata>> ReadBootstrapMetadataAsync(AggregateIdentity identity)
    {
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SourceMetadataReads);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.BootstrapStateReads);
        ConditionalValue<AggregateMetadata> result = await StateManager.TryGetStateAsync<AggregateMetadata>(identity.MetadataKey).ConfigureAwait(false);
        if (result.HasValue) MeasureSourceMetadata(result.Value);
        return result;
    }
}
