using Hexalith.EventStore.ServiceDefaults.Routing;

using System.Runtime.CompilerServices;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Events;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Shares bounded stored-source admission, application and verification without retaining a historical prefix.</summary>
internal sealed partial class ProjectionSourceGuardHistoryReader(IActorProxyFactory actors,
    IEventPayloadProtectionService protection, IOptions<EventStoreActorOptions> actorOptions)
{
    /// <summary>Admits every readable tail event before any lifecycle reservation or handler effects.</summary>
    internal async Task<ProjectionSourceAdmittedHistory> AdmitAsync(AggregateIdentity identity, string projection,
        SourceProof baseSource, SourceProof admittedHead, string baseRouteFingerprint, int pageSize,
        CancellationToken cancellationToken, ProjectionSourceAdmittedHistory? expectedAdmission = null)
    {
        string initial = ProjectionDeliveryFingerprint.ComputeInitial(identity, projection,
            baseRouteFingerprint.StartsWith("v2:", StringComparison.Ordinal));
        string prefix = baseRouteFingerprint;
        ProjectionSourceAdmittedDigests? admitted = expectedAdmission is null
            ? new(baseSource.HeadSequence, admittedHead.HeadSequence, initial.StartsWith("v2:", StringComparison.Ordinal), baseRouteFingerprint) : null;
        if (expectedAdmission is not null && baseRouteFingerprint != (expectedAdmission.History.BaseSequence == 0
            ? expectedAdmission.History.InitialFingerprint : expectedAdmission.History.BaseFingerprint))
            throw new ProjectionDeliveryHistoryValidationException("source_conflict");
        HashSet<string>? identities = new(StringComparer.Ordinal);
        long sourceBytes = 0, wireBytes = 0;
        int pages = 0;
        try
        {
            await foreach (ProjectionSourceGuardPage page in ReadPagesAsync(identity, baseSource, admittedHead,
                pageSize, cancellationToken).ConfigureAwait(false))
            {
                if (page.StartsRange) pages++;
                sourceBytes += page.SourceBytes;
                wireBytes += page.WireBytes;
                for (int index = 0; index < page.Readable.Length; index++)
                {
                    ProjectionEventDto item = page.Readable[index];
                    if (string.IsNullOrWhiteSpace(item.MessageId) || !identities.Add(item.MessageId))
                        throw new ProjectionDeliveryHistoryValidationException("delivery_identity_conflict");
                    string eventFingerprint = ProjectionDeliveryFingerprint.ComputeEvent(item, initial.StartsWith("v2:", StringComparison.Ordinal));
                    prefix = ProjectionDeliveryFingerprint.Extend(prefix, eventFingerprint);
                    if (admitted is not null) admitted.Add(item.SequenceNumber, item.MessageId, page.SourceEventDigests[index], eventFingerprint, prefix);
                    else if (expectedAdmission!.Events is not ProjectionSourceAdmittedDigestList<ProjectionSourceAdmittedEvent> expected
                        || !expected.Table.MatchesEvent(item.SequenceNumber, item.MessageId, page.SourceEventDigests[index], eventFingerprint))
                        throw new ProjectionDeliveryHistoryValidationException("source_conflict");
                }
            }
        }
        finally
        {
            // Release phase-only identities before an inline async continuation
            // can begin the next route or application pass.
            identities = null;
        }

        if (admitted is null)
        {
            if (prefix != expectedAdmission!.History.PrefixFingerprint
                || expectedAdmission.Events.Count != admittedHead.HeadSequence - baseSource.HeadSequence)
                throw new ProjectionDeliveryHistoryValidationException("source_conflict");
            return expectedAdmission;
        }
        admitted.Seal();
        return new(new(initial, prefix, admitted.RouteEvents, baseSource.HeadSequence,
            baseSource.HeadSequence == 0 ? null : baseRouteFingerprint), admitted.SourceEvents, sourceBytes, wireBytes, pages);
    }

    /// <summary>Reads exactly the frozen suffix, with no prior-envelope or terminal empty probe.</summary>
    internal async IAsyncEnumerable<ProjectionSourceGuardPage> ReadPagesAsync(AggregateIdentity identity,
        SourceProof baseSource, SourceProof admittedHead, int pageSize,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 256);
        if (admittedHead.HeadSequence > 10_000) throw new ProjectionDeliveryHistoryValidationException("delivery_owner_limit_exceeded");
        if (!SourceProofCodec.IsValid(baseSource, identity, baseSource.HeadSequence, baseSource.RetainedFloor)
            || !SourceProofCodec.IsValid(admittedHead, identity, admittedHead.HeadSequence, admittedHead.RetainedFloor)
            || baseSource.Erased || admittedHead.Erased || baseSource.SourceIncarnation != admittedHead.SourceIncarnation
            || baseSource.HeadSequence >= admittedHead.HeadSequence)
            throw new ProjectionDeliveryHistoryValidationException("source_conflict");
        if (admittedHead.RetainedFloor - 1 > baseSource.HeadSequence)
            throw new ProjectionDeliveryHistoryValidationException("history_unavailable");
        IAggregateActor actor = actors.CreateActorProxy<IAggregateActor>(new ActorId(identity.ActorId), actorOptions.Value.AggregateActorTypeName);
        SourceProof chain = baseSource;
        long after = baseSource.HeadSequence;
        long totalSourceBytes = 0;
        using var wireSize = new SizeLimitedWriteStream(67_108_864);
        while (after < admittedHead.HeadSequence)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SourceEventRange response = await RouteCatalogRemoteCall.InvokeAsync(() => actor.ReadMeasuredEventsBoundedRangeAsync(after, admittedHead.HeadSequence,
                pageSize, 2_097_152).WaitAsync(cancellationToken)).ConfigureAwait(false);
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SourceResponseDeserializationBytes, response.EnvelopeArrayBytes);
            if (response.ReasonCode is not null)
                throw new ProjectionDeliveryHistoryValidationException(response.ReasonCode switch
                {
                    "delivery_page_limit_exceeded" or "history_unavailable" or "source_conflict" or "delivery_gap" => response.ReasonCode,
                    _ => "delivery_state_unavailable",
                });
            EventEnvelope[] stored = response.Events;
            long pageBytes = response.EnvelopeArrayBytes;
            if (pageBytes < 2L + Math.Max(0, stored.Length - 1) || pageBytes > 2_097_152
                || (totalSourceBytes += pageBytes) > 67_108_864)
                throw new ProjectionDeliveryHistoryValidationException("delivery_byte_limit_exceeded");
            long next = ProjectionStreamPageValidation.ValidateGuarded(identity, after, admittedHead.HeadSequence, pageSize, stored);
            for (int index = 0; index < stored.Length; index++)
            {
                string hash = SourceProofCodec.ComputeEventHash(stored[index]);
                chain = SourceProofCodec.ExtendVerified(chain, stored[index], hash);
                // A compressed protected actor page can decode to almost the complete 64 MiB
                // prefix. Keep only one readable event beside any retained canonical prefix.
                ProjectionEventReadabilityResult decoded = await ProjectionEventWireBuilder.BuildAsync(protection,
                    identity, [stored[index]], cancellationToken).ConfigureAwait(false);
                if (decoded.Events is not { Length: 1 } readable)
                {
                    if (decoded.UnreadableReason == UnreadableProtectedDataReason.ProviderUnavailable)
                        throw new InvalidOperationException("history_unavailable");
                    throw new ProjectionDeliveryHistoryValidationException("history_unavailable");
                }
                long previousWireBytes = wireSize.Length;
                long previousAttempts = wireSize.AttemptedBytes;
                try
                {
                    ProjectionEventDto item = readable[0];
                    if (item.SequenceNumber == baseSource.HeadSequence + 1) wireSize.WriteByte((byte)'[');
                    if (item.SequenceNumber > baseSource.HeadSequence + 1) wireSize.WriteByte((byte)',');
                    await ProjectionWireJsonSerializer.SerializeAsync(wireSize, item, JsonSerializerOptions.Web, cancellationToken).ConfigureAwait(false);
                    if (item.SequenceNumber == admittedHead.HeadSequence) wireSize.WriteByte((byte)']');
                }
                catch (ProjectionRebuildPrefixSafetyLimitExceededException)
                { throw new ProjectionDeliveryHistoryValidationException("delivery_byte_limit_exceeded"); }
                finally { ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.WireSizingBytes, wireSize.AttemptedBytes - previousAttempts); }
                long admittedWireBytes = wireSize.Length - previousWireBytes;
                ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.WireEvents);
                ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.WireBytes, admittedWireBytes);
                if (stored[index].SequenceNumber == admittedHead.HeadSequence
                    && (chain.CumulativeDigest != admittedHead.CumulativeDigest || chain.HeadMessageId != admittedHead.HeadMessageId))
                    throw new ProjectionDeliveryHistoryValidationException("source_conflict");
                yield return new([stored[index]], readable, [hash], index == 0 ? pageBytes : 0, admittedWireBytes)
                    { StartsRange = index == 0 };
            }
            after = next;
        }
    }
}
