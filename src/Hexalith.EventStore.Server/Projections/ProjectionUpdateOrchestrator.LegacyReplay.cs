using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

using Dapr.Actors;

using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Queries;

namespace Hexalith.EventStore.Server.Projections;

internal partial class ProjectionUpdateOrchestrator
{
    /// <summary>Runs durable compatible replay independently of process-local scheduling.</summary>
    internal async Task<bool> DeliverCompatibleReplayAsync(AggregateIdentity identity, DomainServiceRegistration registration,
        ProjectionActivationWorkItem? sampledWork, IProjectionReplayPublicationGateway publication,
        IProjectionRebuildInspectionGateway inspection, IProjectionLegacyDeliveryOutbox outbox, CancellationToken cancellationToken)
    {
        long observedHead = 0, observedCheckpoint = 0;
        string status = "retryable", reason = "delivery_state_unavailable";
        try
        {
            TimeProvider clock = timeProvider ?? TimeProvider.System;
            IAggregateActor source = actorProxyFactory.CreateActorProxy<IAggregateActor>(new ActorId(identity.ActorId), AggregateActorTypeName);
            SourceProofInspection sourceInspection;
            try
            {
                sourceInspection = await source.InspectSourceProofAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception failure) when (IsUnavailableActorMethod(failure))
            {
                reason = ProjectionDispatchReasonCodes.UnsupportedCapability;
                // The released path is eligible only before any durable replay intent
                // or writer-v2 cutover. Its exact discovered scope is fenced again
                // before and under the existing lifecycle lease.
                return sampledWork?.LegacyDelivery is not null
                    || await deliveryStateStore!.ReadWriterProtocolAsync(cancellationToken).ConfigureAwait(false) is not null;
            }
            if (sourceInspection.Status == SourceProofInspectionStatus.Invalid)
                throw new ProjectionDeliveryHistoryValidationException("source_conflict");
            AggregateStreamMetadata metadata = new(sourceInspection.Exists, sourceInspection.CurrentSequence, sourceInspection.Erased);
            if (metadata.Erased) return true;
            ProjectionLegacyDeliveryIntent? original = sampledWork?.LegacyDelivery;
            long head = original?.HeadSequence ?? metadata.CurrentSequence;
            observedHead = head;
            if (head == 0)
            {
                // No route was discovered and no model completion may be invented.
                reason = ProjectionDispatchReasonCodes.UnsupportedCapability;
                return true;
            }
            if (!metadata.Exists || metadata.CurrentSequence < head)
                throw new ProjectionDeliveryHistoryValidationException("checkpoint_drift");
            SourceProof? anchor = original?.SourceProof ?? (original is null ? sourceInspection.Proof : null);
            if (original?.SourceProof is { } frozen && (sourceInspection.Proof is not { } current
                || current.SourceIncarnation != frozen.SourceIncarnation || current.RetainedFloor > 1))
                throw new ProjectionDeliveryHistoryValidationException("source_conflict");
            if (sampledWork is null)
            {
                await projectionActivationOutbox!.EnsureAsync(identity, cancellationToken).ConfigureAwait(false);
                sampledWork = await projectionActivationOutbox.GetAsync(identity, cancellationToken).ConfigureAwait(false);
                if (sampledWork is null || sampledWork.LegacyDelivery is not null) return true;
            }
            string invocation = UniqueIdHelper.GenerateSortableUniqueStringId();
            if (original?.InvocationToken is not null && original.LeaseExpiresAtUtc > clock.GetUtcNow())
            {
                // Named delivery owns independent durable reservations. A failed or still-running
                // legacy attempt must not freeze later named heads behind its compatibility lease.
                // Preserve that exact legacy intent and validate the current authoritative prefix
                // before allowing the named coordinator to admit its own routes.
                if (namedProjectionDispatchCoordinator is not null)
                {
                    (ProjectionEventDto[] namedEvents, string _, string namedHeadId) = await ReadLegacyReplayPrefixAsync(
                        source, identity, metadata.CurrentSequence, sourceInspection.Proof, true, cancellationToken).ConfigureAwait(false);
                    _ = await namedProjectionDispatchCoordinator.TryDispatchHeadAsync(identity, registration,
                        metadata.CurrentSequence, namedHeadId, namedEvents, cancellationToken).ConfigureAwait(false);
                }
                return true;
            }
            (ProjectionEventDto[] events, string fingerprint, string headId) = await ReadLegacyReplayPrefixAsync(
                source, identity, head, anchor, true, cancellationToken).ConfigureAwait(false);
            if (original is not null && (original.HeadMessageId != headId || original.SourceFingerprint != fingerprint))
                throw new ProjectionDeliveryHistoryValidationException("source_conflict");
            ProjectionLegacyDeliveryIntent intent = (original ?? new ProjectionLegacyDeliveryIntent(
                UniqueIdHelper.GenerateSortableUniqueStringId(), registration.AppId, registration.Version ?? "v1", head, headId)
                { SourceFingerprint = fingerprint, FrozenActivationRevision = sampledWork.Revision, SourceProof = anchor })
                with { InvocationToken = invocation, LeaseExpiresAtUtc = clock.GetUtcNow() + TimeSpan.FromMinutes(5) };
            ProjectionActivationWorkItem? work = await outbox.TryBindLegacyDeliveryAsync(sampledWork, intent, cancellationToken).ConfigureAwait(false);
            if (work is null) return true;

            // Named and explicit legacy routes may coexist. Their durable work remains independent.
            if (namedProjectionDispatchCoordinator is not null)
                _ = await namedProjectionDispatchCoordinator.TryDispatchHeadAsync(identity, registration, head, headId, events,
                    cancellationToken).ConfigureAwait(false);

            ProjectionState? candidate = null;
            string? actorId = intent.ProjectionType is null ? null
                : QueryActorIdHelper.DeriveActorId(intent.ProjectionType, identity.TenantId, identity.AggregateId, []);
            if (actorId is not null)
            {
                ProjectionRebuildInspection persisted = await inspection.InspectAsync(actorId, intent.OperationId, cancellationToken).ConfigureAwait(false);
                if (persisted.Status == ProjectionRebuildInspectionStatus.Conflict)
                    throw new ProjectionDeliveryHistoryValidationException("delivery_reconciliation_required");
                candidate = persisted.State;
            }
            if (candidate is null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using HttpRequestMessage request = daprClient.CreateInvokeMethodRequest(intent.AppId, "project",
                    new ProjectionRequest(identity.TenantId, identity.Domain, identity.AggregateId, events));
                request.Content?.Dispose();
                request.Content = ProjectionWireJsonSerializer.CreateContent(
                    new ProjectionRequest(identity.TenantId, identity.Domain, identity.AggregateId, events), daprClient.JsonSerializerOptions);
                using HttpResponseMessage? response = await SendProjectRequestAsync(httpClientFactory.CreateClient(), request,
                    intent.AppId, identity, cancellationToken).ConfigureAwait(false);
                if (response is null) return true;
                if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.NotImplemented)
                {
                    if (namedProjectionDispatchCoordinator is not null)
                        await CompleteLegacyIntentAsync(identity, outbox, work, cancellationToken).ConfigureAwait(false);
                    return true;
                }
                if (!response.IsSuccessStatusCode) return true;
                ProjectionResponse? prepared = await ReadProjectResponseAsync(response, intent.AppId, identity, cancellationToken).ConfigureAwait(false);
                if (prepared is null || string.IsNullOrWhiteSpace(prepared.ProjectionType)
                    || prepared.State.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
                    || prepared.State.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(prepared.State.GetString())) return true;
                candidate = ProjectionState.FromProjectionResponse(prepared, identity.TenantId);
                actorId = QueryActorIdHelper.DeriveActorId(prepared.ProjectionType, identity.TenantId, identity.AggregateId, []);
            }
            string candidateDigest = Convert.ToHexStringLower(SHA256.HashData(candidate.StateBytes));
            if (intent.CandidateFingerprint is not null && intent.CandidateFingerprint != candidateDigest
                || intent.ProjectionType is not null && intent.ProjectionType != candidate.ProjectionType)
                throw new ProjectionDeliveryHistoryValidationException("source_conflict");
            intent = intent with { ProjectionType = candidate.ProjectionType, CandidateFingerprint = candidateDigest };
            work = await BindLegacyProgressAsync(identity, work, intent, outbox, cancellationToken).ConfigureAwait(false);
            if (work is null) return true;

            string route = candidate.ProjectionType;
            ProjectionDeliveryStateReadResult scope = await deliveryStateStore!.ReadAsync(identity, route, cancellationToken).ConfigureAwait(false);
            observedCheckpoint = scope.State?.LastDeliveredSequence ?? 0;
            // Catalog removal cannot turn an admitted guarded scope into an unguarded legacy writer.
            if (scope.SourceGuard is not null || scope.State?.SchemaVersion == ProjectionDeliveryState.GuardSchemaVersion
                || scope.State?.GuardEvidence is not null || scope.State?.ActiveReservation?.GuardBinding is not null
                || scope.Classification is ProjectionDeliveryStateClassification.Unsupported or ProjectionDeliveryStateClassification.SchemaRegression)
                throw new ProjectionDeliveryHistoryValidationException("guard_activation_required");
            ProjectionDeliveryFingerprintHistory history = ProjectionDeliveryFingerprint.ComputeHistory(identity, route, events);
            events = [];
            string lease = UniqueIdHelper.GenerateSortableUniqueStringId();
            if (!await lifecycleGateway!.BeginDeliveryWriteAsync(identity, route, lease, cancellationToken).ConfigureAwait(false)) return true;
            bool completed = false;
            CancellationTokenSource? finalization = null;
            try
            {
                ProjectionActivationWorkItem? ownership = await projectionActivationOutbox!.GetAsync(identity, cancellationToken).ConfigureAwait(false);
                if (ownership?.LegacyDelivery != intent || intent.LeaseExpiresAtUtc <= clock.GetUtcNow()) return true;
                scope = await deliveryStateStore.ReadAsync(identity, route, cancellationToken).ConfigureAwait(false);
                observedCheckpoint = scope.State?.LastDeliveredSequence ?? 0;
                if (!CanPublishLegacyScope(scope)) return true;
                ProjectionLifecycleSnapshot lifecycle = await lifecycleGateway.ReadSnapshotAsync(identity, route, cancellationToken).ConfigureAwait(false);
                var binding = new ProjectionReplayPublicationBinding(identity.TenantId, identity.Domain, identity.AggregateId,
                    route, intent.OperationId, head, headId, fingerprint, candidateDigest);
                var bound = new ProjectionReplayPublicationRequest(binding, lifecycle, lease, candidate);
                ProjectionDeliveryReservation? reservation = null;
                if (scope.WriterProtocolV2Active)
                {
                    ProjectionDeliveryAdmissionResult admission = await deliveryIdempotency!.TryAdmitHistoryAsync(identity, route,
                        history, true, cancellationToken, intent.DeliveryFencingToken).ConfigureAwait(false);
                    if (admission.Disposition == ProjectionDeliveryAdmissionDisposition.AlreadyCompleted)
                    {
                        finalization = new CancellationTokenSource(TimeSpan.FromSeconds(30), clock);
                        CancellationToken readbackToken = finalization.Token;
                        ProjectionRebuildInspection selected = await inspection.InspectAsync(actorId!, intent.OperationId, readbackToken).ConfigureAwait(false);
                        completed = selected.Status == ProjectionRebuildInspectionStatus.Promoted && selected.State is not null
                            && selected.State.StateBytes.AsSpan().SequenceEqual(candidate.StateBytes)
                            ? await publication.FinalizeReplayAsync(actorId!, bound with { State = null }, readbackToken).ConfigureAwait(false)
                            : await publication.ReadReplayCompletionAsync(actorId!, bound with { State = null }, readbackToken).ConfigureAwait(false);
                        return true;
                    }
                    reservation = admission.Reservation;
                    if (reservation is null) return true;
                    intent = intent with { DeliveryFencingToken = reservation.FencingToken };
                    work = await BindLegacyProgressAsync(identity, work, intent, outbox, cancellationToken).ConfigureAwait(false);
                    if (work is null) return true;
                }
                else
                {
                    if (scope.Classification == ProjectionDeliveryStateClassification.Current || deliveryCheckpointStore is null) return true;
                    long checkpoint = await deliveryCheckpointStore.ReadDeliveredSequenceAsync(identity, route, cancellationToken).ConfigureAwait(false);
                    if (checkpoint > head) throw new ProjectionDeliveryHistoryValidationException("checkpoint_drift");
                }
                ProjectionRebuildInspection staged = await publication.StageReplayAsync(actorId!, bound, cancellationToken).ConfigureAwait(false);
                if (staged.Status is not ProjectionRebuildInspectionStatus.Staged and not ProjectionRebuildInspectionStatus.Promoted) return true;
                cancellationToken.ThrowIfCancellationRequested();
                (ProjectionEventDto[] _, string verified, string verifiedHeadId) = await ReadLegacyReplayPrefixAsync(
                    source, identity, head, anchor, false, cancellationToken).ConfigureAwait(false);
                if (verified != fingerprint || verifiedHeadId != headId) throw new ProjectionDeliveryHistoryValidationException("source_conflict");
                // No later handler starts after this point. Recovery uses this exact candidate and manifest.
                finalization = new CancellationTokenSource(TimeSpan.FromSeconds(30), clock);
                CancellationToken finalToken = finalization.Token;
                using var publicationCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, finalToken);
                ProjectionRebuildInspection promoted;
                try
                {
                    promoted = await publication.PromoteReplayAsync(actorId!, bound with { State = null },
                        publicationCancellation.Token).WaitAsync(publicationCancellation.Token).ConfigureAwait(false);
                }
                catch when (!finalToken.IsCancellationRequested)
                {
                    // A timeout/canceled caller is not permission to prepare a
                    // replacement. Inspect this exact original candidate only.
                    promoted = await inspection.InspectAsync(actorId!, intent.OperationId, finalToken).ConfigureAwait(false);
                    if (promoted.Status != ProjectionRebuildInspectionStatus.Promoted) return true;
                }
                ProjectionRebuildInspection readback = await inspection.InspectAsync(actorId!, intent.OperationId, finalToken).ConfigureAwait(false);
                if (promoted.Status != ProjectionRebuildInspectionStatus.Promoted
                    || readback.Status != ProjectionRebuildInspectionStatus.Promoted || readback.State is null
                    || !readback.State.StateBytes.AsSpan().SequenceEqual(candidate.StateBytes)) return true;
                SourceProofInspection finalSource = await source.InspectSourceProofAsync().WaitAsync(finalToken).ConfigureAwait(false);
                if (finalSource.Status == SourceProofInspectionStatus.Invalid || finalSource.Erased || !finalSource.Exists
                    || finalSource.CurrentSequence < head || anchor is not null && (finalSource.Proof is not { } finalAnchor
                        || finalAnchor.SourceIncarnation != anchor.SourceIncarnation || finalAnchor.RetainedFloor > 1
                        || finalAnchor.HeadSequence == head && finalAnchor.CumulativeDigest != anchor.CumulativeDigest)) return true;
                if (anchor is null)
                {
                    // Metadata without an anchor cannot witness stored-prefix
                    // identity across publication. Recheck the frozen prefix
                    // without retaining another complete decoded array.
                    (ProjectionEventDto[] _, string publishedFingerprint, string publishedHeadId) = await ReadLegacyReplayPrefixAsync(
                        source, identity, head, null, false, finalToken).ConfigureAwait(false);
                    if (publishedFingerprint != fingerprint || publishedHeadId != headId)
                        throw new ProjectionDeliveryHistoryValidationException("source_conflict");
                }
                if (reservation is not null)
                {
                    ProjectionDeliveryCompletion completion = await deliveryIdempotency!.CompleteHistoryAsync(identity, route,
                        history, reservation, finalToken).ConfigureAwait(false);
                    if (completion is not ProjectionDeliveryCompletion.Completed and not ProjectionDeliveryCompletion.AlreadyCompleted) return true;
                    ProjectionDeliveryStateReadResult saved = await deliveryStateStore.ReadAsync(identity, route, finalToken).ConfigureAwait(false);
                    if (saved.State?.LastDeliveredSequence != head || saved.State.LastCompletedMessageId != headId
                        || saved.State.CompletedPrefixFingerprint != history.PrefixFingerprint || saved.State.ActiveReservation is not null) return true;
                }
                else if (!await deliveryCheckpointStore!.SaveDeliveredSequenceAsync(identity, route, head, finalToken).ConfigureAwait(false)
                    || await deliveryCheckpointStore.ReadDeliveredSequenceAsync(identity, route, finalToken).ConfigureAwait(false) != head) return true;
                completed = await publication.FinalizeReplayAsync(actorId!, bound with { State = null }, finalToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
            }
            finally
            {
                finalization ??= new CancellationTokenSource(TimeSpan.FromSeconds(30), clock);
                try
                {
                    if (!await lifecycleGateway.CompleteDeliveryWriteAsync(identity, route, lease, finalization.Token).ConfigureAwait(false)) completed = false;
                    if (completed)
                    {
                        observedCheckpoint = head;
                        if (work is not null && !await CompleteLegacyIntentAsync(identity, outbox, work, finalization.Token).ConfigureAwait(false))
                        {
                            reason = ProjectionDispatchReasonCodes.DeliveryInProgress;
                        }
                        else
                        {
                            status = "completed"; reason = "delivery_completed";
                        }
                    }
                }
                finally { finalization.Dispose(); }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            status = "retryable"; reason = ProjectionDispatchReasonCodes.Cancellation;
            throw;
        }
        catch (ProjectionDeliveryHistoryValidationException failure)
        {
            status = "failed"; reason = failure.Message;
        }
        catch (Exception failure) when (IsUnavailableActorMethod(failure))
        {
            // Unsupported publication after intent creation retains that exact work;
            // it never authorizes the released direct writer.
            reason = ProjectionDispatchReasonCodes.UnsupportedCapability;
        }
        catch
        {
            reason = "delivery_state_unavailable";
        }
        finally
        {
            // Legacy route discovery has no verified catalog tag. No raw route or
            // provider exception is exported, including on a failed partial attempt.
            ProjectionDeliveryDiagnostics.RecordGuarded("unknown", "full_replay", status, reason, observedHead, observedCheckpoint);
        }
        return true;
    }

