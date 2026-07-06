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
                consistencyMode: null,
                metadata: s_emptyMetadata,
                cancellationToken)
            .ConfigureAwait(false);

        return existing?.State == EventStoreDomainEventMarkerState.Completed
            ? EventStoreDomainEventMarkerAcquisitionResult.Completed
            : EventStoreDomainEventMarkerAcquisitionResult.Acquired;
    }

    /// <inheritdoc/>
    public async Task MarkCompletedAsync(string messageId, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

        EventStoreDomainEventsOptions value = _options.Value;
        _ = await _daprClient
            .TrySaveStateAsync(
                value.MarkerStateStoreName,
                BuildMarkerKey(value, messageId),
                EventStoreDomainEventMarkerRecord.Completed(DateTimeOffset.UtcNow),
                string.Empty,
                new StateOptions { Concurrency = ConcurrencyMode.FirstWrite },
                s_emptyMetadata,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task ReleaseAsync(string messageId, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

        EventStoreDomainEventsOptions value = _options.Value;
        await _daprClient
            .DeleteStateAsync(
                value.MarkerStateStoreName,
                BuildMarkerKey(value, messageId),
                new StateOptions(),
                s_emptyMetadata,
                cancellationToken)
            .ConfigureAwait(false);
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
