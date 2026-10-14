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
            return null;
        }

        try
        {
            IReadOnlyList<EventEnvelope> events = await LoadPersistedEventsRangeAsync(identity, start, end)
                .ConfigureAwait(false);
            return HasVerifiedEventRange(identity, events, start, end, count, correlationId, causationId)
                ? events : null;
        }
        catch (Exception exception) when (exception is not (OperationCanceledException or DrainStateStoreException))
        {
            logger.LogWarning(exception,
                "Persisted recovery range could not be verified; evidence retained: CorrelationId={CorrelationId}",
                correlationId);
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
                    && intent.EndSequence <= int.MaxValue
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
                && intent.EventCount == cached.EventCount
                && CreateCommandProcessingIdentity(command).Matches(intent.MessageId, intent.CausationId, intent.CommandType))
            {
                await TryRepairTerminalAdvisoryAsync(intent).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is not (OperationCanceledException or ActorStateRemediationException))
        {
            logger.LogWarning(exception, "Pending terminal advisory lookup failed: MessageId={MessageId}", command.MessageId);
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
            logger.LogWarning(exception, "Terminal advisory repair remains pending: MessageId={MessageId}", intent.MessageId);
        }
    }

    private async Task RepairTerminalAdvisoryAsync(TerminalAdvisoryIntent intent, TerminalAdvisoryRepairIndex? committedIndex)
    {
        if (!TerminalAdvisoryMatchesActor(intent))
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
        catch (Exception exception)
        {
            logger.LogWarning(exception,
                "Terminal advisory remains pending: MessageId={MessageId}, CorrelationId={CorrelationId}",
                intent.MessageId, intent.CorrelationId);
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
            logger.LogWarning(exception, "Terminal advisory reminder cleanup failed: MessageId={MessageId}", intent.MessageId);
        }
    }

    private async Task ArmTerminalAdvisoryReminderAsync(TerminalAdvisoryIntent intent, TerminalAdvisoryRepairIndex index)
    {
        if (index.Entries.Any(entry => entry is not null && entry.MessageId == intent.MessageId
            && entry.ReminderArmedAt is not null))
        {
            return;
        }

        try
        {
            (TimeSpan dueTime, TimeSpan period) = GetDrainReminderSchedule();
            _ = await RegisterReminderAsync(TerminalAdvisoryIntent.GetReminderName(intent.MessageId), null,
                dueTime, period).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Terminal advisory reminder remains unarmed: MessageId={MessageId}", intent.MessageId);
            return;
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
            await InspectTerminalAdvisoryMutationFailureAsync(intent, index, updated, exception,
                removing: false).ConfigureAwait(false);
            if (exception is OperationCanceledException)
            {
                throw;
            }
        }
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

    private async Task RearmTerminalAdvisoriesAsync()
    {
        TerminalAdvisoryRepairIndex index = await ReadTerminalAdvisoryIndexAsync().ConfigureAwait(false);
        int probes = 0;
        int rearmed = 0;
        foreach (TerminalAdvisoryRepairEntry? entry in index.Entries)
        {
            if (entry is null || entry.ReminderArmedAt is not null)
            {
                continue;
            }

            if (probes++ >= MaxActivationProbeEntries || rearmed >= MaxActivationRearmEntries)
            {
                break;
            }

            ConditionalValue<TerminalAdvisoryIntent> state = await StateManager
                .TryGetStateAsync<TerminalAdvisoryIntent>(TerminalAdvisoryIntent.GetStateKey(entry.MessageId))
                .ConfigureAwait(false);
            if (!state.HasValue || state.Value is not { } intent || !TerminalAdvisoryMatchesActor(intent))
            {
                continue;
            }

            await ArmTerminalAdvisoryReminderAsync(intent, index).ConfigureAwait(false);
            index = await ReadTerminalAdvisoryIndexAsync().ConfigureAwait(false);
            if (index.Entries.Any(candidate => candidate is not null && candidate.MessageId == entry.MessageId
                && candidate.ReminderArmedAt is not null))
            {
                rearmed++;
            }
        }
    }
}
