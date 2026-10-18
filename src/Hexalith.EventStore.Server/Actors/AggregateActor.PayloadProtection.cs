using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.PayloadProtection;
using Hexalith.EventStore.Server.Events;

namespace Hexalith.EventStore.Server.Actors;

public partial class AggregateActor
{
    private async Task StagePayloadProtectionCompletionsAsync(AggregateIdentity identity,
        IReadOnlyCollection<PayloadProtectionCompletionContext> originals, CancellationToken cancellationToken)
    {
        if (originals.Count == 0) { return; }
        if (originals.Count > 4096 || originals.Any(context => context.Identity != identity))
        { throw new InvalidOperationException("ProtectionCompletionJournalMismatch"); }
        await StateManager.SetStateAsync(PayloadProtectionCompletionJournal.StateName,
            new PayloadProtectionCompletionJournal([.. originals]), cancellationToken).ConfigureAwait(false);
        // Arm before the source commit. A crash after commit then needs no new command;
        // a crash before commit leaves a harmless reminder that finds no journal.
        await ArmPayloadProtectionCompletionReminderAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task ArmPayloadProtectionCompletionReminderAsync(CancellationToken cancellationToken)
    {
        if (payloadProtectionService is not InteractionPayloadProtectionService) { return; }
        _ = await RegisterReminderAsync(PayloadProtectionCompletionJournal.ReminderName, null,
            TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30)).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task RearmPayloadProtectionCompletionReminderAsync()
    {
        if (payloadProtectionService is not InteractionPayloadProtectionService) { return; }
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30), IdempotencyTimeProvider);
        var found = await StateManager.TryGetStateAsync<PayloadProtectionCompletionJournal>(
            PayloadProtectionCompletionJournal.StateName, timeout.Token).WaitAsync(timeout.Token).ConfigureAwait(false);
        if (found.HasValue && found.Value is { Completed: false })
        {
            await ArmPayloadProtectionCompletionReminderAsync(timeout.Token).ConfigureAwait(false);
        }
    }

    private async Task RecoverPayloadProtectionCompletionReminderAsync()
    {
        if (payloadProtectionService is not InteractionPayloadProtectionService)
        { throw new InvalidOperationException("ProtectionCompletionEngineUnavailable"); }
        // Every read, independent writer proof and completion commit shares one budget.
        // A failed/unknown save leaves the reminder armed and forces a fresh durable read.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30), IdempotencyTimeProvider);
        CancellationToken token = timeout.Token;
        try
        {
            await EnsureStateCacheBarrierAsync(correlationId: null, activity: null).WaitAsync(token).ConfigureAwait(false);
            var found = await StateManager.TryGetStateAsync<PayloadProtectionCompletionJournal>(
                PayloadProtectionCompletionJournal.StateName, token).WaitAsync(token).ConfigureAwait(false);
            if (found.HasValue && found.Value is not { Completed: true })
            {
                await RecoverPayloadProtectionCompletionsAsync(GetAggregateIdentityFromActorId(), token).ConfigureAwait(false);
                await StateManager.SaveStateAsync(token).WaitAsync(token).ConfigureAwait(false);
            }
            await UnregisterReminderAsync(PayloadProtectionCompletionJournal.ReminderName).WaitAsync(token).ConfigureAwait(false);
        }
        catch
        {
            _stateCacheUnsafe = true;
            throw;
        }
    }

    private async Task RecoverPayloadProtectionCompletionsAsync(AggregateIdentity identity, CancellationToken cancellationToken = default)
    {
        if (payloadProtectionService is not InteractionPayloadProtectionService) { return; }
        // Confirmed original recovery is independent of the abandoned command's cancellation,
        // while every read and owner call shares this one bounded recovery budget.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30), IdempotencyTimeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, cancellationToken);
        CancellationToken token = linked.Token;
        try
        {
            ConditionalValue<PayloadProtectionCompletionJournal> found = await StateManager
                .TryGetStateAsync<PayloadProtectionCompletionJournal>(PayloadProtectionCompletionJournal.StateName, token)
                .WaitAsync(token).ConfigureAwait(false);
            if (!found.HasValue || found.Value is { Completed: true }) { return; }
            if (found.Value is null) { throw new InvalidOperationException("ProtectionCompletionJournalMismatch"); }
            PayloadProtectionCompletionJournal journal = found.Value;
            if (journal.Originals is not { Length: > 0 and <= 4096 })
            { throw new InvalidOperationException("ProtectionCompletionJournalMismatch"); }
            foreach (PayloadProtectionCompletionContext context in journal.Originals)
            {
                if (context.Identity != identity || context.Occurrence.RecordSequence is 0 or > long.MaxValue)
                { throw new InvalidOperationException("ProtectionCompletionJournalMismatch"); }
                EventStorePayloadProtectionMetadata? metadata;
                if (context.Occurrence.PayloadKind == PayloadProtectionPayloadKind.Event)
                {
                    ConditionalValue<EventEnvelope> source = await StateManager.TryGetStateAsync<EventEnvelope>(
                        $"{identity.EventStreamKeyPrefix}{context.Occurrence.RecordSequence}", token).WaitAsync(token).ConfigureAwait(false);
                    if (!source.HasValue || source.Value.TenantId != identity.TenantId || source.Value.Domain != identity.Domain
                        || source.Value.AggregateId != identity.AggregateId || (ulong)source.Value.SequenceNumber != context.Occurrence.RecordSequence
                        || source.Value.EventTypeName != context.Occurrence.PayloadTypeId || source.Value.ApplicationPayloadDigest is null)
                    { throw new InvalidOperationException("ProtectionCompletionSourceMismatch"); }
                    metadata = EventStorePayloadProtectionMetadataCarrier.Read(source.Value.Extensions);
                    EventLogicalDigest.RequireMatching(source.Value, source.Value.SerializationFormat, source.Value.Payload);
                }
                else if (context.Occurrence.PayloadKind == PayloadProtectionPayloadKind.Snapshot)
                {
                    ConditionalValue<SnapshotRecord> source = await StateManager.TryGetStateAsync<SnapshotRecord>(
                        identity.SnapshotKey, token).WaitAsync(token).ConfigureAwait(false);
                    if (!source.HasValue || source.Value.TenantId != identity.TenantId || source.Value.Domain != identity.Domain
                        || source.Value.AggregateId != identity.AggregateId || (ulong)source.Value.SequenceNumber != context.Occurrence.RecordSequence)
                    { throw new InvalidOperationException("ProtectionCompletionSourceMismatch"); }
                    metadata = source.Value.ProtectionMetadata;
                }
                else { throw new InvalidOperationException("ProtectionCompletionJournalMismatch"); }
                if (metadata?.State != PayloadProtectionState.Protected || metadata.Scheme != "hexalith-pdenc-v2"
                    || metadata.KeyAlias != context.KeyReference)
                { throw new InvalidOperationException("ProtectionCompletionSourceMismatch"); }
                // This requests the original independently checked writer proof. Neither the
                // journal nor a successful local save supplies the registry's activation authority.
                await payloadProtectionService.CompletePayloadProtectionAsync(context,
                    PayloadProtectionPersistenceOutcome.Persisted, token).WaitAsync(token).ConfigureAwait(false);
            }
            // Retain originals. The caller's next atomic save records completion; a crash before
            // that save repeats exact idempotent lookup/completion, never a new encryption identity.
            await StateManager.SetStateAsync(PayloadProtectionCompletionJournal.StateName,
                journal with { Completed = true }, token).WaitAsync(token).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not PayloadProtectionRecoveryException)
        {
            throw new PayloadProtectionRecoveryException(exception);
        }
    }
}
