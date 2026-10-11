using System.Net;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Effects;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Processes;
using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Contracts.Processes;
using Hexalith.EventStore.Contracts.Streams;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.DomainService;

/// <summary>Processes bounded source pages under a durable source fence and target effect receipts.</summary>
public sealed class CheckpointedProcessRunner(
    IEventStoreGatewayClient gateway,
    IProcessTranslator translator,
    IProcessDelegationProvider delegation,
    ITrustedEffectSubmitter submitter,
    ProcessCheckpointStore checkpoints,
    IOptions<EventStoreProcessOptions> options,
    TimeProvider? time = null)
{
    private readonly IEventStoreGatewayClient _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
    private readonly IProcessTranslator _translator = translator ?? throw new ArgumentNullException(nameof(translator));
    private readonly IProcessDelegationProvider _delegation = delegation ?? throw new ArgumentNullException(nameof(delegation));
    private readonly ITrustedEffectSubmitter _submitter = submitter ?? throw new ArgumentNullException(nameof(submitter));
    private readonly ProcessCheckpointStore _checkpoints = checkpoints ?? throw new ArgumentNullException(nameof(checkpoints));
    private readonly EventStoreProcessOptions _options = (options ?? throw new ArgumentNullException(nameof(options))).Value;
    private readonly TimeProvider _time = time ?? TimeProvider.System;

    /// <summary>Runs at most one source page; a full page remains Incomplete for the next pass.</summary>
    public Task<ProcessRunResult> RunAsync(ProcessSource source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        return RunWithSweepAsync(source, null, cancellationToken);
    }

    /// <summary>Runs a source within a currently owned recovery pass.</summary>
    internal async Task<ProcessRunResult> RunWithSweepAsync(ProcessSource source, ProcessSweepState? sweepOwner, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        source.Validate();
        if (!await _checkpoints.EnsureCandidateAsync(source, sweepOwner, cancellationToken).ConfigureAwait(false))
        {
            ProcessSourceState pending = await WithDeadline(ct => _checkpoints.GetAsync(source, ct), cancellationToken).ConfigureAwait(false);
            return new ProcessRunResult(pending.Checkpoint, ProcessSourceStatus.Incomplete, 0, "candidate-publication");
        }
        ProcessSourceState? owned = await WithDeadline(ct => _checkpoints.AcquireAsync(source, ct), cancellationToken).ConfigureAwait(false);
        if (owned is null)
        {
            // A release can occur between failed acquisition and inspection. No unprocessed source is resolved.
            ProcessSourceState current = await WithDeadline(ct => _checkpoints.GetAsync(source, ct), cancellationToken).ConfigureAwait(false);
            return new ProcessRunResult(current.Checkpoint,
                current.Status == ProcessSourceStatus.Active ? ProcessSourceStatus.Incomplete : current.Status,
                0,
                current.ReasonCode ?? "source-busy");
        }

        try
        {
            if (owned.Status is ProcessSourceStatus.Parked or ProcessSourceStatus.Quarantined)
            {
                return Result(owned, 0);
            }

            if (owned.Attempts >= _options.RetryLimit)
            {
                owned = await FailAsync(owned, owned.ReasonCode ?? "retry-exhausted", owned.FailureSequence, false, sweepOwner, cancellationToken).ConfigureAwait(false);
                return Result(owned, 0);
            }

            StreamReadRequest request = new(source.Tenant, source.Domain, source.Aggregate, owned.Checkpoint,
                PageSize: Math.Min(Math.Min(Math.Min(_options.PageSize, _options.MaxEnvelopesPerRun),
                    Math.Max(1, _options.MaxPageBytes / _options.MaxEventBytes)),
                    Math.Max(1, _options.MaxCommandBytesPerPage / _options.MaxCommandBytesPerEnvelope)));
            StreamReadPage page;
            try
            {
                page = await WithReadDeadline(ct => _gateway.ReadWorkloadStreamAsync(request, ct), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (IsOperational(exception, cancellationToken))
            {
                bool corrupt = exception is JsonException || exception is EventStoreGatewayException gatewayException
                    && (gatewayException.ReasonCode is StreamReplayReasonCodes.MissingEvent or StreamReplayReasonCodes.CorruptEvent
                        || gatewayException.Code is StreamReplayReasonCodes.MissingEvent or StreamReplayReasonCodes.CorruptEvent);
                string reason = corrupt ? "invalid-evidence" : exception is ProcessReadAdmissionException ? "read-worker-exhausted" : "source-read";
                owned = await FailAsync(owned, reason, corrupt ? null : owned.FailureSequence, corrupt, sweepOwner, cancellationToken).ConfigureAwait(false);
                return Result(owned, 0);
            }

            try
            {
                ValidatePage(request, page);
            }
            catch (Exception exception) when (IsOperational(exception, cancellationToken))
            {
                owned = await FailAsync(owned, "invalid-evidence", null, true, sweepOwner, cancellationToken).ConfigureAwait(false);
                return Result(owned, 0);
            }

            if (page.Events.Count == 0)
            {
                if (owned.FailureSequence is { } failedSequence && failedSequence > owned.Checkpoint)
                {
                    owned = await FailAsync(owned, "invalid-evidence", failedSequence, true, sweepOwner, cancellationToken).ConfigureAwait(false);
                    return Result(owned, 0);
                }
                owned = await WithDeadline(ct => _checkpoints.UpdateOwnedAsync(owned, state => state with
                {
                    Status = ProcessSourceStatus.Active,
                    Attempts = 0,
                    ReasonCode = null,
                    FailureSequence = null,
                }, ct, sweepOwner), cancellationToken).ConfigureAwait(false);
                return Result(owned, 0);
            }

            // A validated head clears failures from an earlier read or envelope. A
            // replay of the same failed envelope keeps its retry budget intact.
            bool newEvidence = owned.FailureSequence != page.Events[0].SequenceNumber;
            owned = await WithDeadline(ct => _checkpoints.UpdateOwnedAsync(owned, state => state with
            {
                Status = ProcessSourceStatus.Incomplete,
                Attempts = newEvidence ? 0 : state.Attempts,
                ReasonCode = null,
                FailureSequence = newEvidence ? null : state.FailureSequence,
            }, ct, sweepOwner), cancellationToken).ConfigureAwait(false);

            int processed = 0;
            int pageCommandBytes = 0;
            long serviceStarted = _time.GetTimestamp();
            foreach (StreamReadEvent streamEvent in page.Events)
            {
                if (processed > 0 && _time.GetElapsedTime(serviceStarted) >= _options.SourceServiceQuantum)
                {
                    return Result(owned, processed);
                }

                owned = await WithDeadline(ct => _checkpoints.RenewAsync(owned, ct), cancellationToken).ConfigureAwait(false);
                IReadOnlyList<ProcessEffect> effects;
                try
                {
                    effects = await CaptureEnvelopeAsync(source, streamEvent, cancellationToken).ConfigureAwait(false);
                }
                catch (ProcessReadAdmissionException)
                {
                    owned = await FailAsync(owned, "read-worker-exhausted", streamEvent.SequenceNumber, false, sweepOwner, cancellationToken).ConfigureAwait(false);
                    return Result(owned, processed);
                }
                catch (Exception exception) when (exception is TimeoutException || exception is OperationCanceledException && !cancellationToken.IsCancellationRequested)
                {
                    owned = await FailAsync(owned, "translation-timeout", streamEvent.SequenceNumber, false, sweepOwner, cancellationToken).ConfigureAwait(false);
                    return Result(owned, processed);
                }
                catch (Exception exception) when (IsOperational(exception, cancellationToken))
                {
                    owned = await FailAsync(owned, "invalid-evidence", streamEvent.SequenceNumber, true, sweepOwner, cancellationToken).ConfigureAwait(false);
                    return Result(owned, processed);
                }

                try
                {
                    pageCommandBytes = checked(pageCommandBytes + effects.Sum(static effect => checked(effect.CommandPayload.Length + Encoding.UTF8.GetByteCount(effect.CommandType))));
                    if (pageCommandBytes > _options.MaxCommandBytesPerPage)
                        throw new InvalidOperationException("Process translation exceeds page command byte limit.");
                }
                catch (Exception exception) when (exception is OverflowException or InvalidOperationException)
                {
                    owned = await FailAsync(owned, "invalid-evidence", streamEvent.SequenceNumber, true, sweepOwner, cancellationToken).ConfigureAwait(false);
                    return Result(owned, processed);
                }

                foreach (ProcessEffect effect in effects)
                {
                    owned = await WithDeadline(ct => _checkpoints.RenewAsync(owned, ct), cancellationToken).ConfigureAwait(false);
                    EffectIdentity identity = new(source.Tenant, source.Domain, source.Aggregate, streamEvent.SequenceNumber,
                        effect.Kind, effect.TargetDomain, effect.TargetAggregate, effect.Ordinal);
                    string messageId = EffectIdentityCodec.ComputeMessageId(identity);
                    TrustedEffectSubmission submission = new(identity, effect.CommandType, effect.CommandPayload, messageId, messageId);
                    TrustedEffectContext context;
                    try
                    {
                        context = await WithDeadline(ct => _delegation.CreateAsync(submission, streamEvent.MessageId, ct), cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception exception) when (IsOperational(exception, cancellationToken))
                    {
                        owned = await FailAsync(owned, "delegation-unavailable", streamEvent.SequenceNumber, false, sweepOwner, cancellationToken).ConfigureAwait(false);
                        return Result(owned, processed);
                    }

                    if (context is null)
                    {
                        owned = await FailAsync(owned, "delegation-unavailable", streamEvent.SequenceNumber, false, sweepOwner, cancellationToken).ConfigureAwait(false);
                        return Result(owned, processed);
                    }

                    try
                    {
                        // Delegation may consume almost the whole operation deadline. Fence the
                        // source again immediately before handing the effect to the target.
                        owned = await WithDeadline(ct => _checkpoints.RenewAsync(owned, ct), cancellationToken).ConfigureAwait(false);
                        TrustedEffectResult? receipt = await WithDeadline(ct => _submitter.SubmitAsync(submission, context, ct), cancellationToken).ConfigureAwait(false);
                        if (receipt is null || receipt.EffectId != EffectIdentityCodec.ComputeEffectId(identity)
                            || !Enum.IsDefined(receipt.Disposition))
                        {
                            owned = await FailAsync(owned, "invalid-receipt", streamEvent.SequenceNumber, true, sweepOwner, cancellationToken).ConfigureAwait(false);
                            return Result(owned, processed);
                        }

                        // A near-deadline target call may have committed under the old
                        // lease. Renew before the checkpoint CAS; replay uses the receipt
                        // if ownership was lost in the meantime.
                        owned = await WithDeadline(ct => _checkpoints.RenewAsync(owned, ct), cancellationToken).ConfigureAwait(false);
                    }
                    catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.Conflict)
                    {
                        owned = await FailAsync(owned, "effect-conflict", streamEvent.SequenceNumber, true, sweepOwner, cancellationToken).ConfigureAwait(false);
                        return Result(owned, processed);
                    }
                    catch (Exception exception) when (IsOperational(exception, cancellationToken))
                    {
                        owned = await FailAsync(owned, "effect-unknown", streamEvent.SequenceNumber, false, sweepOwner, cancellationToken).ConfigureAwait(false);
                        return Result(owned, processed);
                    }
                }

                owned = await WithDeadline(ct => _checkpoints.UpdateOwnedAsync(owned, state => state with
                {
                    Checkpoint = streamEvent.SequenceNumber,
                    Status = ProcessSourceStatus.Incomplete,
                    Attempts = 0,
                    ReasonCode = null,
                    FailureSequence = null,
                }, ct, sweepOwner), cancellationToken).ConfigureAwait(false);
                processed++;
            }

            owned = await WithDeadline(ct => _checkpoints.UpdateOwnedAsync(owned, state => state with
            {
                Status = page.Metadata.IsTruncated ? ProcessSourceStatus.Incomplete : ProcessSourceStatus.Active,
                Attempts = 0,
                ReasonCode = null,
                FailureSequence = null,
            }, ct, sweepOwner), cancellationToken).ConfigureAwait(false);
            return Result(owned, processed);
        }
        finally
        {
            try
            {
                await WithDeadline(ct => _checkpoints.ReleaseAsync(owned, ct), CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // The durable lease expires; a later pass retries under a new fence.
            }
        }
    }

    private void ValidatePage(StreamReadRequest request, StreamReadPage page)
    {
        long last = StreamReadPageValidator.ValidateAndGetNextSequence(request, page);
        if ((!page.Metadata.IsTruncated && last != page.Metadata.LatestSequence)
            || page.Metadata.IsTruncated && page.Metadata.LatestSequence <= last)
        {
            throw new InvalidOperationException("Process page head metadata is inconsistent.");
        }

        long expected = request.FromSequence;
        int sourceBytes = 0;
        foreach (StreamReadEvent streamEvent in page.Events)
        {
            expected = checked(expected + 1);
            int serializedBytes = JsonSerializer.SerializeToUtf8Bytes(streamEvent).Length;
            sourceBytes = checked(sourceBytes + serializedBytes);
            if (streamEvent.SequenceNumber != expected || serializedBytes > _options.MaxEventBytes || sourceBytes > _options.MaxPageBytes)
            {
                throw new InvalidOperationException("Process page has a gap or exceeds byte limits.");
            }
        }
    }

    private async Task<IReadOnlyList<ProcessEffect>> CaptureEnvelopeAsync(ProcessSource source, StreamReadEvent streamEvent, CancellationToken cancellationToken)
    {
        IReadOnlyList<ProcessEffect> translated = await WithReadDeadline(ct => _translator.TranslateAsync(source, streamEvent, ct).AsTask(), cancellationToken).ConfigureAwait(false);
        if (translated is null || translated.Count > _options.MaxEffectsPerEnvelope) throw new InvalidOperationException("Process translation exceeds effect limits.");
        int envelopeBytes = 0;
        var ordinals = new HashSet<(string, string, string, long)>();
        var effects = new List<ProcessEffect>(translated.Count);
        foreach (ProcessEffect effect in translated)
        {
            if (effect is null || effect.CommandPayload is null || string.IsNullOrWhiteSpace(effect.CommandType)
                || !ordinals.Add((effect.Kind, effect.TargetDomain, effect.TargetAggregate, effect.Ordinal)))
            {
                throw new InvalidOperationException("Process translation contains invalid or duplicate effects.");
            }

            _ = EffectIdentityCodec.ComputeEffectId(new EffectIdentity(source.Tenant, source.Domain, source.Aggregate,
                streamEvent.SequenceNumber, effect.Kind, effect.TargetDomain, effect.TargetAggregate, effect.Ordinal));
            envelopeBytes = checked(envelopeBytes + effect.CommandPayload.Length + Encoding.UTF8.GetByteCount(effect.CommandType));
            if (envelopeBytes > _options.MaxCommandBytesPerEnvelope)
                throw new InvalidOperationException("Process translation exceeds command byte limits.");
            effects.Add(effect with { CommandPayload = [.. effect.CommandPayload] });
        }

        return effects;
    }

    private async Task<ProcessSourceState> FailAsync(ProcessSourceState owned, string reason, long? sequence, bool quarantine, ProcessSweepState? sweepOwner, CancellationToken cancellationToken)
        => await WithDeadline(ct => _checkpoints.UpdateOwnedAsync(owned, state => state with
        {
            Status = quarantine ? ProcessSourceStatus.Quarantined : state.Attempts + 1 >= _options.RetryLimit ? ProcessSourceStatus.Parked : ProcessSourceStatus.Retrying,
            Attempts = quarantine ? state.Attempts : state.Attempts + 1,
            ReasonCode = reason,
            FailureSequence = sequence,
        }, ct, sweepOwner), cancellationToken).ConfigureAwait(false);

    private static ProcessRunResult Result(ProcessSourceState state, int processed) => new(state.Checkpoint, state.Status, processed, state.ReasonCode);

    private async Task<T> WithDeadline<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_options.OperationTimeout);
        return await operation(deadline.Token).WaitAsync(_options.OperationTimeout, cancellationToken).ConfigureAwait(false);
    }

    private async Task WithDeadline(Func<CancellationToken, Task> operation, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_options.OperationTimeout);
        await operation(deadline.Token).WaitAsync(_options.OperationTimeout, cancellationToken).ConfigureAwait(false);
    }

    private Task<T> WithReadDeadline<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
        => ProcessReadDeadline.RunAsync(operation, _options.OperationTimeout, cancellationToken);

    private static bool IsOperational(Exception exception, CancellationToken cancellationToken)
        => exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested;
}
