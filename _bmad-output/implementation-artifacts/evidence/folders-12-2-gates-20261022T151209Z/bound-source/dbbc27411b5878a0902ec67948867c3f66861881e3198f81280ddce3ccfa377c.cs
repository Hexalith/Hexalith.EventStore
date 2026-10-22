using System.Text.Json;

using Dapr.Actors;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Events;

namespace Hexalith.EventStore.Server.Projections;

internal sealed partial class ProjectionDeliveryRetryWorker
{
    private async Task<ProjectionEventDto[]> LoadRecoveryPrefixAsync(AggregateIdentity identity,
        ProjectionDeliveryRetryWorkItem work, CancellationToken cancellationToken)
    {
        if (work.HeadSequence is <= 0 or > 10_000)
            throw new ProjectionDeliveryHistoryValidationException("delivery_owner_limit_exceeded");
        IAggregateActor source = actorProxyFactory.CreateActorProxy<IAggregateActor>(new ActorId(identity.ActorId), actorOptions.Value.AggregateActorTypeName);
        var readable = new List<ProjectionEventDto>();
        var identities = new HashSet<string>(StringComparer.Ordinal);
        using var size = new SizeLimitedWriteStream(67_108_864);
        long cursor = 0, sourceBytes = 0;
        while (cursor < work.HeadSequence)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SourceEventRange range = await source.ReadMeasuredEventsBoundedRangeAsync(cursor, work.HeadSequence,
                ReadPageSize, 2_097_152).WaitAsync(cancellationToken).ConfigureAwait(false);
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SourceResponseDeserializationBytes, range.EnvelopeArrayBytes);
            if (range.ReasonCode is not null)
                throw new ProjectionDeliveryHistoryValidationException(range.ReasonCode is "delivery_page_limit_exceeded"
                    or "history_unavailable" or "source_conflict" or "delivery_gap" ? range.ReasonCode : "delivery_state_unavailable");
            long next = ProjectionStreamPageValidation.ValidateGuarded(identity, cursor, work.HeadSequence, ReadPageSize, range.Events);
            if (range.EnvelopeArrayBytes < 2L + Math.Max(0, range.Events.Length - 1) || range.EnvelopeArrayBytes > 2_097_152
                || (sourceBytes += range.EnvelopeArrayBytes) > 67_108_864)
                throw new ProjectionDeliveryHistoryValidationException("delivery_byte_limit_exceeded");
            foreach (EventEnvelope stored in range.Events)
            {
                if (string.IsNullOrWhiteSpace(stored.MessageId) || !identities.Add(stored.MessageId))
                    throw new ProjectionDeliveryHistoryValidationException("delivery_identity_conflict");
                ProjectionEventReadabilityResult decoded = await ProjectionEventWireBuilder.BuildAsync(payloadProtectionService,
                    identity, [stored], cancellationToken).ConfigureAwait(false);
                if (decoded.Events is not { Length: 1 } events)
                {
                    if (decoded.UnreadableReason == UnreadableProtectedDataReason.ProviderUnavailable)
                        throw new InvalidOperationException("history_unavailable");
                    throw new ProjectionDeliveryHistoryValidationException("history_unavailable");
                }
                ProjectionEventDto item = events[0];
                long before = size.Length, attempts = size.AttemptedBytes;
                try
                {
                    if (item.SequenceNumber == 1) size.WriteByte((byte)'[');
                    else size.WriteByte((byte)',');
                    await ProjectionWireJsonSerializer.SerializeAsync(size, item, JsonSerializerOptions.Web, cancellationToken).ConfigureAwait(false);
                    if (item.SequenceNumber == work.HeadSequence) size.WriteByte((byte)']');
                }
                catch (ProjectionRebuildPrefixSafetyLimitExceededException)
                { throw new ProjectionDeliveryHistoryValidationException("delivery_byte_limit_exceeded"); }
                finally { ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.WireSizingBytes, size.AttemptedBytes - attempts); }
                ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.WireBytes, size.Length - before);
                ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.WireEvents);
                readable.Add(item);
            }
            cursor = next;
        }
        if (readable[^1].MessageId != work.HeadMessageId)
            throw new ProjectionDeliveryHistoryValidationException("delivery_identity_conflict");
        return [.. readable];
    }
}
