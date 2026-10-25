using System.Text.Json;

using Dapr.Client;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// DAPR state-store implementation of <see cref="IReadModelStore"/>, the additive coordinated
/// <see cref="IReadModelBatchStore"/>, its marker-gated <see cref="IReadModelBatchStagingStore"/>
/// companion, and the additive ETag-conditional
/// <see cref="IReadModelConditionalEraser"/>.
/// </summary>
/// <remarks>
/// <para>
/// Single-key writes via <see cref="TrySaveAsync{TValue}"/> use first-write-wins concurrency so the
/// reload-and-merge loop in <see cref="ReadModelWritePolicy"/> can detect and retry on conflict. Batches
/// use the marker-gated resumable protocol by default; a store must be explicitly qualified as
/// <see cref="ReadModelBatchStoreProfile.TransactionQualified"/> to use the single-transaction path.
/// </para>
/// <para>
/// <see cref="GetAsync{TValue}"/> reads raw bytes through the pinned byte-state API and decodes both legacy
/// raw values and versioned batch envelopes, so a reader observes the previous complete value until a
/// resumable batch's commit marker is durable, and the committed value afterwards.
/// </para>
/// </remarks>
public sealed partial class DaprReadModelStore : IReadModelStore, IReadModelExpiringStore, IReadModelBulkStore, IReadModelBatchStore, IReadModelBatchStagingStore, IReadModelConditionalEraser {
    private readonly DaprClient _daprClient;
    private readonly ReadModelBatchOptions _options;
    private readonly ILogger _logger;

    /// <summary>Gets the existing SDK budget for shared maintenance attempts.</summary>
    internal TimeSpan ReconciliationTimeout => _options.ReconciliationTimeout;

    /// <summary>Initializes a new <see cref="DaprReadModelStore"/>.</summary>
    /// <param name="daprClient">The DAPR client used to access the state store.</param>
    /// <param name="options">The coordinated-batch options and per-store profiles.</param>
    /// <param name="logger">The logger for bounded structured batch events.</param>
    public DaprReadModelStore(
        DaprClient daprClient,
        IOptions<ReadModelBatchOptions>? options = null,
        ILogger<DaprReadModelStore>? logger = null) {
        ArgumentNullException.ThrowIfNull(daprClient);
        _daprClient = daprClient;
        _options = options?.Value ?? new ReadModelBatchOptions();
        _logger = logger ?? NullLogger<DaprReadModelStore>.Instance;
    }

