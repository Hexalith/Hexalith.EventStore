using System.Text.Json;

using Hexalith.EventStore.Client.Subscriptions;

using System.Net;
using System.Security.Cryptography;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Operations.Configuration;
using Hexalith.EventStore.Operations.Models;
using Hexalith.EventStore.Operations.Replay;
using Hexalith.EventStore.Operations.Telemetry;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Operations.Actors;

/// <summary>
/// Provides the durable serialization point for one subscriber dead-letter topic.
/// </summary>
public sealed class DeadLetterDrainActor(
    ActorHost host,
    IDeadLetterReplayTransport replayTransport,
    EventStoreOperationsTelemetry telemetry,
    IOptions<EventStoreOperationsOptions> options)
    : Actor(host), IDeadLetterDrainActor, IRemindable
{
    /// <summary>Gets the registered Dapr actor type name.</summary>
    public const string ActorTypeName = "EventStoreDeadLetterDrainActor";

    internal const string IndexStateName = "index";
    internal const string ReplayExhaustedPrefix = "replay-exhausted:";
    internal const string ReplayReminderName = "replay-drain";

    private readonly EventStoreOperationsOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    private readonly IDeadLetterReplayTransport _replayTransport = replayTransport ?? throw new ArgumentNullException(nameof(replayTransport));
    private readonly EventStoreOperationsTelemetry _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));

    /// <inheritdoc/>
    public async Task<DeadLetterCaptureResult> CaptureAsync(DeadLetterCaptureRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Identity);
        ArgumentNullException.ThrowIfNull(request.Body);

        if (string.IsNullOrWhiteSpace(request.Identity.MessageId)
            || string.IsNullOrWhiteSpace(request.BodySha256)
            || string.IsNullOrWhiteSpace(request.Topic)
            || !request.Identity.HasBoundedRetainedValues()
            || !DeadLetterSafeIdentity.IsValidValue(request.Topic)
            || request.Body.Length is < 1
            || request.Body.Length > _options.MaxBodyBytes
            || request.BodySha256.Length != 64
            || !string.Equals(
                request.BodySha256,
                Convert.ToHexStringLower(SHA256.HashData(request.Body)),
                StringComparison.Ordinal))
        {
            _telemetry.Capture(_options.TopicName, "unretainable");
            return new DeadLetterCaptureResult(DeadLetterCaptureOutcome.Unretainable);
        }

        string itemStateName = ItemStateName(request.Identity.MessageId);
        await StateManager.ClearCacheAsync().ConfigureAwait(false);
        ConditionalValue<DeadLetterRecord> existing = await StateManager
            .TryGetStateAsync<DeadLetterRecord>(itemStateName)
            .ConfigureAwait(false);
        if (existing.HasValue)
        {
            DeadLetterCaptureOutcome outcome = string.Equals(
                existing.Value.BodySha256,
                request.BodySha256,
                StringComparison.Ordinal)
                && ValidStoredBody(existing.Value)
                && existing.Value.Identity == request.Identity
                && existing.Value.Topic == request.Topic
                && existing.Value.DeliveryFingerprint == request.DeliveryFingerprint
                && (await ReadIndexAsync().ConfigureAwait(false)).MessageIds.Contains(request.Identity.MessageId, StringComparer.Ordinal)
                ? DeadLetterCaptureOutcome.Duplicate
                : DeadLetterCaptureOutcome.HashConflict;
            _telemetry.Capture(request.Topic, OutcomeCode(outcome));
            return new DeadLetterCaptureResult(outcome);
        }

        DeadLetterIndex index = await ReadIndexAsync().ConfigureAwait(false);
        var record = new DeadLetterRecord(
            request.Identity,
            request.Topic,
            request.Body,
            request.BodySha256,
            request.CapturedAtUtc,
            DeadLetterReplayState.Pending,
            0,
            request.Identity.IsReplayable ? null : "identity-unavailable")
        {
            DeliveryFingerprint = request.DeliveryFingerprint,
            DeliveryFailure = request.DeliveryFailure,
        };

        await StateManager.SetStateAsync(itemStateName, record).ConfigureAwait(false);
        await StateManager
            .SetStateAsync(IndexStateName, new DeadLetterIndex([.. index.MessageIds, request.Identity.MessageId]))
            .ConfigureAwait(false);
        if (request.DeliveryFailure is not null)
        {
            _ = await StateManager.TryRemoveStateAsync(AttemptStateName(request.Identity.MessageId)).ConfigureAwait(false);
        }

        // Dapr actors commit all staged item and index changes atomically through the actor state store.
        // The capture endpoint cannot acknowledge until this save completes.
        await StateManager.SaveStateAsync().ConfigureAwait(false);
        await StateManager.ClearCacheAsync().ConfigureAwait(false);
        ConditionalValue<DeadLetterRecord> saved = await StateManager.TryGetStateAsync<DeadLetterRecord>(itemStateName).ConfigureAwait(false);
        if (!saved.HasValue || !ValidStoredBody(saved.Value) || saved.Value.Identity != request.Identity || saved.Value.Topic != request.Topic
            || saved.Value.BodySha256 != request.BodySha256 || saved.Value.DeliveryFingerprint != request.DeliveryFingerprint
            || !MatchesEvidence(saved.Value.DeliveryFailure, request.DeliveryFailure)
            || !(await ReadIndexAsync().ConfigureAwait(false)).MessageIds.Contains(request.Identity.MessageId, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Durable subscriber poison capture could not be confirmed.");
        }

        _telemetry.Capture(request.Topic, "captured");
        await ObserveBacklogAsync().ConfigureAwait(false);
        return new DeadLetterCaptureResult(DeadLetterCaptureOutcome.Captured);
    }

    /// <inheritdoc/>
    public async Task<EventStoreSubscriberPoisonOutcome> ObserveSubscriberDeliveryAsync(SubscriberDeliveryObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        EventStoreSubscriberDelivery delivery = observation.Delivery;
        if (delivery is null || !delivery.IsValid() || delivery.SubscriberAppId != _options.ReplayAppId
            || delivery.PubSubName != _options.PubSubName
            || delivery.TopicName != _options.LiveTopicName
            || delivery.SubscriptionRoute != "/" + _options.ReplayMethodName.TrimStart('/')
            || observation.Failure is { } invalid && (!Enum.IsDefined(invalid.Reason) || invalid.AttemptLimit != _options.MaximumDeliveryAttempts
                || invalid.ObservationId == Guid.Empty))
        {
            return EventStoreSubscriberPoisonOutcome.Conflict;
        }

        EventStoreDomainEventEnvelope envelope = delivery.Envelope;
        string fingerprint = delivery.Fingerprint();
        string itemName = ItemStateName(envelope.MessageId);
        await StateManager.ClearCacheAsync().ConfigureAwait(false);
        var retained = await StateManager.TryGetStateAsync<DeadLetterRecord>(itemName).ConfigureAwait(false);
        if (retained.HasValue)
        {
            return ValidStoredBody(retained.Value) && retained.Value.DeliveryFingerprint == fingerprint
                && retained.Value.Identity.TenantId == envelope.TenantId && retained.Value.Identity.Domain == envelope.Domain
                && retained.Value.Topic == _options.TopicName
                && (await ReadIndexAsync().ConfigureAwait(false)).MessageIds.Contains(envelope.MessageId, StringComparer.Ordinal)
                ? EventStoreSubscriberPoisonOutcome.Retained
                : EventStoreSubscriberPoisonOutcome.Conflict;
        }

        if (observation.Failure is not { } failure)
        {
            return EventStoreSubscriberPoisonOutcome.Retryable;
        }

        string attemptName = AttemptStateName(envelope.MessageId);
        var previous = await StateManager.TryGetStateAsync<SubscriberFailureEvidence>(attemptName).ConfigureAwait(false);
        if (previous.HasValue && previous.Value.Fingerprint != fingerprint)
        {
            return EventStoreSubscriberPoisonOutcome.Conflict;
        }

        if (previous.HasValue && previous.Value.ObservationIds.Contains(failure.ObservationId))
        {
            return EventStoreSubscriberPoisonOutcome.Retryable;
        }

        DateTimeOffset now = observation.ObservedAtUtc;
        var evidence = new SubscriberFailureEvidence(fingerprint,
            previous.HasValue ? Math.Min(previous.Value.Attempts + 1, 1000) : 1,
            previous.HasValue ? previous.Value.FirstFailure : now,
            previous.HasValue && previous.Value.LastFailure > now ? previous.Value.LastFailure : now,
            failure.Reason, failure.Retryable)
        {
            ObservationIds = [.. previous.HasValue ? previous.Value.ObservationIds : [], failure.ObservationId],
        };
        if (failure.Retryable && evidence.Attempts < failure.AttemptLimit)
        {
            await StateManager.SetStateAsync(attemptName, evidence).ConfigureAwait(false);
            await StateManager.SaveStateAsync().ConfigureAwait(false);
            await StateManager.ClearCacheAsync().ConfigureAwait(false);
            var confirmed = await StateManager.TryGetStateAsync<SubscriberFailureEvidence>(attemptName).ConfigureAwait(false);
            if (!confirmed.HasValue || !MatchesEvidence(confirmed.Value, evidence))
            {
                throw new InvalidOperationException("Durable subscriber failure evidence could not be confirmed.");
            }

            return EventStoreSubscriberPoisonOutcome.Retryable;
        }

        var identity = new DeadLetterSafeIdentity(envelope.MessageId, envelope.TenantId, envelope.Domain,
            envelope.AggregateId, envelope.CorrelationId, envelope.EventTypeName)
        {
            CausationId = envelope.CausationId,
            SequenceNumber = envelope.SequenceNumber,
            OriginalTimestamp = envelope.Timestamp,
            OriginalTopic = delivery.TopicName,
        };
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(new
        {
            specversion = "1.0",
            id = delivery.Id,
            source = delivery.Source,
            type = envelope.EventTypeName,
            topic = delivery.TopicName,
            pubsubname = delivery.PubSubName,
            data = envelope,
        }, JsonSerializerOptions.Web);
        var capture = await CaptureAsync(new DeadLetterCaptureRequest(identity, _options.TopicName,
            body, Convert.ToHexStringLower(SHA256.HashData(body)), now)
        {
            DeliveryFingerprint = fingerprint,
            DeliveryFailure = evidence,
        }).ConfigureAwait(false);
        return capture.Outcome is DeadLetterCaptureOutcome.Captured or DeadLetterCaptureOutcome.Duplicate
            ? EventStoreSubscriberPoisonOutcome.Retained
            : EventStoreSubscriberPoisonOutcome.Conflict;
    }

    /// <inheritdoc/>
    public async Task<DeadLetterListResult> ListAsync(DeadLetterListRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegative(request.Offset);
        ArgumentOutOfRangeException.ThrowIfLessThan(request.Count, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(request.Count, _options.MaxListItems);
        if (request.TenantId is not null && !DeadLetterSafeIdentity.IsValidValue(request.TenantId))
        {
            throw new ArgumentException("The tenant scope is invalid.", nameof(request));
        }

        await StateManager.ClearCacheAsync().ConfigureAwait(false);
        DeadLetterIndex index = await ReadIndexAsync().ConfigureAwait(false);
        var page = new List<DeadLetterListItem>(request.Count);
        int totalCount = 0;
        DateTimeOffset? oldestFailureAt = null;
        int? nextOffset = null;
        for (int rawIndex = 0; rawIndex < index.MessageIds.Count; rawIndex++)
        {
            string messageId = index.MessageIds[rawIndex];
            ConditionalValue<DeadLetterRecord> item = await StateManager
                .TryGetStateAsync<DeadLetterRecord>(ItemStateName(messageId))
                .ConfigureAwait(false);
            if (!item.HasValue || item.Value.Identity?.MessageId != messageId
                || !item.Value.Identity.HasBoundedRetainedValues())
            {
                throw new InvalidOperationException("The indexed subscriber poison evidence is unavailable.");
            }

            if (request.TenantId is not null && !TenantMatches(item.Value.Identity, request.TenantId))
            {
                continue;
            }

            if (!Enum.IsDefined(item.Value.State))
            {
                throw new InvalidOperationException("The indexed subscriber poison evidence is unavailable.");
            }

            if (!IsOpenState(item.Value.State))
            {
                continue;
            }

            totalCount++;
            DateTimeOffset failureAt = item.Value.DeliveryFailure?.FirstFailure ?? item.Value.CapturedAtUtc;
            oldestFailureAt = oldestFailureAt is null || failureAt < oldestFailureAt ? failureAt : oldestFailureAt;
            if (rawIndex < request.Offset)
            {
                continue;
            }

            if (page.Count < request.Count)
            {
                page.Add(ToListItem(item.Value));
                continue;
            }

            // The cursor points at the next entry that actually satisfies this request, not merely at the next
            // raw index. Handing back a cursor derived from index position alone yields an empty page whenever
            // every later entry is terminal or belongs to another tenant.
            nextOffset ??= rawIndex;
        }

        return new DeadLetterListResult(page, totalCount, nextOffset) { OldestFailureAt = oldestFailureAt };
    }

    /// <inheritdoc/>
    public async Task<DeadLetterActorActionResult> RetryAsync(DeadLetterActionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        string? operationId = ActionOperationId(request, "retry");
        if (operationId is not null && await ReadActionAsync(operationId).ConfigureAwait(false) is { } existing)
        {
            if (!existing.MessageIds.SequenceEqual(ActionMessageIds(request), StringComparer.Ordinal))
            {
                return new DeadLetterActorActionResult(false, "action-conflict");
            }

            if (existing.Result.State == "pending")
            {
                await ArmReplayReminderAsync().ConfigureAwait(false);
            }

            return existing.Result;
        }

        IReadOnlyList<DeadLetterRecord>? records = await LoadAuthorizedRecordsAsync(request, allowExhausted: true)
            .ConfigureAwait(false);
        if (records is null)
        {
            return HiddenNotFound();
        }

        if (records.Any(static record => !record.Identity.IsReplayable))
        {
            _telemetry.Action(_options.TopicName, "rejected", "identity-unavailable");
            return new DeadLetterActorActionResult(false, "invalid-operation");
        }

        if (operationId is not null && await HasPendingActionAsync(records).ConfigureAwait(false))
        {
            return new DeadLetterActorActionResult(false, "action-pending");
        }

        foreach (DeadLetterRecord record in records)
        {
            // The attempt counter is reset here on purpose. It bounds one operator-requested replay campaign, not
            // the item's lifetime: an operator who has fixed the target is asking for a fresh budget, and carrying
            // the previous outage's attempts forward would exhaust the item on its first delivery and archive it
            // beyond recovery.
            await StateManager
                .SetStateAsync(
                    ItemStateName(record.Identity.MessageId),
                    record with
                    {
                        State = DeadLetterReplayState.ReplayRequested,
                        ReplayAttempts = 0,
                        LastReasonCode = null,
                        ActionOperationId = operationId,
                    })
                .ConfigureAwait(false);
        }

        var accepted = new DeadLetterActorActionResult(true, "replay-requested") { OperationId = operationId, State = operationId is null ? null : "pending" };
        if (operationId is not null)
        {
            await StageActionAsync(request, "retry", accepted).ConfigureAwait(false);
        }

        // Persist operator intent before reminder registration or delivery. A crash after this save is recovered
        // by actor activation and may repeat delivery, which the subscriber marker store tolerates.
        await StateManager.SaveStateAsync().ConfigureAwait(false);
        if (operationId is not null)
        {
            accepted = (await RequireActionAsync(operationId).ConfigureAwait(false)).Result;
        }

        await ArmReplayReminderAsync().ConfigureAwait(false);
        if (operationId is null)
        {
            // Preserve the direct actor compatibility path used by existing development consumers.
            await DrainReplayRequestsAsync().ConfigureAwait(false);
        }

        _telemetry.Action(_options.TopicName, "accepted", "replay-requested");
        return accepted;
    }

    /// <inheritdoc/>
    public Task<DeadLetterActorActionResult> SkipAsync(DeadLetterActionRequest request)
        => ArchiveCoreAsync(request, "operator-skip");

    /// <inheritdoc/>
    public Task<DeadLetterActorActionResult> ArchiveAsync(DeadLetterActionRequest request)
        => ArchiveCoreAsync(request, "operator-archive");

    /// <inheritdoc/>
    public async Task ReceiveReminderAsync(
        string reminderName,
        byte[] state,
        TimeSpan dueTime,
        TimeSpan period)
    {
        _ = state;
        _ = dueTime;
        _ = period;
        if (!string.Equals(reminderName, ReplayReminderName, StringComparison.Ordinal))
        {
            return;
        }

        await NormalizeReplayingAsync().ConfigureAwait(false);
        await DrainReplayRequestsAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async Task OnActivateAsync()
    {
        DeadLetterIndex index = await ReadIndexAsync().ConfigureAwait(false);
        bool recoveryNeeded = false;
        foreach (string messageId in index.MessageIds)
        {
            ConditionalValue<DeadLetterRecord> item = await StateManager
                .TryGetStateAsync<DeadLetterRecord>(ItemStateName(messageId))
                .ConfigureAwait(false);
            if (item.HasValue && item.Value.State is DeadLetterReplayState.ReplayRequested or DeadLetterReplayState.Replaying)
            {
                recoveryNeeded = true;
                if (item.Value.State == DeadLetterReplayState.Replaying)
                {
                    await StateManager
                        .SetStateAsync(
                            ItemStateName(messageId),
                            item.Value with { State = DeadLetterReplayState.ReplayRequested, LastReasonCode = "restart-recovery" })
                        .ConfigureAwait(false);
                }
            }
        }

        if (recoveryNeeded)
        {
            await StateManager.SaveStateAsync().ConfigureAwait(false);
            await ArmReplayReminderAsync().ConfigureAwait(false);
        }

        await ObserveBacklogAsync().ConfigureAwait(false);
    }

    private static string AttemptStateName(string messageId) => "subscriber-attempt:" + Uri.EscapeDataString(messageId);

    internal static string ItemStateName(string messageId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        if (!DeadLetterSafeIdentity.IsValidValue(messageId))
        {
            throw new ArgumentOutOfRangeException(nameof(messageId));
        }

        return "item:" + Uri.EscapeDataString(messageId);
    }

    private async Task<DeadLetterActorActionResult> ArchiveCoreAsync(DeadLetterActionRequest request, string reasonCode)
    {
        ArgumentNullException.ThrowIfNull(request);
        string? operationId = ActionOperationId(request, reasonCode);
        if (operationId is not null && await ReadActionAsync(operationId).ConfigureAwait(false) is { } existing)
        {
            return existing.MessageIds.SequenceEqual(ActionMessageIds(request), StringComparer.Ordinal)
                ? existing.Result
                : new DeadLetterActorActionResult(false, "action-conflict");
        }

        IReadOnlyList<DeadLetterRecord>? records = await LoadAuthorizedRecordsAsync(request).ConfigureAwait(false);
        if (records is null)
        {
            return HiddenNotFound();
        }

        if (operationId is not null && await HasPendingActionAsync(records).ConfigureAwait(false))
        {
            return new DeadLetterActorActionResult(false, "action-pending");
        }

        foreach (DeadLetterRecord record in records)
        {
            await StateManager
                .SetStateAsync(
                    ItemStateName(record.Identity.MessageId),
                    record with { State = DeadLetterReplayState.Archived, LastReasonCode = reasonCode, ActionOperationId = operationId })
                .ConfigureAwait(false);
        }

        var completed = new DeadLetterActorActionResult(true, reasonCode) { OperationId = operationId, State = operationId is null ? null : "completed" };
        if (operationId is not null)
        {
            await StageActionAsync(request, reasonCode, completed).ConfigureAwait(false);
        }

        await StateManager.SaveStateAsync().ConfigureAwait(false);
        if (operationId is not null)
        {
            completed = (await RequireActionAsync(operationId).ConfigureAwait(false)).Result;
        }
        _telemetry.Action(_options.TopicName, "archived", reasonCode);
        await ObserveBacklogAsync().ConfigureAwait(false);
        return completed;
    }

    private static string? ActionOperationId(DeadLetterActionRequest request, string operation)
    {
        if (request.RequestedBy is null)
        {
            return null;
        }

        return EventStorePayloadFingerprint.Compute(JsonSerializer.SerializeToUtf8Bytes(new
        {
            request.TenantId,
            Actor = request.RequestedBy,
            operation,
            Identity = request.ActionId,
            Messages = request.ActionId is null ? ActionMessageIds(request) : null,
        }));
    }

    private static IOrderedEnumerable<string> ActionMessageIds(DeadLetterActionRequest request)
        => request.MessageIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);

    private static string ActionStateName(string operationId) => "action:" + operationId;

    private async Task<bool> HasPendingActionAsync(IReadOnlyList<DeadLetterRecord> records)
    {
        foreach (DeadLetterRecord record in records)
        {
            if (record.State is DeadLetterReplayState.ReplayRequested or DeadLetterReplayState.Replaying
                || record.ActionOperationId is { } operationId
                    && (await RequireActionAsync(operationId).ConfigureAwait(false)).Result.State == "pending")
            {
                return true;
            }
        }

        return false;
    }

    private async Task<DeadLetterActionEvidence?> ReadActionAsync(string operationId)
    {
        await StateManager.ClearCacheAsync().ConfigureAwait(false);
        var evidence = await StateManager.TryGetStateAsync<DeadLetterActionEvidence>(ActionStateName(operationId)).ConfigureAwait(false);
        return evidence.HasValue ? evidence.Value : null;
    }

    private async Task<DeadLetterActionEvidence> RequireActionAsync(string operationId)
        => await ReadActionAsync(operationId).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The subscriber recovery action could not be confirmed.");

    private Task StageActionAsync(DeadLetterActionRequest request, string operation, DeadLetterActorActionResult result)
        => StateManager.SetStateAsync(ActionStateName(result.OperationId!), new DeadLetterActionEvidence(
            result, request.TenantId, [.. ActionMessageIds(request)],
            request.RequestedBy!, operation));

    private async Task StageReplayActionOutcomeAsync(string? operationId)
    {
        if (operationId is null)
        {
            return;
        }

        var evidence = await StateManager.TryGetStateAsync<DeadLetterActionEvidence>(ActionStateName(operationId)).ConfigureAwait(false);
        if (!evidence.HasValue || evidence.Value.Result.State != "pending")
        {
            throw new InvalidOperationException("The subscriber replay action evidence is unavailable.");
        }

        bool failed = false;
        foreach (string messageId in evidence.Value.MessageIds)
        {
            var item = await StateManager.TryGetStateAsync<DeadLetterRecord>(ItemStateName(messageId)).ConfigureAwait(false);
            if (!item.HasValue || item.Value.ActionOperationId != operationId || item.Value.Identity.TenantId != evidence.Value.TenantId)
            {
                throw new InvalidOperationException("The subscriber replay item evidence is unavailable.");
            }

            if (item.Value.State is DeadLetterReplayState.ReplayRequested or DeadLetterReplayState.Replaying)
            {
                return;
            }

            failed |= item.Value.State != DeadLetterReplayState.Replayed;
        }

        DeadLetterActorActionResult result = evidence.Value.Result with
        {
            Success = !failed,
            State = failed ? "failed" : "completed",
            ReasonCode = failed ? "replay-failed" : "target-acknowledged",
        };
        await StateManager.SetStateAsync(ActionStateName(operationId), evidence.Value with { Result = result }).ConfigureAwait(false);
    }

    private async Task ArmReplayReminderAsync()
    {
        try
        {
            TimeSpan period = TimeSpan.FromSeconds(_options.ReplayReminderPeriodSeconds);
            _ = await RegisterReminderAsync(ReplayReminderName, null, TimeSpan.Zero, period).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // ReplayRequested is already durable. Activation will retry reminder registration; no payload or
            // identifier is logged here, and the bounded metric communicates the degraded recovery path.
            _telemetry.Action(_options.TopicName, "degraded", "reminder-registration");
            throw;
        }
    }

    private async Task DrainReplayRequestsAsync()
    {
        DeadLetterIndex index = await ReadIndexAsync().ConfigureAwait(false);
        foreach (string messageId in index.MessageIds)
        {
            ConditionalValue<DeadLetterRecord> item = await StateManager
                .TryGetStateAsync<DeadLetterRecord>(ItemStateName(messageId))
                .ConfigureAwait(false);
            if (!item.HasValue || item.Value.State != DeadLetterReplayState.ReplayRequested)
            {
                continue;
            }

            DeadLetterRecord replaying = item.Value with
            {
                State = DeadLetterReplayState.Replaying,
                ReplayAttempts = item.Value.ReplayAttempts == int.MaxValue
                    ? int.MaxValue
                    : item.Value.ReplayAttempts + 1,
                LastReasonCode = null,
            };
            await StateManager.SetStateAsync(ItemStateName(messageId), replaying).ConfigureAwait(false);
            await StateManager.SaveStateAsync().ConfigureAwait(false);

            string? failure = null;
            try
            {
                await _replayTransport.DeliverAsync(replaying.Body).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failure = ReplayFailureReason(ex);
            }

            bool exhausted = failure is not null && replaying.ReplayAttempts >= _options.MaxReplayAttempts;
            string reason = failure ?? "target-acknowledged";
            await StateManager.SetStateAsync(ItemStateName(messageId), replaying with
            {
                State = failure is null ? DeadLetterReplayState.Replayed
                    : exhausted ? DeadLetterReplayState.Archived : DeadLetterReplayState.ReplayRequested,
                LastReasonCode = exhausted ? ReplayExhaustedPrefix + reason : reason,
            }).ConfigureAwait(false);
            await StageReplayActionOutcomeAsync(replaying.ActionOperationId).ConfigureAwait(false);
            await StateManager.SaveStateAsync().ConfigureAwait(false);
            _telemetry.Action(_options.TopicName, failure is null ? "replayed" : exhausted ? "exhausted" : "retryable", reason);

        }

        // Observed once per drain rather than once per item: the observation rescans the whole backlog, so
        // doing it inside the loop makes a drain quadratic in retained items.
        await ObserveBacklogAsync().ConfigureAwait(false);

        if (!await HasReplayRequestsAsync().ConfigureAwait(false))
        {
            try
            {
                await UnregisterReminderAsync(ReplayReminderName).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _telemetry.Action(_options.TopicName, "degraded", "reminder-unregistration");
            }
        }
    }

    private async Task<bool> HasReplayRequestsAsync()
    {
        DeadLetterIndex index = await ReadIndexAsync().ConfigureAwait(false);
        foreach (string messageId in index.MessageIds)
        {
            ConditionalValue<DeadLetterRecord> item = await StateManager
                .TryGetStateAsync<DeadLetterRecord>(ItemStateName(messageId))
                .ConfigureAwait(false);
            if (item.HasValue && item.Value.State == DeadLetterReplayState.ReplayRequested)
            {
                return true;
            }
        }

        return false;
    }

    private async Task NormalizeReplayingAsync()
    {
        DeadLetterIndex index = await ReadIndexAsync().ConfigureAwait(false);
        bool changed = false;
        foreach (string messageId in index.MessageIds)
        {
            ConditionalValue<DeadLetterRecord> item = await StateManager
                .TryGetStateAsync<DeadLetterRecord>(ItemStateName(messageId))
                .ConfigureAwait(false);
            if (item.HasValue && item.Value.State == DeadLetterReplayState.Replaying)
            {
                await StateManager
                    .SetStateAsync(
                        ItemStateName(messageId),
                        item.Value with
                        {
                            State = DeadLetterReplayState.ReplayRequested,
                            LastReasonCode = "restart-recovery",
                        })
                    .ConfigureAwait(false);
                changed = true;
            }
        }

        if (changed)
        {
            await StateManager.SaveStateAsync().ConfigureAwait(false);
        }
    }

    private async Task ObserveBacklogAsync()
    {
        DeadLetterIndex index = await ReadIndexAsync().ConfigureAwait(false);
        int count = 0;
        DateTimeOffset? oldest = null;
        foreach (string messageId in index.MessageIds)
        {
            ConditionalValue<DeadLetterRecord> item = await StateManager
                .TryGetStateAsync<DeadLetterRecord>(ItemStateName(messageId))
                .ConfigureAwait(false);
            if (!item.HasValue || !IsOpenState(item.Value.State))
            {
                continue;
            }

            count++;
            oldest = oldest is null || item.Value.CapturedAtUtc < oldest.Value
                ? item.Value.CapturedAtUtc
                : oldest;
        }

        _telemetry.SetBacklog(_options.TopicName, count, oldest);
    }

    private DeadLetterActorActionResult HiddenNotFound()
    {
        _telemetry.Action(_options.TopicName, "rejected", "not-found");
        return new DeadLetterActorActionResult(false, "not-found");
    }

    private async Task<IReadOnlyList<DeadLetterRecord>?> LoadAuthorizedRecordsAsync(
        DeadLetterActionRequest request,
        bool allowExhausted = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TenantId);
        ArgumentNullException.ThrowIfNull(request.MessageIds);
        if (!DeadLetterSafeIdentity.IsValidValue(request.TenantId)
            || request.MessageIds.Count == 0
            || request.MessageIds.Count > _options.MaxActionItems)
        {
            return null;
        }

        var records = new List<DeadLetterRecord>(request.MessageIds.Count);
        foreach (string messageId in request.MessageIds.Distinct(StringComparer.Ordinal))
        {
            if (!DeadLetterSafeIdentity.IsValidValue(messageId))
            {
                return null;
            }

            ConditionalValue<DeadLetterRecord> item = await StateManager
                .TryGetStateAsync<DeadLetterRecord>(ItemStateName(messageId))
                .ConfigureAwait(false);
            if (!item.HasValue
                || !(IsOpenState(item.Value.State) || (allowExhausted && IsReplayExhausted(item.Value)))
                || !TenantMatches(item.Value.Identity, request.TenantId))
            {
                return null;
            }

            records.Add(item.Value);
        }

        return records;
    }

    private async Task<DeadLetterIndex> ReadIndexAsync()
    {
        ConditionalValue<DeadLetterIndex> index = await StateManager
            .TryGetStateAsync<DeadLetterIndex>(IndexStateName)
            .ConfigureAwait(false);
        return index.HasValue ? index.Value : DeadLetterIndex.Empty;
    }

    private static bool TenantMatches(DeadLetterSafeIdentity identity, string tenantId)
        => string.Equals(
            identity.TenantId ?? DeadLetterSafeIdentity.UnidentifiedTenantId,
            tenantId,
            StringComparison.Ordinal);

    /// <summary>
    /// Identifies an item the drain archived because its replay budget ran out, as opposed to one an operator
    /// deliberately skipped or archived. Only the former may be re-opened by an explicit retry.
    /// </summary>
    private static bool IsReplayExhausted(DeadLetterRecord record)
        => record.State == DeadLetterReplayState.Archived
            && record.LastReasonCode is not null
            && record.LastReasonCode.StartsWith(ReplayExhaustedPrefix, StringComparison.Ordinal);

    private static bool IsOpenState(DeadLetterReplayState state)
        => state is DeadLetterReplayState.Pending
            or DeadLetterReplayState.ReplayRequested
            or DeadLetterReplayState.Replaying;

    private static DeadLetterListItem ToListItem(DeadLetterRecord record)
        => new(
            record.Identity,
            record.CapturedAtUtc,
            record.ReplayAttempts,
            record.State,
            record.LastReasonCode)
        {
            DeliveryFailure = record.DeliveryFailure,
        };

    private bool ValidStoredBody(DeadLetterRecord record)
        => record.Body is { Length: > 0 } && record.Body.Length <= _options.MaxBodyBytes
            && record.BodySha256 == Convert.ToHexStringLower(SHA256.HashData(record.Body));

    private static bool MatchesEvidence(SubscriberFailureEvidence? stored, SubscriberFailureEvidence? expected)
        => (stored is null && expected is null) || (stored is not null && expected is not null
            && stored.Fingerprint == expected.Fingerprint && stored.Attempts == expected.Attempts
            && stored.FirstFailure == expected.FirstFailure && stored.LastFailure == expected.LastFailure
            && stored.Reason == expected.Reason && stored.Retryable == expected.Retryable
            && stored.ObservationIds.SequenceEqual(expected.ObservationIds));

    private static string OutcomeCode(DeadLetterCaptureOutcome outcome)
        => outcome switch
        {
            DeadLetterCaptureOutcome.Captured => "captured",
            DeadLetterCaptureOutcome.Duplicate => "duplicate",
            DeadLetterCaptureOutcome.HashConflict => "hash-conflict",
            DeadLetterCaptureOutcome.Unretainable => "unretainable",
            _ => "unknown",
        };

    private static string ReplayFailureReason(Exception exception)
    {
        Exception current = exception;
        while (current.InnerException is not null)
        {
            current = current.InnerException;
        }

        return current switch
        {
            TimeoutException => "timeout",
            HttpRequestException { StatusCode: HttpStatusCode.BadRequest } => "target-invalid",
            HttpRequestException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden } => "target-forbidden",
            HttpRequestException { StatusCode: HttpStatusCode.NotFound } => "target-not-found",
            HttpRequestException => "target-unavailable",
            _ => "delivery-failed",
        };
    }
}
