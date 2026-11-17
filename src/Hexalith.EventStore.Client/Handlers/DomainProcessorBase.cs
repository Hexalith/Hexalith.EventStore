
using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
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

    private Task<FoldedSnapshotProcessingResult> ProcessCoreAsync(
        CommandEnvelope command, object? currentState, string? qualifiedProfileId,
        FoldedSnapshotCaptureLimits? qualifiedLimits, CancellationToken cancellationToken)
        => FoldedSnapshotProcessor<TState>.ProcessAsync(command, currentState, qualifiedProfileId, qualifiedLimits,
            () => SnapshotCapture, () => FoldedSnapshotCapture, HandleAsync,
            () => DomainProcessorStateRehydrator.DiscoverApplyMethods(typeof(TState)), null, cancellationToken);

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