    /// <inheritdoc/>
    public async Task<ReadModelEntry<TValue>> GetAsync<TValue>(
        string storeName,
        string key,
        CancellationToken cancellationToken = default)
        where TValue : class {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var accessor = new DaprReadModelBatchStateAccessor(_daprClient, storeName);
        RawStateEntry visible = await ReadModelBatchProtocol
            .ResolveVisibleAsync(accessor, key, cancellationToken, _options.TimeProvider)
            .ConfigureAwait(false);
        if (!visible.Exists) {
            return new ReadModelEntry<TValue>(null, null) { ExpiresAtUtc = visible.ExpiresAtUtc };
        }

        TValue? value = JsonSerializer.Deserialize<TValue>(visible.Value.Span, _daprClient.JsonSerializerOptions);
        return new ReadModelEntry<TValue>(value, visible.ETag) { ExpiresAtUtc = visible.ExpiresAtUtc };
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ReadModelBulkEntry<TValue>>> GetManyAsync<TValue>(
        string storeName,
        IReadOnlyList<string> keys,
        int parallelism,
        CancellationToken cancellationToken = default)
        where TValue : class {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeName);
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(parallelism);
        if (keys.Any(string.IsNullOrWhiteSpace)
            || keys.Distinct(StringComparer.Ordinal).Count() != keys.Count) {
            throw new ArgumentException("Bulk read-model keys must be distinct and non-empty.", nameof(keys));
        }

        if (keys.Count == 0) {
            return [];
        }

        IReadOnlyList<BulkStateItem<JsonElement>> items = await _daprClient
            .GetBulkStateAsync<JsonElement>(
                storeName,
                keys,
                parallelism,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        Dictionary<string, BulkStateItem<JsonElement>> byKey = items
            .Where(static item => !string.IsNullOrWhiteSpace(item.Key))
            .GroupBy(static item => item.Key, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Single(), StringComparer.Ordinal);

        var result = new List<ReadModelBulkEntry<TValue>>(keys.Count);
        var accessor = new DaprReadModelBatchStateAccessor(_daprClient, storeName);
        foreach (string key in keys) {
            cancellationToken.ThrowIfCancellationRequested();
            if (!byKey.TryGetValue(key, out BulkStateItem<JsonElement> item)
                || item.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) {
                result.Add(new ReadModelBulkEntry<TValue>(key, null, null));
                continue;
            }

            ReadOnlyMemory<byte> raw = JsonSerializer.SerializeToUtf8Bytes(item.Value);
            if (ReadModelBatchEnvelope.IsEnvelope(raw.Span) || ReadModelBatchExpiringValue.IsExpiring(raw.Span)) {
                RawStateEntry visible = ReadModelBatchExpiringValue.IsExpiring(raw.Span)
                    ? ReadModelBatchExpiringValue.Resolve(new RawStateEntry(true, raw, item.ETag), _options.TimeProvider)
                    : await ReadModelBatchProtocol.ResolveVisibleAsync(accessor, key, cancellationToken, _options.TimeProvider).ConfigureAwait(false);
                TValue? visibleValue = visible.Exists
                    ? JsonSerializer.Deserialize<TValue>(visible.Value.Span, _daprClient.JsonSerializerOptions)
                    : null;
                result.Add(new ReadModelBulkEntry<TValue>(
                    key,
                    visibleValue,
                    visible.Exists ? visible.ETag : null));
                continue;
            }

            TValue? value = item.Value.Deserialize<TValue>(_daprClient.JsonSerializerOptions);
            result.Add(new ReadModelBulkEntry<TValue>(key, value, item.ETag));
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task SaveAsync<TValue>(
        string storeName,
        string key,
        TValue value,
        CancellationToken cancellationToken = default)
        where TValue : class {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        RecordTypedMutation(value);
        await _daprClient
            .SaveStateAsync(storeName, key, value, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> TrySaveAsync<TValue>(
        string storeName,
        string key,
        TValue value,
        string etag,
        CancellationToken cancellationToken = default)
        where TValue : class {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(etag);

        return await TrySaveConditionalAsync(storeName, key, value, etag, null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> TrySaveWithTimeToLiveAsync<TValue>(
        string storeName,
        string key,
        TValue value,
        string etag,
        TimeSpan timeToLive,
        CancellationToken cancellationToken = default)
        where TValue : class {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(etag);
        long ttlSeconds = ToTtlSeconds(timeToLive);

        return await TrySaveConditionalAsync(storeName, key, value, etag,
            new Dictionary<string, string>(StringComparer.Ordinal) {
                ["ttlInSeconds"] = ttlSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
            }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> TryEraseAsync(
        string storeName,
        string key,
        string etag,
        CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(etag);

        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MutationAttempts);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.StateRpcAttempts);
        return await _daprClient
            .TryDeleteStateAsync(
                storeName,
                key,
                etag,
                new StateOptions { Concurrency = ConcurrencyMode.FirstWrite },
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<(bool Present, string Etag)> TryReadEtagAsync(
        string storeName,
        string key,
        CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        // Mirror GetAsync's marker-gated visibility read so the returned ETag matches the key that
        // TryEraseAsync deletes (type-agnostic: raw bytes only, never a TValue deserialization). A
        // committed-delete envelope reports absent, exactly as the visible value would.
        var accessor = new DaprReadModelBatchStateAccessor(_daprClient, storeName);
        RawStateEntry visible = await ReadModelBatchProtocol
            .ResolveVisibleAsync(accessor, key, cancellationToken, _options.TimeProvider)
            .ConfigureAwait(false);
        return visible.Exists ? (true, visible.ETag) : (false, string.Empty);
    }

    /// <inheritdoc/>
    public void ValidateBatch(ReadModelBatch batch) => ReadModelBatchValidation.Validate(batch, _options);

    /// <inheritdoc/>
    public async Task<ReadModelBatchResult> ExecuteAsync(
        ReadModelBatch batch,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(batch);

        var accessor = new DaprReadModelBatchStateAccessor(_daprClient, batch.Scope.StoreName);
        var protocol = new ReadModelBatchProtocol(accessor, _options, _logger);
        return await protocol.ExecuteAsync(batch, injector: null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<ReadModelBatchStagingResult> StageAsync(
        ReadModelBatch batch,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(batch);
        var accessor = new DaprReadModelBatchStateAccessor(_daprClient, batch.Scope.StoreName);
        var protocol = new ReadModelBatchProtocol(accessor, _options, _logger);
        return await protocol.StageAsync(batch, injector: null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<ReadModelBatchStagingResult> CommitAsync(
        ReadModelBatch batch,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(batch);
        var accessor = new DaprReadModelBatchStateAccessor(_daprClient, batch.Scope.StoreName);
        var protocol = new ReadModelBatchProtocol(accessor, _options, _logger);
        return await protocol.CommitStagedAsync(batch, injector: null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<ReadModelBatchStagingResult> AbortAsync(
        ReadModelBatch batch,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(batch);
        var accessor = new DaprReadModelBatchStateAccessor(_daprClient, batch.Scope.StoreName);
        var protocol = new ReadModelBatchProtocol(accessor, _options, _logger);
        return await protocol.AbortStagedAsync(batch, injector: null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<ReadModelBatchStagingResult> VerifyAsync(
        ReadModelBatch batch,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(batch);
        var accessor = new DaprReadModelBatchStateAccessor(_daprClient, batch.Scope.StoreName);
        var protocol = new ReadModelBatchProtocol(accessor, _options, _logger);
        return await protocol.VerifyStagedAsync(batch, injector: null, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> TrySaveConditionalAsync<TValue>(string storeName, string key, TValue value,
        string etag, IReadOnlyDictionary<string, string>? metadata, CancellationToken token) where TValue : class
    {
        async Task<bool> WriteAsync(string physicalETag)
        {
            RecordTypedMutation(value);
            return await _daprClient.TrySaveStateAsync(storeName, key, value, physicalETag,
                new StateOptions { Concurrency = ConcurrencyMode.FirstWrite }, metadata, token).ConfigureAwait(false);
        }
        Dapr.DaprException? failure = null;
        try { if (await WriteAsync(etag).ConfigureAwait(false)) return true; }
        catch (Dapr.DaprException exception) when (etag.Length == 0 && !token.IsCancellationRequested) { failure = exception; }
        if (etag.Length != 0) return false;
        RawStateEntry physical;
        try
        {
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MetadataReads);
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.StateRpcAttempts);
            (ReadOnlyMemory<byte> bytes, string physicalETag) = await _daprClient.GetByteStateAndETagAsync(
                storeName, key, consistencyMode: ConsistencyMode.Strong, cancellationToken: token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MetadataReadBytes, bytes.Length);
            physical = new(!string.IsNullOrEmpty(physicalETag), bytes, physicalETag);
        }
        catch (Exception) when (!token.IsCancellationRequested)
        {
            if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
            return false;
        }
        if (!physical.Exists)
        {
            if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
            return false;
        }
        // The rejected physical create and this observed CAS protect the logically absent wrapper.
        // Pending envelopes and ordinary/unexpired rows remain conflicts; never delete before retry.
        return ReadModelBatchExpiringValue.IsExpiring(physical.Value.Span)
            && !ReadModelBatchExpiringValue.Resolve(physical, _options.TimeProvider).Exists
            && await WriteAsync(physical.ETag).ConfigureAwait(false);
    }

    private static long ToTtlSeconds(TimeSpan timeToLive) {
        if (timeToLive <= TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(timeToLive), "Time-to-live must be greater than zero.");
        }

        return checked((long)Math.Ceiling(timeToLive.TotalSeconds));
    }

    private void RecordTypedMutation<TValue>(TValue value)
    {
        long bytes = JsonSerializer.SerializeToUtf8Bytes(value, _daprClient.JsonSerializerOptions).Length;
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MutationAttempts);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MutationBytes, bytes);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ModelBytes, bytes);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.StateRpcAttempts);
    }
}
