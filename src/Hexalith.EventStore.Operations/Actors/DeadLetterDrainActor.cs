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
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Identity.MessageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.BodySha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Topic);
        if (!request.Identity.HasBoundedRetainedValues()
            || !DeadLetterSafeIdentity.IsValidValue(request.Topic)
            || request.Body.Length is < 1
            || request.Body.Length > _options.MaxBodyBytes
            || request.BodySha256.Length != 64
            || !string.Equals(
                request.BodySha256,
                Convert.ToHexStringLower(SHA256.HashData(request.Body)),
                StringComparison.Ordinal))
        {
            throw new ArgumentException("The retained identity is invalid.", nameof(request));
        }

        string itemStateName = ItemStateName(request.Identity.MessageId);
        ConditionalValue<DeadLetterRecord> existing = await StateManager
            .TryGetStateAsync<DeadLetterRecord>(itemStateName)
            .ConfigureAwait(false);
        if (existing.HasValue)
        {
            DeadLetterCaptureOutcome outcome = string.Equals(
                existing.Value.BodySha256,
                request.BodySha256,
                StringComparison.Ordinal)
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
            request.Identity.IsReplayable ? null : "identity-unavailable");

        await StateManager.SetStateAsync(itemStateName, record).ConfigureAwait(false);
        await StateManager
            .SetStateAsync(IndexStateName, new DeadLetterIndex([.. index.MessageIds, request.Identity.MessageId]))
            .ConfigureAwait(false);

        // Dapr actors commit all staged item and index changes atomically through the actor state store.
        // The capture endpoint cannot acknowledge until this save completes.
        await StateManager.SaveStateAsync().ConfigureAwait(false);
        _telemetry.Capture(request.Topic, "captured");
        await ObserveBacklogAsync().ConfigureAwait(false);
        return new DeadLetterCaptureResult(DeadLetterCaptureOutcome.Captured);
    }

    /// <inheritdoc/>
    public async Task<DeadLetterListResult> ListAsync(DeadLetterListRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegative(request.Offset);
        ArgumentOutOfRangeException.ThrowIfLessThan(request.Count, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(request.Count, 500);
        if (request.TenantId is not null && !DeadLetterSafeIdentity.IsValidValue(request.TenantId))
        {
            throw new ArgumentException("The tenant scope is invalid.", nameof(request));
        }

        DeadLetterIndex index = await ReadIndexAsync().ConfigureAwait(false);
        var page = new List<DeadLetterListItem>(request.Count);
        int totalCount = 0;
        int? nextOffset = null;
        for (int rawIndex = 0; rawIndex < index.MessageIds.Count; rawIndex++)
        {
            string messageId = index.MessageIds[rawIndex];
            ConditionalValue<DeadLetterRecord> item = await StateManager
                .TryGetStateAsync<DeadLetterRecord>(ItemStateName(messageId))
                .ConfigureAwait(false);
            if (!item.HasValue
                || !IsOpenState(item.Value.State)
                || (request.TenantId is not null
                    && !TenantMatches(item.Value.Identity, request.TenantId)))
            {
                continue;
            }

            totalCount++;
            if (rawIndex < request.Offset || page.Count >= request.Count)
            {
                continue;
            }

            page.Add(ToListItem(item.Value));
            if (page.Count == request.Count && rawIndex < index.MessageIds.Count - 1)
            {
                nextOffset = rawIndex + 1;
            }
        }

        return new DeadLetterListResult(page, totalCount, nextOffset);
    }

    /// <inheritdoc/>
    public async Task<DeadLetterActorActionResult> RetryAsync(DeadLetterActionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        IReadOnlyList<DeadLetterRecord>? records = await LoadAuthorizedRecordsAsync(request).ConfigureAwait(false);
        if (records is null)
        {
            return HiddenNotFound();
        }

        if (records.Any(static record => !record.Identity.IsReplayable))
        {
            _telemetry.Action(_options.TopicName, "rejected", "identity-unavailable");
            return new DeadLetterActorActionResult(false, "invalid-operation");
        }

        foreach (DeadLetterRecord record in records)
        {
            await StateManager
                .SetStateAsync(
                    ItemStateName(record.Identity.MessageId),
                    record with { State = DeadLetterReplayState.ReplayRequested, LastReasonCode = null })
                .ConfigureAwait(false);
        }

        // Persist operator intent before reminder registration or delivery. A crash after this save is recovered
        // by actor activation and may repeat delivery, which the subscriber marker store tolerates.
        await StateManager.SaveStateAsync().ConfigureAwait(false);
        await ArmReplayReminderAsync().ConfigureAwait(false);
        await DrainReplayRequestsAsync().ConfigureAwait(false);
        _telemetry.Action(_options.TopicName, "accepted", "replay-requested");
        return new DeadLetterActorActionResult(true, "replay-requested");
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
        IReadOnlyList<DeadLetterRecord>? records = await LoadAuthorizedRecordsAsync(request).ConfigureAwait(false);
        if (records is null)
        {
            return HiddenNotFound();
        }

        foreach (DeadLetterRecord record in records)
        {
            await StateManager
                .SetStateAsync(
                    ItemStateName(record.Identity.MessageId),
                    record with { State = DeadLetterReplayState.Archived, LastReasonCode = reasonCode })
                .ConfigureAwait(false);
        }

        await StateManager.SaveStateAsync().ConfigureAwait(false);
        _telemetry.Action(_options.TopicName, "archived", reasonCode);
        await ObserveBacklogAsync().ConfigureAwait(false);
        return new DeadLetterActorActionResult(true, reasonCode);
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

            try
            {
                await _replayTransport.DeliverAsync(replaying.Body).ConfigureAwait(false);
                await StateManager
                    .SetStateAsync(
                        ItemStateName(messageId),
                        replaying with { State = DeadLetterReplayState.Replayed, LastReasonCode = "target-acknowledged" })
                    .ConfigureAwait(false);
                await StateManager.SaveStateAsync().ConfigureAwait(false);
                _telemetry.Action(_options.TopicName, "replayed", "target-acknowledged");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A target that rejects this item permanently must not be re-delivered every reminder period
                // forever. Once the attempt ceiling is reached the item leaves the backlog as archived, carrying
                // both the exhaustion marker and the last failure reason; its retained body is untouched.
                string reason = ReplayFailureReason(ex);
                bool exhausted = replaying.ReplayAttempts >= _options.MaxReplayAttempts;
                await StateManager
                    .SetStateAsync(
                        ItemStateName(messageId),
                        replaying with
                        {
                            State = exhausted ? DeadLetterReplayState.Archived : DeadLetterReplayState.ReplayRequested,
                            LastReasonCode = exhausted ? "replay-exhausted:" + reason : reason,
                        })
                    .ConfigureAwait(false);
                await StateManager.SaveStateAsync().ConfigureAwait(false);
                _telemetry.Action(_options.TopicName, exhausted ? "exhausted" : "retryable", reason);
            }
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

    private async Task<IReadOnlyList<DeadLetterRecord>?> LoadAuthorizedRecordsAsync(DeadLetterActionRequest request)
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
                || !IsOpenState(item.Value.State)
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
            record.LastReasonCode);

    private static string OutcomeCode(DeadLetterCaptureOutcome outcome)
        => outcome switch
        {
            DeadLetterCaptureOutcome.Captured => "captured",
            DeadLetterCaptureOutcome.Duplicate => "duplicate",
            DeadLetterCaptureOutcome.HashConflict => "hash-conflict",
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
