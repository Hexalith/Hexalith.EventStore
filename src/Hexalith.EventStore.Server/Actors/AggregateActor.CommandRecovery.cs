using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.Events;

using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Server.Actors;

public partial class AggregateActor
{
    private bool HasVerifiedCheckpointIdentity(AggregateIdentity identity, PipelineState checkpoint)
        => string.Equals(identity.ActorId, Host.Id.GetId(), StringComparison.Ordinal)
            && HasCompletePipelineIdentity(checkpoint)
            && !string.IsNullOrWhiteSpace(checkpoint.CorrelationId)
            && checkpoint.EventCount is >= 0
            && (checkpoint.EventCount == 0
                ? checkpoint.StartSequence is null && checkpoint.EndSequence is null
                : checkpoint.StartSequence is > 0
                    && checkpoint.EndSequence >= checkpoint.StartSequence
                    && checkpoint.EndSequence <= int.MaxValue
                    && checkpoint.EndSequence - checkpoint.StartSequence + 1 == checkpoint.EventCount);

    private async Task<IReadOnlyList<EventEnvelope>?> TryVerifyCheckpointAsync(
        AggregateIdentity identity, PipelineState checkpoint)
    {
        if (!HasVerifiedCheckpointIdentity(identity, checkpoint))
        {
            return null;
        }

        if (checkpoint.EventCount == 0)
        {
            return [];
        }

        return await TryVerifyPersistedRangeAsync(identity, checkpoint.StartSequence!.Value,
            checkpoint.EndSequence!.Value, checkpoint.EventCount!.Value,
            checkpoint.CorrelationId, checkpoint.CausationId).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<EventEnvelope>?> TryVerifyPersistedRangeAsync(
        AggregateIdentity identity, long start, long end, int count, string correlationId, string? causationId)
    {
        if (start <= 0 || end < start || end > int.MaxValue || count <= 0
            || end - start + 1 != count || string.IsNullOrWhiteSpace(correlationId)
            || string.IsNullOrWhiteSpace(causationId))
        {
            ReportRecoveryRetention(correlationId, "range_identity_invalid");
            return null;
        }

        try
        {
            IReadOnlyList<EventEnvelope> events = await LoadPersistedEventsRangeAsync(identity, start, end)
                .ConfigureAwait(false);
            if (!HasVerifiedEventRange(identity, events, start, end, count, correlationId, causationId))
            {
                ReportRecoveryRetention(correlationId, "range_envelope_identity_invalid");
                return null;
            }
            return events;
        }
        catch (Exception exception) when (exception is not (OperationCanceledException or DrainStateStoreException))
        {
            ReportRecoveryRetention(correlationId, "range_preparation_invalid");
            return null;
        }
    }

    private async Task<string?> ReadVerifiedDrainCausationAsync(UnpublishedEventsRecord record, string trackingId)
    {
        if (!string.IsNullOrWhiteSpace(record.MessageId) && record.CausationId is not null)
        {
            return record.CausationId;
        }

        string? persisted = await TryReadDrainCausationIdAsync(record, trackingId).ConfigureAwait(false);
        return record.CausationId is null || string.Equals(record.CausationId, persisted, StringComparison.Ordinal)
            ? persisted : null;
    }

    private async Task<TerminalAdvisoryRepairIndex> ReadTerminalAdvisoryIndexAsync()
    {
        ConditionalValue<TerminalAdvisoryRepairIndex> state = await StateManager
            .TryGetStateAsync<TerminalAdvisoryRepairIndex>(TerminalAdvisoryRepairIndex.StateKey)
            .ConfigureAwait(false);
        return state.HasValue && state.Value is not null ? state.Value : TerminalAdvisoryRepairIndex.Empty;
    }

    private async Task<TerminalAdvisoryRepairIndex> StageTerminalAdvisoryAsync(TerminalAdvisoryIntent intent)
    {
        ConditionalValue<TerminalAdvisoryIntent> existing = await StateManager
            .TryGetStateAsync<TerminalAdvisoryIntent>(TerminalAdvisoryIntent.GetStateKey(intent.MessageId))
            .ConfigureAwait(false);
        if (existing.HasValue && existing.Value != intent)
        {
            throw new InvalidOperationException("A different terminal advisory already owns this message identity.");
        }

        TerminalAdvisoryRepairIndex index = await ReadTerminalAdvisoryIndexAsync().ConfigureAwait(false);
        if (!index.Entries.Any(entry => entry is not null
            && string.Equals(entry.MessageId, intent.MessageId, StringComparison.Ordinal)))
        {
            index = new TerminalAdvisoryRepairIndex([.. index.Entries, new(intent.MessageId)]);
        }

        await StateManager.SetStateAsync(TerminalAdvisoryIntent.GetStateKey(intent.MessageId), intent)
            .ConfigureAwait(false);
        await StateManager.SetStateAsync(TerminalAdvisoryRepairIndex.StateKey, index).ConfigureAwait(false);
        return index;
    }

    private bool TerminalAdvisoryMatchesActor(TerminalAdvisoryIntent intent)
    {
        AggregateIdentity identity = GetAggregateIdentityFromActorId();
        return string.Equals(intent.TenantId, identity.TenantId, StringComparison.Ordinal)
            && string.Equals(intent.Domain, identity.Domain, StringComparison.Ordinal)
            && string.Equals(intent.AggregateId, identity.AggregateId, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(intent.MessageId)
            && !string.IsNullOrWhiteSpace(intent.CorrelationId)
            && !string.IsNullOrWhiteSpace(intent.CausationId)
            && !string.IsNullOrWhiteSpace(intent.CommandType)
            && intent.EventCount >= 0
            && (intent.EventCount == 0
                ? intent.StartSequence is null && intent.EndSequence is null
                : intent.StartSequence is > 0 && intent.EndSequence >= intent.StartSequence
                    && intent.EndSequence - intent.StartSequence + 1 == intent.EventCount)
            && intent.Status is { Status: CommandStatus.Completed or CommandStatus.Rejected }
            && (intent.Status.EventCount == intent.EventCount
                || intent.EventCount == 0 && intent.Status.Status == CommandStatus.Rejected && intent.Status.EventCount is null)
            && (intent.Status.CommittedEventSequence is null
                || intent.Status.Status == CommandStatus.Completed && intent.EventCount > 0
                    && intent.Status.CommittedEventSequence == intent.EndSequence)
            && string.Equals(intent.Status.MessageId, intent.MessageId, StringComparison.Ordinal)
            && string.Equals(intent.Status.CorrelationId, intent.CorrelationId, StringComparison.Ordinal)
            && string.Equals(intent.Status.Domain, intent.Domain, StringComparison.Ordinal)
            && string.Equals(intent.Status.AggregateId, intent.AggregateId, StringComparison.Ordinal);
    }

    private async Task RepairMatchingTerminalAdvisoryAsync(CommandEnvelope command, CommandProcessingResult cached)
    {
        try
        {
            ConditionalValue<TerminalAdvisoryIntent> state = await StateManager
                .TryGetStateAsync<TerminalAdvisoryIntent>(TerminalAdvisoryIntent.GetStateKey(command.MessageId))
                .ConfigureAwait(false);
            if (state.HasValue && state.Value is { } intent && TerminalAdvisoryMatchesActor(intent)
                && string.Equals(command.AggregateIdentity.ActorId, Host.Id.GetId(), StringComparison.Ordinal)
                && string.Equals(intent.CorrelationId, cached.CorrelationId, StringComparison.Ordinal)
                && TerminalAdvisoryMatchesResult(intent, cached)
                && CreateCommandProcessingIdentity(command).Matches(intent.MessageId, intent.CausationId, intent.CommandType))
            {
                await TryRepairTerminalAdvisoryAsync(intent).ConfigureAwait(false);
            }
            else if (state.HasValue)
            {
                ReportRecoveryRetention(command.MessageId, "advisory_cached_result_conflict");
            }
        }
        catch (Exception exception) when (exception is not (OperationCanceledException or ActorStateRemediationException))
        {
            ReportRecoveryRetention(command.MessageId, "advisory_lookup_failed");
        }
    }

    private async Task TryRepairTerminalAdvisoryAsync(TerminalAdvisoryIntent intent, TerminalAdvisoryRepairIndex? committedIndex = null)
    {
        try
        {
            await RepairTerminalAdvisoryAsync(intent, committedIndex).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not (OperationCanceledException or ActorStateRemediationException))
        {
            ReportRecoveryRetention(intent.MessageId, "advisory_repair_failed");
        }
    }

    private async Task RepairTerminalAdvisoryAsync(TerminalAdvisoryIntent intent, TerminalAdvisoryRepairIndex? committedIndex)
    {
        if (!await CanRepairTerminalAdvisoryAsync(intent, intent.MessageId).ConfigureAwait(false))
        {
            return;
        }

        TerminalAdvisoryRepairIndex original = committedIndex ?? await ReadTerminalAdvisoryIndexAsync().ConfigureAwait(false);
        if (!original.Entries.Any(entry => entry is not null
            && string.Equals(entry.MessageId, intent.MessageId, StringComparison.Ordinal)))
        {
            return;
        }

        try
        {
            await commandStatusStore.WriteStatusAsync(intent.TenantId, intent.MessageId, intent.Status)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            ReportRecoveryRetention(intent.MessageId, "advisory_write_failed");
            await ArmTerminalAdvisoryReminderAsync(intent, original).ConfigureAwait(false);
            return;
        }

        TerminalAdvisoryRepairIndex updated = new([.. original.Entries.Where(entry => entry is null
            || !string.Equals(entry.MessageId, intent.MessageId, StringComparison.Ordinal))]);
        try
        {
            _ = await StateManager.TryRemoveStateAsync(TerminalAdvisoryIntent.GetStateKey(intent.MessageId))
                .ConfigureAwait(false);
            await StateManager.SetStateAsync(TerminalAdvisoryRepairIndex.StateKey, updated).ConfigureAwait(false);
            await StateManager.SaveStateAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            bool acknowledged = await InspectTerminalAdvisoryMutationFailureAsync(intent, original, updated, exception,
                removing: true).ConfigureAwait(false);
            if (exception is OperationCanceledException)
            {
                throw;
            }

            if (!acknowledged)
            {
                await ArmTerminalAdvisoryReminderAsync(intent, original).ConfigureAwait(false);
            }

            return;
        }

        if (!original.Entries.Any(entry => entry is not null && entry.MessageId == intent.MessageId
            && entry.ReminderArmedAt is not null))
        {
            return;
        }

        try
        {
            await UnregisterReminderAsync(TerminalAdvisoryIntent.GetReminderName(intent.MessageId))
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ReportRecoveryRetention(intent.MessageId, "advisory_reminder_cleanup_failed");
        }
    }

    private async Task<TerminalAdvisoryRepairIndex> ArmTerminalAdvisoryReminderAsync(TerminalAdvisoryIntent intent, TerminalAdvisoryRepairIndex index)
    {
        if (index.Entries.Any(entry => entry is not null && entry.MessageId == intent.MessageId
            && entry.ReminderArmedAt is not null))
        {
            return index;
        }

        try
        {
            (TimeSpan dueTime, TimeSpan period) = GetDrainReminderSchedule();
            _ = await RegisterReminderAsync(TerminalAdvisoryIntent.GetReminderName(intent.MessageId), null,
                dueTime, period).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ReportRecoveryRetention(intent.MessageId, "advisory_reminder_registration_failed");
            return index;
        }

        TerminalAdvisoryRepairIndex updated = new([.. index.Entries.Select(entry => entry is not null
            && entry.MessageId == intent.MessageId
                ? entry with { ReminderArmedAt = IdempotencyTimeProvider.GetUtcNow() } : entry!)]);
        try
        {
            await StateManager.SetStateAsync(TerminalAdvisoryRepairIndex.StateKey, updated).ConfigureAwait(false);
            await StateManager.SaveStateAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            bool committed = await InspectTerminalAdvisoryMutationFailureAsync(intent, index, updated, exception,
                removing: false).ConfigureAwait(false);
            if (exception is OperationCanceledException)
            {
                throw;
            }
            return committed ? updated : index;
        }
        return updated;
    }

    private async Task<bool> InspectTerminalAdvisoryMutationFailureAsync(TerminalAdvisoryIntent intent,
        TerminalAdvisoryRepairIndex original, TerminalAdvisoryRepairIndex updated,
        Exception exception, bool removing)
    {
        (bool discarded, string discardType) = await TryDiscardFailedBatchAsync().ConfigureAwait(false);
        if (!discarded)
        {
            throw CreateStateRemediationException(intent.CorrelationId, "TerminalAdvisoryRepair", exception,
                "DiscardTerminalAdvisoryRepairBatch", exception, discardType, false, "Unobserved");
        }

        try
        {
            ConditionalValue<TerminalAdvisoryIntent> observed = await StateManager
                .TryGetStateAsync<TerminalAdvisoryIntent>(TerminalAdvisoryIntent.GetStateKey(intent.MessageId))
                .ConfigureAwait(false);
            TerminalAdvisoryRepairIndex observedIndex = await ReadTerminalAdvisoryIndexAsync().ConfigureAwait(false);
            if ((removing ? !observed.HasValue : observed.HasValue && observed.Value == intent)
                && observedIndex.Entries.SequenceEqual(updated.Entries))
            {
                return true;
            }

            if (observed.HasValue && observed.Value == intent && observedIndex.Entries.SequenceEqual(original.Entries))
            {
                return false;
            }

            throw new InvalidOperationException("Terminal advisory repair durable intent/index is ambiguous.");
        }
        catch (Exception inspectionException)
        {
            _stateCacheUnsafe = true;
            throw CreateStateRemediationException(intent.CorrelationId, "TerminalAdvisoryRepair", exception,
                "InspectTerminalAdvisoryRepairCommit", inspectionException, discardType, true, "DurableInspectionFailed");
        }
    }

    private async Task<bool> TerminalAdvisoryStateMatchesAsync(TerminalAdvisoryIntent intent,
        TerminalAdvisoryRepairIndex expectedIndex)
    {
        ConditionalValue<TerminalAdvisoryIntent> state = await StateManager
            .TryGetStateAsync<TerminalAdvisoryIntent>(TerminalAdvisoryIntent.GetStateKey(intent.MessageId))
            .ConfigureAwait(false);
        TerminalAdvisoryRepairIndex index = await ReadTerminalAdvisoryIndexAsync().ConfigureAwait(false);
        return state.HasValue && state.Value == intent && index.Entries.SequenceEqual(expectedIndex.Entries);
    }

    private async Task<bool> TerminalAdvisoryPreCommitStateMatchesAsync(TerminalAdvisoryIntent intent,
        TerminalAdvisoryRepairIndex expectedIndex)
    {
        ConditionalValue<TerminalAdvisoryIntent> state = await StateManager
            .TryGetStateAsync<TerminalAdvisoryIntent>(TerminalAdvisoryIntent.GetStateKey(intent.MessageId))
            .ConfigureAwait(false);
        TerminalAdvisoryRepairIndex index = await ReadTerminalAdvisoryIndexAsync().ConfigureAwait(false);
        return !state.HasValue && index.Entries.SequenceEqual(expectedIndex.Entries.Where(entry => entry is null
            || !string.Equals(entry.MessageId, intent.MessageId, StringComparison.Ordinal)));
    }

    private static bool TerminalAdvisoryMatchesResult(TerminalAdvisoryIntent intent, CommandProcessingResult result)
        => intent.EventCount == result.EventCount
            && string.Equals(intent.CorrelationId, result.CorrelationId, StringComparison.Ordinal)
            && (result.Accepted
                ? intent.Status.Status == CommandStatus.Completed
                    && string.IsNullOrWhiteSpace(result.RejectionEventType)
                    && string.IsNullOrWhiteSpace(intent.Status.RejectionEventType)
                : intent.Status.Status == CommandStatus.Rejected
                    && HasDomainRejectionEvidence(result)
                    && (intent.Status.RejectionEventType is null
                        || string.Equals(intent.Status.RejectionEventType, result.RejectionEventType, StringComparison.Ordinal)));

    private static bool HasDomainRejectionEvidence(CommandProcessingResult result)
        => !result.Accepted && !result.BackpressureExceeded
            && !string.Equals(result.FailureReason, "BackpressureExceeded", StringComparison.Ordinal)
            && (!string.IsNullOrWhiteSpace(result.RejectionEventType)
                || string.Equals(result.FailureReason, "DomainRejected", StringComparison.Ordinal));

    private async Task<bool> CanRepairTerminalAdvisoryAsync(TerminalAdvisoryIntent intent, string indexedMessageId)
    {
        if (!string.Equals(intent.MessageId, indexedMessageId, StringComparison.Ordinal) || !TerminalAdvisoryMatchesActor(intent))
        {
            ReportRecoveryRetention(indexedMessageId, "advisory_key_or_status_identity");
            return false;
        }
        ConditionalValue<IdempotencyRecord> cached = await StateManager
            .TryGetStateAsync<IdempotencyRecord>(IdempotencyChecker.GetRecordKey(indexedMessageId)).ConfigureAwait(false);
        if (cached.HasValue)
        {
            IdempotencyRecord record = cached.Value;
            if (record.Disposition != IdempotencyRecordDisposition.Terminal
                || !new CommandProcessingIdentity(intent.MessageId, intent.CausationId, intent.CommandType)
                    .Matches(record.MessageId, record.CausationId, record.CommandType)
                || !TerminalAdvisoryMatchesResult(intent, record.ToResult()))
            {
                ReportRecoveryRetention(indexedMessageId, "advisory_terminal_outcome_conflict");
                return false;
            }
        }
        return true;
    }

    private void ReportRecoveryRetention(string? owner, string reason)
    {
        static string SafeOwner(string? value)
            => string.IsNullOrWhiteSpace(value) ? "(none)" : new string(value.Take(128)
                .Select(character => char.IsAsciiLetterOrDigit(character) || character is '-' or ':' or '_' ? character : '?').ToArray());
        AggregateActorRecoveryLog.Retained(logger, SafeOwner(Host.Id.GetId()), SafeOwner(owner), reason);
    }

    private async Task<int> ReadRecoveryScanOffsetAsync(string key, int count)
    {
        ConditionalValue<RecoveryScanProgress> stored = await StateManager.TryGetStateAsync<RecoveryScanProgress>(key).ConfigureAwait(false);
        return count == 0 || !stored.HasValue || stored.Value.NextOffset < 0 ? 0 : stored.Value.NextOffset % count;
    }

    private async Task PersistRecoveryScanOffsetAsync(string key, int originalOffset, int nextOffset)
    {
        if (originalOffset == nextOffset) { return; }
        ConditionalValue<RecoveryScanProgress> original = await StateManager.TryGetStateAsync<RecoveryScanProgress>(key).ConfigureAwait(false);
        var updated = new RecoveryScanProgress(nextOffset);
        try
        {
            await StateManager.SetStateAsync(key, updated).ConfigureAwait(false);
            await StateManager.SaveStateAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            (bool discarded, string discardType) = await TryDiscardFailedBatchAsync().ConfigureAwait(false);
            if (!discarded)
            {
                throw CreateStateRemediationException(key, "RecoveryScan", exception, "DiscardRecoveryScanBatch",
                    exception, discardType, false, "Unobserved");
            }
            try
            {
                ConditionalValue<RecoveryScanProgress> observed = await StateManager.TryGetStateAsync<RecoveryScanProgress>(key).ConfigureAwait(false);
                if (observed.HasValue && observed.Value == updated
                    || observed.HasValue == original.HasValue && (!observed.HasValue || observed.Value == original.Value))
                {
                    if (exception is OperationCanceledException) { throw; }
                    return;
                }
                throw new InvalidOperationException("Recovery scan durable continuation is ambiguous.");
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception inspectionException)
            {
                _stateCacheUnsafe = true;
                throw CreateStateRemediationException(key, "RecoveryScan", exception, "InspectRecoveryScanCommit",
                    inspectionException, discardType, true, "DurableInspectionFailed");
            }
        }
    }

    private async Task RearmTerminalAdvisoriesAsync()
    {
        TerminalAdvisoryRepairIndex index = await ReadTerminalAdvisoryIndexAsync().ConfigureAwait(false);
        int start = await ReadRecoveryScanOffsetAsync(RecoveryScanProgress.AdvisoryKey, index.Entries.Count).ConfigureAwait(false);
        int next = start;
        int probes = 0;
        int rearmed = 0;
        for (int visited = 0; visited < index.Entries.Count && rearmed < MaxActivationRearmEntries; visited++)
        {
            int position = (int)(((long)start + visited) % index.Entries.Count);
            TerminalAdvisoryRepairEntry? entry = index.Entries[position];
            if (entry?.ReminderArmedAt is not null)
            {
                next = (position + 1) % index.Entries.Count;
                continue;
            }
            if (probes >= MaxActivationProbeEntries) { break; }
            probes++;
            next = (position + 1) % index.Entries.Count;
            if (entry is null) { ReportRecoveryRetention(null, "advisory_index_identity"); continue; }
            try
            {
                ConditionalValue<TerminalAdvisoryIntent> state = await StateManager
                    .TryGetStateAsync<TerminalAdvisoryIntent>(TerminalAdvisoryIntent.GetStateKey(entry.MessageId)).ConfigureAwait(false);
                if (!state.HasValue || state.Value is not { } intent)
                {
                    ReportRecoveryRetention(entry.MessageId, "advisory_intent_missing");
                    continue;
                }
                if (!await CanRepairTerminalAdvisoryAsync(intent, entry.MessageId).ConfigureAwait(false)) { continue; }
                index = await ArmTerminalAdvisoryReminderAsync(intent, index).ConfigureAwait(false);
                if (index.Entries.Any(candidate => candidate is not null && candidate.MessageId == entry.MessageId
                    && candidate.ReminderArmedAt is not null)) { rearmed++; }
            }
            catch (Exception exception) when (exception is not (OperationCanceledException or ActorStateRemediationException)
                && !_stateCacheUnsafe)
            {
                ReportRecoveryRetention(entry.MessageId, "advisory_owner_read_or_rearm_failed");
            }
        }
        await PersistRecoveryScanOffsetAsync(RecoveryScanProgress.AdvisoryKey, start, next).ConfigureAwait(false);
    }
}
