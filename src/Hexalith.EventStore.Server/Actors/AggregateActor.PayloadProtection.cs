using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.PayloadProtection;
using Hexalith.EventStore.Server.Events;

namespace Hexalith.EventStore.Server.Actors;

public partial class AggregateActor
{
    private Task? _unfinishedPayloadProtectionIo;

    private void CheckPayloadProtectionIoReady()
    {
        if (_unfinishedPayloadProtectionIo is { IsCompleted: false })
        { throw new PayloadProtectionRecoveryException(new InvalidOperationException("ProtectionCompletionIoPending")); }
        if (_unfinishedPayloadProtectionIo is not null)
        {
            _ = _unfinishedPayloadProtectionIo.Exception;
            _unfinishedPayloadProtectionIo = null;
        }
    }

    private async Task<T> PayloadProtectionIoAsync<T>(Func<Task<T>> operation, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        CheckPayloadProtectionIoReady();
        Task<T> pending = operation();
        _unfinishedPayloadProtectionIo = pending;
        try { return await pending.WaitAsync(token).ConfigureAwait(false); }
        finally
        {
            if (pending.IsCompleted && ReferenceEquals(_unfinishedPayloadProtectionIo, pending))
            { _ = pending.Exception; _unfinishedPayloadProtectionIo = null; }
        }
    }

    private async Task PayloadProtectionIoAsync(Func<Task> operation, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        CheckPayloadProtectionIoReady();
        Task pending = operation();
        _unfinishedPayloadProtectionIo = pending;
        try { await pending.WaitAsync(token).ConfigureAwait(false); }
        finally
        {
            if (pending.IsCompleted && ReferenceEquals(_unfinishedPayloadProtectionIo, pending))
            { _ = pending.Exception; _unfinishedPayloadProtectionIo = null; }
        }
    }

    /// <summary>Quiesces original state/scheduler I/O before Dapr's automatic save can start.</summary>
    protected override async Task OnPostActorMethodAsync(ActorMethodContext actorMethodContext)
    {
        if (_unfinishedPayloadProtectionIo is { } pending) { await pending.ConfigureAwait(false); }
        await base.OnPostActorMethodAsync(actorMethodContext).ConfigureAwait(false);
    }

    /// <summary>Quiesces unknown original I/O before Dapr failure cleanup releases this actor turn.</summary>
    protected override async Task OnActorMethodFailedAsync(ActorMethodContext actorMethodContext, Exception exception)
    {
        if (_unfinishedPayloadProtectionIo is { } pending)
        {
            try { await pending.ConfigureAwait(false); }
            catch (Exception) { /* Preserve the original failure after the operation quiesces. */ }
        }
        await base.OnActorMethodFailedAsync(actorMethodContext, exception).ConfigureAwait(false);
    }

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
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30), IdempotencyTimeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, cancellationToken);
        await PayloadProtectionIoAsync(() => RegisterReminderAsync(PayloadProtectionCompletionJournal.ReminderName, null,
            TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30)), linked.Token).ConfigureAwait(false);
    }

    private async Task RearmPayloadProtectionCompletionReminderAsync()
    {
        if (payloadProtectionService is not InteractionPayloadProtectionService) { return; }
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30), IdempotencyTimeProvider);
        var found = await PayloadProtectionIoAsync(() => StateManager.TryGetStateAsync<PayloadProtectionCompletionJournal>(
            PayloadProtectionCompletionJournal.StateName, timeout.Token), timeout.Token).ConfigureAwait(false);
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
            await PayloadProtectionIoAsync(() => EnsureStateCacheBarrierAsync(correlationId: null, activity: null), token).ConfigureAwait(false);
            var found = await PayloadProtectionIoAsync(() => StateManager.TryGetStateAsync<PayloadProtectionCompletionJournal>(
                PayloadProtectionCompletionJournal.StateName, token), token).ConfigureAwait(false);
            if (found.HasValue && found.Value is not { Completed: true })
            {
                await RecoverPayloadProtectionCompletionsAsync(GetAggregateIdentityFromActorId(), token).ConfigureAwait(false);
                await PayloadProtectionIoAsync(() => StateManager.SaveStateAsync(token), token).ConfigureAwait(false);
            }
            await PayloadProtectionIoAsync(() => UnregisterReminderAsync(PayloadProtectionCompletionJournal.ReminderName), token).ConfigureAwait(false);
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
            ConditionalValue<PayloadProtectionCompletionJournal> found = await PayloadProtectionIoAsync(() => StateManager
                .TryGetStateAsync<PayloadProtectionCompletionJournal>(PayloadProtectionCompletionJournal.StateName, token), token).ConfigureAwait(false);
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
                    ConditionalValue<EventEnvelope> source = await PayloadProtectionIoAsync(() => StateManager.TryGetStateAsync<EventEnvelope>(
                        $"{identity.EventStreamKeyPrefix}{context.Occurrence.RecordSequence}", token), token).ConfigureAwait(false);
                    if (!source.HasValue || source.Value.TenantId != identity.TenantId || source.Value.Domain != identity.Domain
                        || source.Value.AggregateId != identity.AggregateId || (ulong)source.Value.SequenceNumber != context.Occurrence.RecordSequence
                        || source.Value.EventTypeName != context.Occurrence.PayloadTypeId || source.Value.ApplicationPayloadDigest is null)
                    { throw new InvalidOperationException("ProtectionCompletionSourceMismatch"); }
                    metadata = EventStorePayloadProtectionMetadataCarrier.Read(source.Value.Extensions);
                    EventLogicalDigest.RequireMatching(source.Value, source.Value.SerializationFormat, source.Value.Payload);
                }
                else if (context.Occurrence.PayloadKind == PayloadProtectionPayloadKind.Snapshot)
                {
                    ConditionalValue<SnapshotRecord> source = await PayloadProtectionIoAsync(() => StateManager.TryGetStateAsync<SnapshotRecord>(
                        identity.SnapshotKey, token), token).ConfigureAwait(false);
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
                await PayloadProtectionIoAsync(() => payloadProtectionService.CompletePayloadProtectionAsync(context,
                    PayloadProtectionPersistenceOutcome.Persisted, token), token).ConfigureAwait(false);
            }
            // Retain originals. The caller's next atomic save records completion; a crash before
            // that save repeats exact idempotent lookup/completion, never a new encryption identity.
            await PayloadProtectionIoAsync(() => StateManager.SetStateAsync(PayloadProtectionCompletionJournal.StateName,
                journal with { Completed = true }, token), token).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not PayloadProtectionRecoveryException)
        {
            throw new PayloadProtectionRecoveryException(exception);
        }
    }
}
