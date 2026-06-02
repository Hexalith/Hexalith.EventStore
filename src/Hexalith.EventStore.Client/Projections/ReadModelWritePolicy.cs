using System.Diagnostics;

using Hexalith.EventStore.Contracts.Projections;

using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// Optimistic-concurrency, merge-on-write helpers for persisted read models built on
/// <see cref="IReadModelStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is the platform generalization of the per-domain projection write policies domain modules
/// previously hand-wrote (e.g. <c>TenantProjectionWritePolicy</c>). Every write follows the same
/// reload-and-merge contract: read the current value (and ETag), produce the next value from it, and
/// attempt a first-write-wins save. On an ETag conflict the loaded value is stale, so the loop re-reads
/// and re-applies up to <see cref="DefaultMaxAttempts"/> times before failing.
/// </para>
/// <para>
/// The transform passed to <see cref="UpdateAsync{TValue}"/> MUST be idempotent with respect to the
/// loaded value: it may run more than once (on each retry) and, for event-replay read models, the same
/// events may be re-applied to an already-updated value. Callers must ensure re-applying does not
/// duplicate list entries, double-count, or otherwise diverge from a from-scratch rebuild.
/// </para>
/// </remarks>
public static partial class ReadModelWritePolicy {
    /// <summary>The default number of optimistic-concurrency attempts before failing.</summary>
    public const int DefaultMaxAttempts = 3;

    private const string ConflictReason = "optimistic-concurrency-conflict";
    private const string RetryExhaustedReason = "retry-exhausted";

    /// <summary>
    /// Reads the current read model under <paramref name="key"/>, produces the next value via
    /// <paramref name="update"/>, and saves it under optimistic concurrency, retrying on conflict.
    /// </summary>
    /// <typeparam name="TValue">The read-model type.</typeparam>
    /// <param name="store">The read-model store.</param>
    /// <param name="storeName">The DAPR state-store component name.</param>
    /// <param name="key">The state key.</param>
    /// <param name="update">
    /// Produces the next value from the current one (<see langword="null"/> when the key is absent).
    /// Must be idempotent — it can run on every retry.
    /// </param>
    /// <param name="context">Optional diagnostic context used for logging on conflict/exhaustion.</param>
    /// <param name="logger">Optional logger for conflict/exhaustion diagnostics.</param>
    /// <param name="maxAttempts">The maximum number of attempts (defaults to <see cref="DefaultMaxAttempts"/>).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The persisted value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the retry budget is exhausted.</exception>
    public static async Task<TValue> UpdateAsync<TValue>(
        IReadModelStore store,
        string storeName,
        string key,
        Func<TValue?, TValue> update,
        ReadModelWriteContext context = default,
        ILogger? logger = null,
        int maxAttempts = DefaultMaxAttempts,
        CancellationToken cancellationToken = default)
        where TValue : class {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrWhiteSpace(storeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(update);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);

        for (int attempt = 1; attempt <= maxAttempts; attempt++) {
            cancellationToken.ThrowIfCancellationRequested();
            ReadModelEntry<TValue> current = await store
                .GetAsync<TValue>(storeName, key, cancellationToken)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            TValue next = update(current.Value);
            cancellationToken.ThrowIfCancellationRequested();

            bool saved = await store
                .TrySaveAsync(storeName, key, next, current.ETag ?? string.Empty, cancellationToken)
                .ConfigureAwait(false);

            if (saved) {
                return next;
            }

            if (attempt == maxAttempts) {
                if (logger is not null) {
                    RetryExhausted(logger, storeName, key, context.Category, context.ProjectionType, attempt, maxAttempts, RetryExhaustedReason, context.CorrelationId);
                }

                throw new InvalidOperationException(
                    $"Read-model write for key '{key}' in store '{storeName}' exceeded the optimistic-concurrency retry limit after {maxAttempts} attempts.");
            }

            if (logger is not null) {
                OptimisticConcurrencyConflict(logger, storeName, key, context.Category, context.ProjectionType, attempt, maxAttempts, ConflictReason, context.CorrelationId);
            }
        }

        throw new UnreachableException();
    }

    /// <summary>
    /// Reload-and-apply convenience over <see cref="UpdateAsync{TValue}"/>: loads (or seeds) the read
    /// model and applies each event to it, retrying under optimistic concurrency.
    /// </summary>
    /// <typeparam name="TValue">The read-model type.</typeparam>
    /// <param name="store">The read-model store.</param>
    /// <param name="storeName">The DAPR state-store component name.</param>
    /// <param name="key">The state key.</param>
    /// <param name="events">The projection events to apply (nulls are skipped).</param>
    /// <param name="defaultFactory">Creates the initial value when the key is absent.</param>
    /// <param name="applyEvent">Applies a single event to the value. Must be idempotent.</param>
    /// <param name="context">Optional diagnostic context used for logging.</param>
    /// <param name="logger">Optional logger for conflict/exhaustion diagnostics.</param>
    /// <param name="maxAttempts">The maximum number of attempts (defaults to <see cref="DefaultMaxAttempts"/>).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The persisted value.</returns>
    public static Task<TValue> ApplyEventsAsync<TValue>(
        IReadModelStore store,
        string storeName,
        string key,
        IReadOnlyCollection<ProjectionEventDto?> events,
        Func<TValue> defaultFactory,
        Action<TValue, ProjectionEventDto> applyEvent,
        ReadModelWriteContext context = default,
        ILogger? logger = null,
        int maxAttempts = DefaultMaxAttempts,
        CancellationToken cancellationToken = default)
        where TValue : class {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(defaultFactory);
        ArgumentNullException.ThrowIfNull(applyEvent);

        ReadModelWriteContext enriched = context.WithEventDiagnostics(events);

        return UpdateAsync<TValue>(
            store,
            storeName,
            key,
            current => {
                TValue value = current ?? defaultFactory();
                foreach (ProjectionEventDto? evt in events) {
                    if (evt is not null) {
                        applyEvent(value, evt);
                    }
                }

                return value;
            },
            enriched,
            logger,
            maxAttempts,
            cancellationToken);
    }

