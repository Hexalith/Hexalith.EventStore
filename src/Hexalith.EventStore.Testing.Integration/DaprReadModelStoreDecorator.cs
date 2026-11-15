using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Testing.Integration;

/// <summary>
/// Preserves the production Dapr adapter's capability surface when a live fixture injects a selected store failure.
/// Every operation delegates to the original adapter; this type supplies no persistence or recovery algorithm.
/// </summary>
/// <param name="inner">The configured production Dapr read-model adapter.</param>
public abstract class DaprReadModelStoreDecorator(DaprReadModelStore inner) : IReadModelStore,
    IReadModelBatchStore, IReadModelPendingBatchRecoveryStore, IReadModelExpiringStore, IReadModelBulkStore,
    IReadModelConditionalEraser, IReadModelBatchStagingStore, IReadModelOwnerProofStore,
    IReadModelAppliedReceiptStore, IReadModelAppliedReceiptRecoveryStore, IReadModelRecoveryScopeStore
{
    private readonly DaprReadModelStore _inner = inner ?? throw new ArgumentNullException(nameof(inner));

    /// <inheritdoc />
    public virtual Task<ReadModelEntry<T>> GetAsync<T>(string storeName, string key, CancellationToken cancellationToken = default) where T : class
        => _inner.GetAsync<T>(storeName, key, cancellationToken);

    /// <inheritdoc />
    public virtual Task<ReadModelEntry<T>> GetCompactedAsync<T>(string storeName, string key, CancellationToken cancellationToken = default) where T : class
        => _inner.GetCompactedAsync<T>(storeName, key, cancellationToken);

    /// <inheritdoc />
    public virtual Task SaveAsync<T>(string storeName, string key, T value, CancellationToken cancellationToken = default) where T : class
        => _inner.SaveAsync(storeName, key, value, cancellationToken);

    /// <inheritdoc />
    public virtual Task<bool> TrySaveAsync<T>(string storeName, string key, T value, string etag, CancellationToken cancellationToken = default) where T : class
        => _inner.TrySaveAsync(storeName, key, value, etag, cancellationToken);

    /// <inheritdoc />
    public void ValidateBatch(ReadModelBatch batch) => _inner.ValidateBatch(batch);

    /// <inheritdoc />
    public Task<ReadModelBatchResult> ExecuteAsync(ReadModelBatch batch, CancellationToken cancellationToken = default)
        => _inner.ExecuteAsync(batch, cancellationToken);

    /// <inheritdoc />
    public Task<ReadModelPendingBatchRecoveryResult> RecoverPendingAsync(ReadModelBatchScope scope, string key, CancellationToken cancellationToken = default)
        => _inner.RecoverPendingAsync(scope, key, cancellationToken);

    /// <inheritdoc />
    public Task<ReadModelPendingBatchRecoveryResult> RecoverNonExpiringPendingAsync(ReadModelBatchScope scope, string key, CancellationToken cancellationToken = default)
        => _inner.RecoverNonExpiringPendingAsync(scope, key, cancellationToken);

    /// <inheritdoc />
    public Task<bool> TrySaveWithTimeToLiveAsync<T>(string storeName, string key, T value, string etag, TimeSpan timeToLive,
        CancellationToken cancellationToken = default) where T : class
        => _inner.TrySaveWithTimeToLiveAsync(storeName, key, value, etag, timeToLive, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<ReadModelBulkEntry<T>>> GetManyAsync<T>(string storeName, IReadOnlyList<string> keys,
        int parallelism, CancellationToken cancellationToken = default) where T : class
        => _inner.GetManyAsync<T>(storeName, keys, parallelism, cancellationToken);

    /// <inheritdoc />
    public Task<bool> TryEraseAsync(string storeName, string key, string etag, CancellationToken cancellationToken = default)
        => _inner.TryEraseAsync(storeName, key, etag, cancellationToken);

    /// <inheritdoc />
    public Task<(bool Present, string Etag)> TryReadEtagAsync(string storeName, string key, CancellationToken cancellationToken = default)
        => _inner.TryReadEtagAsync(storeName, key, cancellationToken);

    /// <inheritdoc />
    public Task<ReadModelBatchStagingResult> StageAsync(ReadModelBatch batch, CancellationToken cancellationToken = default)
        => _inner.StageAsync(batch, cancellationToken);

    /// <inheritdoc />
    public Task<ReadModelBatchStagingResult> CommitAsync(ReadModelBatch batch, CancellationToken cancellationToken = default)
        => _inner.CommitAsync(batch, cancellationToken);

    /// <inheritdoc />
    public Task<ReadModelBatchStagingResult> AbortAsync(ReadModelBatch batch, CancellationToken cancellationToken = default)
        => _inner.AbortAsync(batch, cancellationToken);

    /// <inheritdoc />
    public Task<ReadModelBatchStagingResult> VerifyAsync(ReadModelBatch batch, CancellationToken cancellationToken = default)
        => _inner.VerifyAsync(batch, cancellationToken);

    /// <inheritdoc />
    public Task<ProjectionOwnerProofResult> ReadOwnerProofAsync(ProjectionOwnerProofRequest request,
        ReadModelOwnerProofBinding binding, CancellationToken cancellationToken = default)
        => _inner.ReadOwnerProofAsync(request, binding, cancellationToken);

    /// <inheritdoc />
    public Task<ProjectionOwnerProofResult> ReadAppliedReceiptAsync(ProjectionAppliedReceiptRequest request,
        ReadModelOwnerProofBinding binding, CancellationToken cancellationToken = default)
        => _inner.ReadAppliedReceiptAsync(request, binding, cancellationToken);

    /// <inheritdoc />
    public Task<ProjectionOwnerProofResult> ReconcileAppliedReceiptAsync(ProjectionAppliedReceiptRequest request,
        ReadModelOwnerProofBinding binding, CancellationToken cancellationToken = default)
        => _inner.ReconcileAppliedReceiptAsync(request, binding, cancellationToken);

    /// <inheritdoc />
    public Task<ReadModelBatchStagingResult> PrepareRecoveryManifestAsync(ReadModelBatch batch, CancellationToken cancellationToken = default)
        => _inner.PrepareRecoveryManifestAsync(batch, cancellationToken);

    /// <inheritdoc />
    public Task<ReadModelBatch?> ReadPreparedRecoveryBatchAsync(ReadModelBatchScope scope, CancellationToken cancellationToken = default)
        => _inner.ReadPreparedRecoveryBatchAsync(scope, cancellationToken);

    /// <inheritdoc />
    public Task<ReadModelBatchStagingResult> ReadRecoveryDecisionAsync(ReadModelBatchScope scope, string fingerprint, CancellationToken cancellationToken = default)
        => _inner.ReadRecoveryDecisionAsync(scope, fingerprint, cancellationToken);

    /// <inheritdoc />
    public Task<ReadModelPendingBatchRecoveryResult> SettleRecoveryScopeAsync(ReadModelBatchScope scope, CancellationToken cancellationToken = default)
        => _inner.SettleRecoveryScopeAsync(scope, cancellationToken);

    /// <inheritdoc />
    public Task<ReadModelPendingBatchRecoveryResult> RetireRecoveryScopeAsync(ReadModelBatchScope scope, CancellationToken cancellationToken = default)
        => _inner.RetireRecoveryScopeAsync(scope, cancellationToken);

    /// <inheritdoc />
    public Task ReleaseRecoveryScopeAsync(ReadModelBatchScope scope, CancellationToken cancellationToken = default)
        => _inner.ReleaseRecoveryScopeAsync(scope, cancellationToken);
}
