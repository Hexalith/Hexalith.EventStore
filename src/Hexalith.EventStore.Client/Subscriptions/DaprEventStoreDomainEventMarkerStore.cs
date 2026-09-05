using Dapr.Client;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Client.Subscriptions;

/// <summary>
/// DAPR state-store implementation of <see cref="IEventStoreDomainEventMarkerStore"/>.
/// </summary>
/// <param name="daprClient">The DAPR client.</param>
/// <param name="options">Domain-event consumer options.</param>
public sealed class DaprEventStoreDomainEventMarkerStore(
    DaprClient daprClient,
    IOptions<EventStoreDomainEventsOptions> options) : IEventStoreDomainEventMarkerStore {
    private const int MaxTransitionAttempts = 5;

    private static readonly IReadOnlyDictionary<string, string> s_emptyMetadata =
        new Dictionary<string, string>(StringComparer.Ordinal);

    private readonly DaprClient _daprClient = daprClient ?? throw new ArgumentNullException(nameof(daprClient));
    private readonly IOptions<EventStoreDomainEventsOptions> _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc/>
    public async Task<EventStoreDomainEventMarkerAcquisitionResult> TryAcquireAsync(
        string messageId,
        CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

        EventStoreDomainEventsOptions value = _options.Value;
        string key = BuildMarkerKey(value, messageId);
        EventStoreDomainEventMarkerRecord? existing = await _daprClient
            .GetStateAsync<EventStoreDomainEventMarkerRecord?>(
                value.MarkerStateStoreName,
                key,
                ConsistencyMode.Strong,
                metadata: s_emptyMetadata,
                cancellationToken)
            .ConfigureAwait(false);

        return existing?.State switch {
            null => EventStoreDomainEventMarkerAcquisitionResult.Acquired,
            EventStoreDomainEventMarkerState.Completed => EventStoreDomainEventMarkerAcquisitionResult.Completed,
            EventStoreDomainEventMarkerState.InProgress => EventStoreDomainEventMarkerAcquisitionResult.InProgress,
            EventStoreDomainEventMarkerState.Dispatched => EventStoreDomainEventMarkerAcquisitionResult.CompletionPending,
            _ => (EventStoreDomainEventMarkerAcquisitionResult)(-1),
        };
    }

    /// <inheritdoc/>
    public Task<bool> MarkDispatchedAsync(string messageId, CancellationToken cancellationToken = default)
        => TransitionAsync(messageId, EventStoreDomainEventMarkerState.Dispatched, cancellationToken);

    /// <inheritdoc/>
    public async Task MarkCompletedAsync(string messageId, CancellationToken cancellationToken = default)
        => _ = await TransitionAsync(messageId, EventStoreDomainEventMarkerState.Completed, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public Task ReleaseAsync(string messageId, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

        // Releasing is intentionally a no-op for the DAPR store. TryAcquireAsync only reads state and never
        // persists an in-progress lease, so a failing delivery owns no marker of its own to release. Deleting
        // the key unconditionally would race a concurrent sibling delivery that already wrote a durable
        // Dispatched or Completed marker and wipe it, letting a later redelivery re-run side effects. A failed
        // delivery simply leaves no marker behind and is re-acquired on redelivery.
        _ = cancellationToken;
        return Task.CompletedTask;
    }

    private async Task<bool> TransitionAsync(
        string messageId,
        EventStoreDomainEventMarkerState targetState,
        CancellationToken cancellationToken) {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

        EventStoreDomainEventsOptions value = _options.Value;
        string key = BuildMarkerKey(value, messageId);
        for (int attempt = 1; attempt <= MaxTransitionAttempts; attempt++) {
            cancellationToken.ThrowIfCancellationRequested();
            (EventStoreDomainEventMarkerRecord? existing, string etag) = await _daprClient
                .GetStateAndETagAsync<EventStoreDomainEventMarkerRecord?>(
                    value.MarkerStateStoreName,
                    key,
                    ConsistencyMode.Strong,
                    s_emptyMetadata,
                    cancellationToken)
                .ConfigureAwait(false);

            bool? alreadyTransitioned = ClassifyExistingState(messageId, existing, targetState);
            if (alreadyTransitioned.HasValue) {
                return alreadyTransitioned.Value;
            }

            EventStoreDomainEventMarkerRecord replacement = targetState switch {
                EventStoreDomainEventMarkerState.Dispatched => EventStoreDomainEventMarkerRecord.Dispatched(DateTimeOffset.UtcNow),
                EventStoreDomainEventMarkerState.Completed => EventStoreDomainEventMarkerRecord.Completed(DateTimeOffset.UtcNow),
                _ => throw new ArgumentOutOfRangeException(nameof(targetState), targetState, "Unsupported marker transition target."),
            };
            bool saved = await _daprClient
                .TrySaveStateAsync(
                    value.MarkerStateStoreName,
                    key,
                    replacement,
                    etag,
                    new StateOptions { Concurrency = ConcurrencyMode.FirstWrite },
                    s_emptyMetadata,
                    cancellationToken)
                .ConfigureAwait(false);
            if (saved) {
                return targetState == EventStoreDomainEventMarkerState.Dispatched;
            }
        }

        (EventStoreDomainEventMarkerRecord? finalState, _) = await _daprClient
            .GetStateAndETagAsync<EventStoreDomainEventMarkerRecord?>(
                value.MarkerStateStoreName,
                key,
                ConsistencyMode.Strong,
                s_emptyMetadata,
                cancellationToken)
            .ConfigureAwait(false);
        bool? converged = ClassifyExistingState(messageId, finalState, targetState);
        if (converged.HasValue) {
            return converged.Value;
        }

        throw new InvalidOperationException(
            $"Could not persist marker transition for message '{messageId}' to state '{targetState}' after {MaxTransitionAttempts} attempts.");
    }

    private static bool? ClassifyExistingState(
        string messageId,
        EventStoreDomainEventMarkerRecord? existing,
        EventStoreDomainEventMarkerState targetState) {
        if (existing is null) {
            return null;
        }

        return existing.State switch {
            EventStoreDomainEventMarkerState.Completed => false,
            EventStoreDomainEventMarkerState.Dispatched when targetState == EventStoreDomainEventMarkerState.Dispatched => true,
            EventStoreDomainEventMarkerState.Dispatched when targetState == EventStoreDomainEventMarkerState.Completed => null,
            EventStoreDomainEventMarkerState.InProgress => null,
            _ => throw new InvalidOperationException(
                $"Cannot transition marker for message '{messageId}' from unsupported state '{existing.State}' to '{targetState}'."),
        };
    }

    private static string BuildMarkerKey(EventStoreDomainEventsOptions options, string messageId) {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.MarkerStateStoreName);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.TopicName);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.SubscriptionRoute);

        return string.Concat(
            options.MarkerKeyPrefix ?? string.Empty,
            Uri.EscapeDataString(options.TopicName),
            ":",
            Uri.EscapeDataString(options.SubscriptionRoute),
            ":",
            messageId);
    }
}
