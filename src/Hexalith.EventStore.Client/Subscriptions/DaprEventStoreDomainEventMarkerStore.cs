using System.Text.Json;

using Dapr;
using Dapr.Client;

using Grpc.Core;

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

    private static readonly StateOptions s_checkedStrongWrite = new()
    {
        Concurrency = ConcurrencyMode.FirstWrite,
        Consistency = ConsistencyMode.Strong,
    };

    private static readonly IReadOnlyDictionary<string, string> s_probeMetadata =
        new Dictionary<string, string>(StringComparer.Ordinal) { ["ttlInSeconds"] = "120" };

    private static readonly IReadOnlyDictionary<string, string> s_emptyMetadata =
        new Dictionary<string, string>(StringComparer.Ordinal);

    private readonly DaprClient _daprClient = daprClient ?? throw new ArgumentNullException(nameof(daprClient));
    private readonly IOptions<EventStoreDomainEventsOptions> _options = options ?? throw new ArgumentNullException(nameof(options));

    private readonly SemaphoreSlim _qualificationGate = new(1);
    private string? _qualifiedKeyPrefix;

    /// <inheritdoc/>
    public bool SupportsContentBoundAdmission => true;

    /// <inheritdoc/>
    public bool IsDurable => true;

    /// <inheritdoc/>
    public async Task<EventStoreDomainEventMarkerAdmission> TryAcquireContentAsync(
        string messageId, string fingerprint, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        EventStoreDomainEventsOptions value = _options.Value;
        string prefix = await GetQualifiedKeyPrefixAsync(cancellationToken).ConfigureAwait(false);
        string key = prefix + Uri.EscapeDataString(messageId);
        var lease = new EventStoreDomainEventMarkerLease(messageId, fingerprint, Guid.NewGuid());
        (EventStoreDomainEventMarkerRecord? existing, string etag) = await ReadAsync(key, cancellationToken).ConfigureAwait(false);
        if (existing is not null && (existing.State != EventStoreDomainEventMarkerState.Released
                || existing.Fingerprint != fingerprint || existing.Fence == Guid.Empty))
        {
            return ClassifyContent(existing, lease);
        }

        if (existing is null)
        {
            // A pre-fencing record proves no match for this content. Leave it for authoritative recovery.
            (EventStoreDomainEventMarkerRecord? legacy, _) = await ReadAsync(BuildMarkerKey(value, messageId), cancellationToken)
                .ConfigureAwait(false);
            if (legacy is not null)
            {
                return new(EventStoreDomainEventMarkerAcquisitionResult.ContentConflict);
            }
        }

        EventStoreDomainEventMarkerRecord proposed = EventStoreDomainEventMarkerRecord.InProgress(DateTimeOffset.UtcNow) with
        {
            Fingerprint = fingerprint,
            Fence = lease.Fence,
        };
        EventStoreDomainEventMarkerRecord? confirmed = await WriteAndReadAsync(key, proposed, etag, cancellationToken)
            .ConfigureAwait(false);
        return confirmed is null ? new(EventStoreDomainEventMarkerAcquisitionResult.InProgress) : ClassifyContent(confirmed, lease);
    }

    /// <inheritdoc/>
    public Task<bool> MarkFencedDispatchedAsync(EventStoreDomainEventMarkerLease lease, CancellationToken cancellationToken = default)
        => AdvanceAsync(lease, EventStoreDomainEventMarkerState.InProgress, EventStoreDomainEventMarkerState.Dispatched, cancellationToken);

    /// <inheritdoc/>
    public async Task MarkFencedCompletedAsync(EventStoreDomainEventMarkerLease lease, CancellationToken cancellationToken = default)
        => _ = await AdvanceAsync(lease, EventStoreDomainEventMarkerState.Dispatched, EventStoreDomainEventMarkerState.Completed,
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task ReleaseFenceAsync(EventStoreDomainEventMarkerLease lease, CancellationToken cancellationToken = default)
    {
        string key = await GetFencedKeyAsync(lease, cancellationToken).ConfigureAwait(false);
        (EventStoreDomainEventMarkerRecord? current, string etag) = await ReadAsync(key, cancellationToken).ConfigureAwait(false);
        if (current is { State: EventStoreDomainEventMarkerState.InProgress } && Matches(current, lease))
        {
            // Keep the row so a provider that reuses starting ETags after deletion cannot admit a stale fence.
            EventStoreDomainEventMarkerRecord? readback = await WriteAndReadAsync(key,
                current with { State = EventStoreDomainEventMarkerState.Released, UpdatedAt = DateTimeOffset.UtcNow },
                etag, cancellationToken).ConfigureAwait(false);
            if (readback is null || (Matches(readback, lease) && readback.State == EventStoreDomainEventMarkerState.InProgress))
            {
                throw new InvalidOperationException("The subscription marker release could not be confirmed.");
            }
        }
    }

    private async Task<bool> AdvanceAsync(EventStoreDomainEventMarkerLease lease, EventStoreDomainEventMarkerState from,
        EventStoreDomainEventMarkerState to, CancellationToken cancellationToken)
    {
        string key = await GetFencedKeyAsync(lease, cancellationToken).ConfigureAwait(false);
        (EventStoreDomainEventMarkerRecord? current, string etag) = await ReadAsync(key, cancellationToken).ConfigureAwait(false);
        if (current is not null && Matches(current, lease))
        {
            if (current.State == from)
            {
                current = await WriteAndReadAsync(key, current with { State = to, UpdatedAt = DateTimeOffset.UtcNow },
                    etag, cancellationToken).ConfigureAwait(false);
            }

            if (current is not null && Matches(current, lease))
            {
                if (current.State == EventStoreDomainEventMarkerState.Completed)
                {
                    return false;
                }

                if (current.State == to)
                {
                    return true;
                }
            }
        }

        throw new InvalidOperationException("The subscription marker transition could not be confirmed.");
    }

    private async Task<EventStoreDomainEventMarkerRecord?> WriteAndReadAsync(
        string key, EventStoreDomainEventMarkerRecord value, string etag, CancellationToken cancellationToken)
    {
        DaprException? writeFailure = null;
        try
        {
            _ = await _daprClient.TrySaveStateAsync(_options.Value.MarkerStateStoreName, key, value, etag,
                s_checkedStrongWrite, s_emptyMetadata, cancellationToken).ConfigureAwait(false);
        }
        catch (DaprException exception) when (!cancellationToken.IsCancellationRequested)
        {
            writeFailure = exception;
        }

        (EventStoreDomainEventMarkerRecord? current, _) = await ReadAsync(key, cancellationToken).ConfigureAwait(false);
        if (current is null && writeFailure is not null)
        {
            throw new InvalidOperationException("The subscription marker write could not be confirmed.");
        }

        return current;
    }

    private async Task<(EventStoreDomainEventMarkerRecord? Value, string ETag)> ReadAsync(string key, CancellationToken cancellationToken)
    {
        (EventStoreDomainEventMarkerRecord? value, string etag) = await _daprClient.GetStateAndETagAsync<EventStoreDomainEventMarkerRecord?>(
            _options.Value.MarkerStateStoreName, key, ConsistencyMode.Strong, s_emptyMetadata, cancellationToken).ConfigureAwait(false);
        if ((value is null) != string.IsNullOrEmpty(etag))
        {
            throw new InvalidOperationException("The subscription marker store returned inconsistent state evidence.");
        }

        return (value, etag ?? string.Empty);
    }

    private async Task<string> GetFencedKeyAsync(EventStoreDomainEventMarkerLease lease, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lease);
        return await GetQualifiedKeyPrefixAsync(cancellationToken).ConfigureAwait(false) + Uri.EscapeDataString(lease.MessageId);
    }

    private async Task<string> GetQualifiedKeyPrefixAsync(CancellationToken cancellationToken)
    {
        string? prefix = Volatile.Read(ref _qualifiedKeyPrefix);
        if (prefix is not null)
        {
            return prefix;
        }

        await _qualificationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_qualifiedKeyPrefix is not null)
            {
                return _qualifiedKeyPrefix;
            }

            EventStoreDomainEventsOptions value = _options.Value;
            DaprMetadata metadata = await _daprClient.GetMetadataAsync(cancellationToken).ConfigureAwait(false);
            DaprComponentsMetadata? component = metadata.Components.SingleOrDefault(
                candidate => candidate.Name == value.MarkerStateStoreName);
            if (string.IsNullOrWhiteSpace(metadata.Id) || component is null
                || !component.Capabilities.Contains("ETAG", StringComparer.Ordinal))
            {
                throw new InvalidOperationException("The subscription marker component does not advertise the required ETag capability.");
            }

            ArgumentException.ThrowIfNullOrWhiteSpace(value.PubSubName);
            ArgumentException.ThrowIfNullOrWhiteSpace(value.TopicName);
            ArgumentException.ThrowIfNullOrWhiteSpace(value.SubscriptionRoute);
            string scope = EventStorePayloadFingerprint.Compute(JsonSerializer.SerializeToUtf8Bytes(
                new[] { metadata.Id, value.PubSubName, value.TopicName, value.SubscriptionRoute }));
            prefix = $"{value.MarkerKeyPrefix}v2:{scope}:";
            await VerifyCreateIfAbsentAsync(prefix, component.Type, cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref _qualifiedKeyPrefix, prefix);
            return prefix;
        }
        finally
        {
            _qualificationGate.Release();
        }
    }

    private async Task VerifyCreateIfAbsentAsync(string prefix, string componentType, CancellationToken cancellationToken)
    {
        string key = $"{prefix}probe:{Guid.NewGuid():N}";
        string store = _options.Value.MarkerStateStoreName;
        var original = EventStoreDomainEventMarkerRecord.InProgress(DateTimeOffset.UtcNow) with { Fence = Guid.NewGuid() };
        var competing = original with { Fence = Guid.NewGuid() };
        if (!await _daprClient.TrySaveStateAsync(store, key, original, string.Empty, s_checkedStrongWrite,
                s_probeMetadata, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("The subscription marker create-if-absent probe could not write.");
        }

        bool overwrote;
        try
        {
            overwrote = await _daprClient.TrySaveStateAsync(store, key, competing, string.Empty, s_checkedStrongWrite,
                s_probeMetadata, cancellationToken).ConfigureAwait(false);
        }
        catch (DaprException failure) when (!cancellationToken.IsCancellationRequested
            && componentType == "state.redis" && IsRedisFirstWriteRejection(failure, key))
        {
            overwrote = false;
        }

        (EventStoreDomainEventMarkerRecord? readback, string etag) = await ReadAsync(key, cancellationToken).ConfigureAwait(false);
        if (overwrote || readback != original
            || !await _daprClient.TryDeleteStateAsync(store, key, etag, s_checkedStrongWrite, s_emptyMetadata, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("The subscription marker component did not prove create-if-absent.");
        }
    }

    private static bool IsRedisFirstWriteRejection(DaprException failure, string key)
    {
        // Dapr 1.19.1 Redis surfaces a failed create-if-absent from its Lua script as Internal.
        // Accept only the exact rejection of this probe. Other errors cannot qualify the component.
        if (failure.InnerException is not RpcException { StatusCode: StatusCode.Internal } rpc)
        {
            return false;
        }

        const string rejection = "user_script:14: failed to set key ";
        string detail = rpc.Status.Detail;
        int offset = detail.LastIndexOf(rejection, StringComparison.Ordinal);
        if (offset < 0)
        {
            return false;
        }

        ReadOnlySpan<char> rejectedKey = detail.AsSpan(offset + rejection.Length);
        const string scriptSeparator = " script: ";
        int scriptOffset = rejectedKey.IndexOf(scriptSeparator, StringComparison.Ordinal);
        if (scriptOffset >= 0)
        {
            ReadOnlySpan<char> script = rejectedKey[(scriptOffset + scriptSeparator.Length)..];
            const string scriptLocation = ", on @user_script:14.";
            if (script.Length != 40 + scriptLocation.Length || !script.EndsWith(scriptLocation, StringComparison.Ordinal))
            {
                return false;
            }

            foreach (char digit in script[..40])
            {
                if (!char.IsAsciiHexDigit(digit))
                {
                    return false;
                }
            }

            rejectedKey = rejectedKey[..scriptOffset];
        }

        return rejectedKey.SequenceEqual(key) || rejectedKey.EndsWith("||" + key, StringComparison.Ordinal);
    }

    private static EventStoreDomainEventMarkerAdmission ClassifyContent(
        EventStoreDomainEventMarkerRecord existing, EventStoreDomainEventMarkerLease attempt)
    {
        if (existing.Fingerprint != attempt.Fingerprint || existing.Fence == Guid.Empty)
        {
            return new(EventStoreDomainEventMarkerAcquisitionResult.ContentConflict);
        }

        return existing.State switch
        {
            EventStoreDomainEventMarkerState.Completed => new(EventStoreDomainEventMarkerAcquisitionResult.Completed),
            EventStoreDomainEventMarkerState.Dispatched => new(EventStoreDomainEventMarkerAcquisitionResult.CompletionPending,
                attempt with { Fence = existing.Fence }),
            EventStoreDomainEventMarkerState.InProgress when Matches(existing, attempt)
                => new(EventStoreDomainEventMarkerAcquisitionResult.Acquired, attempt),
            _ => new(EventStoreDomainEventMarkerAcquisitionResult.InProgress),
        };
    }

    private static bool Matches(EventStoreDomainEventMarkerRecord current, EventStoreDomainEventMarkerLease lease)
        => current.Fingerprint == lease.Fingerprint && current.Fence != Guid.Empty && current.Fence == lease.Fence;

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
            _ => throw new InvalidOperationException(
                $"Cannot acquire marker for message '{messageId}' with unsupported durable state '{existing.State}'."),
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
            EventStoreDomainEventMarkerState.InProgress => throw new InvalidOperationException(
                $"Cannot transition marker for message '{messageId}' from in-progress state owned by another attempt to '{targetState}'."),
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