    /// <summary>
    /// Merge convenience over <see cref="UpdateAsync{TValue}"/>: loads (or seeds) the read model and
    /// merges an incoming value into it, retrying under optimistic concurrency. Useful for
    /// cross-aggregate index/singleton read models.
    /// </summary>
    /// <typeparam name="TValue">The read-model type.</typeparam>
    /// <param name="store">The read-model store.</param>
    /// <param name="storeName">The DAPR state-store component name.</param>
    /// <param name="key">The state key.</param>
    /// <param name="incoming">The incoming value to merge.</param>
    /// <param name="defaultFactory">Creates the initial value when the key is absent.</param>
    /// <param name="merge">Merges the current value (first arg) with the incoming value (second arg). Must be idempotent.</param>
    /// <param name="context">Optional diagnostic context used for logging.</param>
    /// <param name="logger">Optional logger for conflict/exhaustion diagnostics.</param>
    /// <param name="maxAttempts">The maximum number of attempts (defaults to <see cref="DefaultMaxAttempts"/>).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The persisted value.</returns>
    public static Task<TValue> MergeAsync<TValue>(
        IReadModelStore store,
        string storeName,
        string key,
        TValue incoming,
        Func<TValue> defaultFactory,
        Func<TValue, TValue, TValue> merge,
        ReadModelWriteContext context = default,
        ILogger? logger = null,
        int maxAttempts = DefaultMaxAttempts,
        CancellationToken cancellationToken = default)
        where TValue : class {
        ArgumentNullException.ThrowIfNull(incoming);
        ArgumentNullException.ThrowIfNull(defaultFactory);
        ArgumentNullException.ThrowIfNull(merge);

        return UpdateAsync<TValue>(
            store,
            storeName,
            key,
            current => merge(current ?? defaultFactory(), incoming),
            context,
            logger,
            maxAttempts,
            cancellationToken);
    }

    [LoggerMessage(
        EventId = 200101,
        Level = LogLevel.Warning,
        Message = "Read-model optimistic-concurrency conflict for store {StateStoreName}, key {StateKey}, category {Category}, projection type {ProjectionType}, attempt {AttemptCount} of {MaxAttempts}, reason {Reason}, correlation ID {CorrelationId}.")]
    private static partial void OptimisticConcurrencyConflict(
        ILogger logger,
        string stateStoreName,
        string stateKey,
        string? category,
        string? projectionType,
        int attemptCount,
        int maxAttempts,
        string reason,
        string? correlationId);

    [LoggerMessage(
        EventId = 200102,
        Level = LogLevel.Error,
        Message = "Read-model optimistic-concurrency retry exhausted for store {StateStoreName}, key {StateKey}, category {Category}, projection type {ProjectionType}, attempts {AttemptCount} of {MaxAttempts}, reason {Reason}, correlation ID {CorrelationId}.")]
    private static partial void RetryExhausted(
        ILogger logger,
        string stateStoreName,
        string stateKey,
        string? category,
        string? projectionType,
        int attemptCount,
        int maxAttempts,
        string reason,
        string? correlationId);
}

/// <summary>
/// Lightweight diagnostic context attached to read-model writes for conflict/exhaustion logging.
/// All fields are optional; the platform never derives behavior from them.
/// </summary>
/// <param name="Category">A coarse category for the read model (e.g. the read-model name).</param>
/// <param name="ProjectionType">The projection type producing the write, when known.</param>
/// <param name="CorrelationId">The correlation identifier for tracing, when known.</param>
/// <param name="EventTypes">A bounded, comma-joined summary of event types in the batch, when known.</param>
public readonly record struct ReadModelWriteContext(
    string? Category = null,
    string? ProjectionType = null,
    string? CorrelationId = null,
    string? EventTypes = null) {
    private const int MaxLoggedEventTypes = 8;

    /// <summary>
    /// Returns a copy enriched with a correlation ID and a bounded event-type summary derived from
    /// <paramref name="events"/>, preserving any values already set.
    /// </summary>
    /// <param name="events">The projection events being applied.</param>
    /// <returns>The enriched context.</returns>
    public ReadModelWriteContext WithEventDiagnostics(IReadOnlyCollection<ProjectionEventDto?> events) {
        ArgumentNullException.ThrowIfNull(events);

        string? correlationId = CorrelationId
            ?? events.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e?.CorrelationId))?.CorrelationId;

        return this with {
            CorrelationId = correlationId,
            EventTypes = EventTypes ?? BuildBoundedEventTypes(events),
        };
    }

    private static string BuildBoundedEventTypes(IReadOnlyCollection<ProjectionEventDto?> events) {
        // Bound the joined log field so a full-replay batch with thousands of events cannot emit
        // hundreds-of-KB log lines per conflict or exhaustion.
        HashSet<string> distinct = new(StringComparer.Ordinal);
        List<string> sample = new(MaxLoggedEventTypes);
        int omitted = 0;
        foreach (ProjectionEventDto? evt in events) {
            string? name = evt?.EventTypeName;
            if (string.IsNullOrWhiteSpace(name) || !distinct.Add(name)) {
                continue;
            }

            if (sample.Count < MaxLoggedEventTypes) {
                sample.Add(name);
            }
            else {
                omitted++;
            }
        }

        string joined = string.Join(",", sample);
        return omitted > 0 ? $"{joined}+{omitted} more" : joined;
    }
}
