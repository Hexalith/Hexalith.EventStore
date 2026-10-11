
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Serialization;
using Hexalith.EventStore.Contracts.Snapshots;

namespace Hexalith.EventStore.Client.Handlers;
/// <summary>
/// Abstract base class for domain processors that provides typed state casting.
/// Safely converts the untyped <c>object?</c> state to <typeparamref name="TState"/>
/// and delegates to the typed <see cref="HandleAsync(CommandEnvelope, TState)"/> method.
/// When state arrives as a <see cref="JsonElement"/> (e.g., from JSON deserialization
/// of <c>object?</c> via DAPR service invocation), it is automatically deserialized
/// to <typeparamref name="TState"/>.
/// </summary>
/// <typeparam name="TState">The aggregate state type. Must be a reference type.</typeparam>
public abstract class DomainProcessorBase<TState> : IDomainProcessor, IAsyncDomainProcessor, IAsyncFoldedSnapshotProcessor
    where TState : class, new() {
    /// <summary>Gets an optional owner declaration that detaches known typed command snapshots.</summary>
    /// <remarks>Undeclared legacy state retains its existing typed-reference behavior.</remarks>
    protected virtual DetachedStateCapture<TState>? SnapshotCapture => null;

    /// <summary>Gets an optional deployment-owned qualification for the exact state and Apply profile.</summary>
    protected virtual FoldedSnapshotCaptureProfile<TState>? FoldedSnapshotCapture => null;

    /// <inheritdoc/>
    public bool IsQualified(Type stateType, string profileId, string parityEvidenceId,
        FoldedSnapshotCaptureLimits limits)
    {
        FoldedSnapshotCaptureProfile<TState>? profile = FoldedSnapshotCapture;
        return stateType == typeof(TState) && profile is not null
            && string.Equals(profile.ProfileId, profileId, StringComparison.Ordinal)
            && string.Equals(profile.ParityEvidenceId, parityEvidenceId, StringComparison.Ordinal)
            && Equals(profile.Limits, limits);
    }

    /// <inheritdoc/>
    public Task<DomainResult> ProcessAsync(CommandEnvelope command, object? currentState)
        => ProcessAsync(command, currentState, CancellationToken.None);

    /// <inheritdoc/>
    /// <remarks>The compatibility HandleAsync overload observes cancellation at its boundaries.</remarks>
    public async Task<DomainResult> ProcessAsync(CommandEnvelope command, object? currentState, CancellationToken cancellationToken)
        => (await ProcessCoreAsync(command, currentState, null, null, cancellationToken).ConfigureAwait(false)).Result;

    /// <inheritdoc/>
    public Task<FoldedSnapshotProcessingResult> ProcessForSnapshotAsync(
        DomainServiceRequest request, string qualifiedProfileId, FoldedSnapshotCaptureLimits limits,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ProcessCoreAsync(request.Command, request.CurrentState,
            request.IncludePostCommandState ? qualifiedProfileId : null, limits, cancellationToken);
    }

    private async Task<FoldedSnapshotProcessingResult> ProcessCoreAsync(
        CommandEnvelope command, object? currentState, string? qualifiedProfileId,
        FoldedSnapshotCaptureLimits? qualifiedLimits, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        DetachedStateCapture<TState>? snapshotCapture;
        try { snapshotCapture = SnapshotCapture; }
        catch (Exception) when (cancellationToken.IsCancellationRequested) {
            cancellationToken.ThrowIfCancellationRequested();
            throw;
        }
        cancellationToken.ThrowIfCancellationRequested();
        TState? typedState = DomainProcessorStateRehydrator.RehydrateState<TState>(
            currentState,
            DomainProcessorStateRehydrator.DiscoverApplyMethods(typeof(TState)),
            cancellationToken, snapshotCapture);
        cancellationToken.ThrowIfCancellationRequested();
        FoldedSnapshotCaptureProfile<TState>? profile = null;
        TState? preCommandState = null;
        if (qualifiedProfileId is not null)
        {
            try
            {
                profile = FoldedSnapshotCapture;
                if (profile is null || !string.Equals(profile.ProfileId, qualifiedProfileId, StringComparison.Ordinal)
                    || !Equals(profile.Limits, qualifiedLimits))
                {
                    profile = null;
                }
                else if (typedState is not null)
                {
                    // The declared graph maximum was admitted when the immutable profile was built.
                    preCommandState = profile.StateCapture.Capture(typedState, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch { profile = null; preCommandState = null; }
        }
        DomainResult result = await HandleAsync(command, typedState, cancellationToken).ConfigureAwait(false);
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
            (wireEvents, token) => CaptureFoldedState(command, preSequence, admittedState, admittedProfile, wireEvents, token));
    }

    private static string CaptureFoldedState(
        CommandEnvelope command,
        long preSequence,
        TState? preCommandState,
        FoldedSnapshotCaptureProfile<TState> profile,
        IReadOnlyList<DomainServiceWireEvent> wireEvents,
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
        var detached = new EventEnvelope[wireEvents.Count];
        for (int index = 0; index < wireEvents.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DomainServiceWireEvent item = wireEvents[index];
            var metadata = new EventMetadata(
                command.MessageId, command.AggregateId, command.Domain, command.TenantId, command.Domain,
                checked(preSequence + index + 1), 0, DateTimeOffset.UtcNow, command.CorrelationId,
                command.CausationId ?? command.CorrelationId, command.UserId, "capture", item.EventTypeName,
                item.MetadataVersion ?? 1, item.SerializationFormat) { PayloadVersion = item.PayloadVersion };
            detached[index] = new EventEnvelope(metadata, item.Payload.ToArray(), null);
        }
        var input = new DomainServiceCurrentState(preCommandState, detached, preSequence,
            checked(preSequence + detached.Length));
        TState folded = DomainProcessorStateRehydrator.RehydrateState<TState>(input,
            DomainProcessorStateRehydrator.DiscoverApplyMethods(typeof(TState)), cancellationToken)
            ?? throw new InvalidOperationException("SnapshotCaptureStateInvalid");
        using var output = new BoundedSnapshotJsonStream(profile.MaximumStateJsonBytes);
        JsonSerializer.Serialize(output, folded, EventStorePayloadSerialization.Options);
        byte[] bytes = output.ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { MaxDepth = profile.MaximumDepth });
        int nodes = 0;
        while (reader.Read())
        {
            if (++nodes > profile.MaximumNodeCount) { throw new InvalidOperationException("SnapshotCaptureLimitExceeded"); }
        }
        if (checked(16L * bytes.Length + 64L * nodes) > profile.Limits.MaximumDomainParsedStateGraphBytes)
        {
            throw new InvalidOperationException("SnapshotCaptureLimitExceeded");
        }
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement state = document.RootElement;
        if (state.ValueKind != JsonValueKind.Object
            || (state.TryGetProperty("currentSequence", out _) && state.TryGetProperty("events", out _)))
        {
            throw new InvalidOperationException("SnapshotStateShapeCollision");
        }
        return Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// Processes a command against optional typed aggregate state and returns domain events.
    /// </summary>
    /// <param name="command">The command envelope to process.</param>
    /// <param name="currentState">The current typed aggregate state, or null for new aggregates.</param>
    /// <returns>A <see cref="DomainResult"/> containing the resulting domain events.</returns>
    protected abstract Task<DomainResult> HandleAsync(CommandEnvelope command, TState? currentState);

    /// <summary>Processes the command with the originating request cancellation token.</summary>
    /// <param name="command">The command envelope to process.</param>
    /// <param name="currentState">The rehydrated aggregate state, or null for a new aggregate.</param>
    /// <param name="cancellationToken">The originating request cancellation token.</param>
    /// <returns>The complete domain result.</returns>
    /// <remarks>Override this overload to pass cancellation into asynchronous domain work.</remarks>
    protected virtual Task<DomainResult> HandleAsync(CommandEnvelope command, TState? currentState, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        return HandleAsync(command, currentState);
    }
}
