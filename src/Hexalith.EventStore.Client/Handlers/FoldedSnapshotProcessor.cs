using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Serialization;
using Hexalith.EventStore.Contracts.Snapshots;

namespace Hexalith.EventStore.Client.Handlers;

/// <summary>Shares the bounded optional capture path across typed processors and convention aggregates.</summary>
/// <typeparam name="TState">The exact owner-declared aggregate state.</typeparam>
internal static class FoldedSnapshotProcessor<TState> where TState : class, new()
{
    /// <summary>Processes an ordinary command and optionally captures only its admitted event effects.</summary>
    internal static async Task<FoldedSnapshotProcessingResult> ProcessAsync(
        CommandEnvelope command, object? currentState, string? qualifiedProfileId,
        FoldedSnapshotCaptureLimits? qualifiedLimits,
        Func<DetachedStateCapture<TState>?> getSnapshotCapture,
        Func<FoldedSnapshotCaptureProfile<TState>?> getFoldedSnapshotCapture,
        Func<CommandEnvelope, TState?, CancellationToken, Task<DomainResult>> handleAsync,
        Func<ApplyMethodTable> getApplyMethods,
        EventPayloadEvolutionRegistry? evolution,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        DetachedStateCapture<TState>? snapshotCapture;
        try
        {
            snapshotCapture = getSnapshotCapture();
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw;
        }
        cancellationToken.ThrowIfCancellationRequested();
        ApplyMethodTable applyMethods = getApplyMethods();
        TState? typedState = DomainProcessorStateRehydrator.RehydrateState<TState>(
            currentState,
            applyMethods,
            cancellationToken, snapshotCapture, evolution);
        cancellationToken.ThrowIfCancellationRequested();
        FoldedSnapshotCaptureProfile<TState>? profile = null;
        TState? preCommandState = null;
        if (qualifiedProfileId is not null)
        {
            try
            {
                profile = getFoldedSnapshotCapture();
                if (profile is null || !string.Equals(profile.ProfileId, qualifiedProfileId, StringComparison.Ordinal)
                    || !Equals(profile.Limits, qualifiedLimits)
                    || !BoundedSnapshotJsonBuffer.CanSerialize(profile.MaximumStateJsonBytes,
                        EventStorePayloadSerialization.Options, profile.Limits.MaximumSerializerScratchBytes,
                        profile.Limits.MaximumWorkingStateGraphBytes))
                {
                    profile = null;
                }
                else if (typedState is not null)
                {
                    // The declared graph maximum was admitted when the immutable profile was built.
                    preCommandState = profile.StateCapture.Capture(typedState, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                profile = null;
                preCommandState = null;
            }
        }
        DomainResult result = await handleAsync(command, typedState, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (profile is null || result.IsNoOp)
        {
            return new FoldedSnapshotProcessingResult(result, null);
        }
        FoldedSnapshotCaptureProfile<TState> admittedProfile = profile;
        TState? admittedState = preCommandState;
        long preSequence = currentState switch
        {
            DomainServiceCurrentState dscs => dscs.CurrentSequence,
            JsonElement { ValueKind: JsonValueKind.Object } json
                when json.TryGetProperty("events", out _)
                    && json.TryGetProperty("currentSequence", out JsonElement sequence)
                    && sequence.ValueKind == JsonValueKind.Number
                    && sequence.TryGetInt64(out long value) => value,
            _ => 0,
        };
        return new FoldedSnapshotProcessingResult(result,
            (wireEvents, token) => CaptureFoldedState(command, preSequence, admittedState, admittedProfile, wireEvents, applyMethods, evolution, token));
    }

    private static string CaptureFoldedState(
        CommandEnvelope command,
        long preSequence,
        TState? preCommandState,
        FoldedSnapshotCaptureProfile<TState> profile,
        IReadOnlyList<DomainServiceWireEvent> wireEvents,
        ApplyMethodTable applyMethods,
        EventPayloadEvolutionRegistry? evolution,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (wireEvents.Count is < 1 || wireEvents.Count > profile.MaximumEventCount)
        {
            throw new InvalidOperationException("SnapshotCaptureLimitExceeded");
        }
        long totalBytes = 0;
        foreach (DomainServiceWireEvent item in wireEvents)
        {
            totalBytes = checked(totalBytes + item.Payload.Length);
            if (totalBytes > profile.MaximumEventBytes
                || !string.Equals(item.SerializationFormat, "json", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("SnapshotCaptureLimitExceeded");
            }
        }
        using var replayInput = new LegacyCommandReplayInput(cancellationToken, profile.Limits);
        replayInput.AdmitCaptureEnvelopes(wireEvents.Count);
        var detached = new EventEnvelope[wireEvents.Count];
        for (int index = 0; index < wireEvents.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DomainServiceWireEvent item = wireEvents[index];
            var metadata = new EventMetadata(
                command.MessageId, command.AggregateId, command.Domain, command.TenantId, command.Domain,
                checked(preSequence + index + 1), 0, DateTimeOffset.UtcNow, command.CorrelationId,
                command.CausationId ?? command.CorrelationId, command.UserId, "capture", item.EventTypeName,
                item.MetadataVersion ?? 1, item.SerializationFormat)
            { PayloadVersion = item.PayloadVersion };
            // The router already supplied private once-admitted bytes. Shared replay
            // admits and makes the final private envelope copy before payload parsing.
            detached[index] = new EventEnvelope(metadata, item.Payload, null);
        }
        var input = new DomainServiceCurrentState(preCommandState, detached, preSequence,
            checked(preSequence + detached.Length));
        TState folded = DomainProcessorStateRehydrator.RehydrateState<TState>(input,
            applyMethods, cancellationToken, evolution: evolution,
            admittedInput: replayInput)
            ?? throw new InvalidOperationException("SnapshotCaptureStateInvalid");
        using var output = new BoundedSnapshotJsonStream(profile.MaximumStateJsonBytes,
            EventStorePayloadSerialization.Options, profile.Limits.MaximumSerializerScratchBytes,
            profile.Limits.MaximumWorkingStateGraphBytes);
        BoundedSnapshotJsonBuffer.Serialize(output, folded, typeof(TState), EventStorePayloadSerialization.Options,
            profile.MaximumStateJsonBytes, profile.Limits.MaximumSerializerScratchBytes,
            profile.Limits.MaximumWorkingStateGraphBytes);
        byte[] bytes = output.ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { MaxDepth = profile.MaximumDepth });
        int nodes = 0;
        while (reader.Read())
        {
            if (++nodes > profile.MaximumNodeCount)
            {
                throw new InvalidOperationException("SnapshotCaptureLimitExceeded");
            }
        }
        if (checked(16L * bytes.Length + 64L * nodes) > profile.Limits.MaximumDomainParsedStateGraphBytes)
        {
            throw new InvalidOperationException("SnapshotCaptureLimitExceeded");
        }
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement state = document.RootElement;
        if (state.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("SnapshotCaptureStateInvalid");
        }
        return Encoding.UTF8.GetString(bytes);
    }

}
