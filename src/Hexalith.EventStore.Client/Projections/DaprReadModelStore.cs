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
public sealed class DaprReadModelStore : IReadModelStore, IReadModelBatchStore, IReadModelBatchStagingStore, IReadModelConditionalEraser {
    private readonly DaprClient _daprClient;
    private readonly ReadModelBatchOptions _options;
    private readonly ILogger _logger;

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
            .ResolveVisibleAsync(accessor, key, cancellationToken)
            .ConfigureAwait(false);
        if (!visible.Exists) {
            return new ReadModelEntry<TValue>(null, null);
        }

        TValue? value = JsonSerializer.Deserialize<TValue>(visible.Value.Span, _daprClient.JsonSerializerOptions);
        return new ReadModelEntry<TValue>(value, visible.ETag);
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

        return await _daprClient
            .TrySaveStateAsync(
                storeName,
                key,
                value,
                etag,
                new StateOptions { Concurrency = ConcurrencyMode.FirstWrite },
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
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
            .ResolveVisibleAsync(accessor, key, cancellationToken)
            .ConfigureAwait(false);
        return visible.Exists ? (true, visible.ETag) : (false, string.Empty);
    }

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
}