    private async Task<ProjectionActivationWorkItem?> BindLegacyProgressAsync(AggregateIdentity identity,
        ProjectionActivationWorkItem expected, ProjectionLegacyDeliveryIntent next, IProjectionLegacyDeliveryOutbox outbox,
        CancellationToken cancellationToken)
    {
        ProjectionActivationWorkItem? current = await projectionActivationOutbox!.GetAsync(identity, cancellationToken).ConfigureAwait(false);
        if (current is null || current.LegacyDelivery != expected.LegacyDelivery) return null;
        return await outbox.TryBindLegacyDeliveryAsync(current, next, cancellationToken).ConfigureAwait(false);
    }

    private static bool CanPublishLegacyScope(ProjectionDeliveryStateReadResult scope)
        => scope.SourceGuard is null && scope.State?.SchemaVersion != ProjectionDeliveryState.GuardSchemaVersion
            && scope.State?.GuardEvidence is null && scope.State?.ActiveReservation?.GuardBinding is null
            && scope.Classification is not ProjectionDeliveryStateClassification.Unsupported and not ProjectionDeliveryStateClassification.SchemaRegression;

    private async Task<bool> CompleteLegacyIntentAsync(AggregateIdentity identity, IProjectionLegacyDeliveryOutbox outbox,
        ProjectionActivationWorkItem work, CancellationToken cancellationToken)
    {
        if (!await outbox.TryCompleteLegacyDeliveryAsync(work, cancellationToken).ConfigureAwait(false)) return false;
        await projectionActivationOutbox!.CompleteAsync(work with { Revision = work.LegacyDelivery!.FrozenActivationRevision,
            LegacyDelivery = null }, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task<(ProjectionEventDto[] Events, string SourceFingerprint, string HeadId)> ReadLegacyReplayPrefixAsync(
        IAggregateActor source, AggregateIdentity identity, long head, SourceProof? anchor, bool retainEvents, CancellationToken cancellationToken)
    {
        if (head > Math.Min(10_000, projectionOptions.Value.RebuildMaxPrefixEventCount))
            throw new ProjectionDeliveryHistoryValidationException("delivery_owner_limit_exceeded");
        int pageSize = Math.Min(projectionOptions.Value.RebuildPageSize, 256);
        long ceiling = Math.Min(67_108_864, projectionOptions.Value.RebuildMaxPrefixBytes);
        List<ProjectionEventDto>? readable = retainEvents ? [] : null;
        using IncrementalHash sourceHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        SourceProof? chain = anchor is null ? null : SourceProofCodec.CreateSeed(identity, anchor.SourceIncarnation);
        using var wireSize = new SizeLimitedWriteStream(ceiling);
        long cursor = 0, sourceBytes = 0;
        string headId = string.Empty;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        while (cursor < head)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SourceEventRange page = await source.ReadMeasuredEventsBoundedRangeAsync(cursor, head, pageSize, 2_097_152)
                .WaitAsync(cancellationToken).ConfigureAwait(false);
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SourceResponseDeserializationBytes, page.EnvelopeArrayBytes);
            if (page.ReasonCode is not null)
                throw new ProjectionDeliveryHistoryValidationException(page.ReasonCode is "delivery_page_limit_exceeded"
                    or "history_unavailable" or "source_conflict" or "delivery_gap" ? page.ReasonCode : "delivery_state_unavailable");
            long next = ProjectionStreamPageValidation.ValidateGuarded(identity, cursor, head, pageSize, page.Events);
            if (page.EnvelopeArrayBytes > 2_097_152 || (sourceBytes += page.EnvelopeArrayBytes) > ceiling)
                throw new ProjectionDeliveryHistoryValidationException("delivery_byte_limit_exceeded");
            foreach (EventEnvelope envelope in page.Events)
            {
                if (string.IsNullOrWhiteSpace(envelope.MessageId) || !ids.Add(envelope.MessageId))
                    throw new ProjectionDeliveryHistoryValidationException("delivery_identity_conflict");
                string eventHash = SourceProofCodec.ComputeEventHash(envelope);
                sourceHash.AppendData(Convert.FromHexString(eventHash));
                if (chain is not null) chain = SourceProofCodec.ExtendVerified(chain, envelope, eventHash);
                headId = envelope.MessageId;
                ProjectionEventReadabilityResult decoded = await ProjectionEventWireBuilder.BuildAsync(_payloadProtectionService,
                    identity, [envelope], cancellationToken).ConfigureAwait(false);
                if (decoded.Events is not { Length: 1 } events)
                {
                    if (decoded.UnreadableReason == UnreadableProtectedDataReason.ProviderUnavailable)
                        throw new InvalidOperationException("history_unavailable");
                    throw new ProjectionDeliveryHistoryValidationException("history_unavailable");
                }
                ProjectionEventDto item = events[0];
                long before = wireSize.Length, attempts = wireSize.AttemptedBytes;
                try
                {
                    if (item.SequenceNumber == 1) wireSize.WriteByte((byte)'[');
                    if (item.SequenceNumber > 1) wireSize.WriteByte((byte)',');
                    await ProjectionWireJsonSerializer.SerializeAsync(wireSize, item, daprClient.JsonSerializerOptions, cancellationToken).ConfigureAwait(false);
                    if (item.SequenceNumber == head) wireSize.WriteByte((byte)']');
                    readable?.Add(item);
                }
                catch (ProjectionRebuildPrefixSafetyLimitExceededException)
                { throw new ProjectionDeliveryHistoryValidationException("delivery_byte_limit_exceeded"); }
                finally { ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.WireSizingBytes, wireSize.AttemptedBytes - attempts); }
                ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.WireBytes, wireSize.Length - before);
                ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.WireEvents);
            }
            cursor = next;
        }
        if (anchor is not null && (anchor.HeadSequence != head || anchor.CumulativeDigest != chain!.CumulativeDigest
            || anchor.HeadMessageId != headId)) throw new ProjectionDeliveryHistoryValidationException("source_conflict");
        return (readable is null ? [] : [.. readable], Convert.ToHexStringLower(sourceHash.GetHashAndReset()), headId);
    }
}
