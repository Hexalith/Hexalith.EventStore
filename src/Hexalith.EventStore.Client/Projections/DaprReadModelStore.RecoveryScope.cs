namespace Hexalith.EventStore.Client.Projections;

public sealed partial class DaprReadModelStore : IReadModelRecoveryScopeStore
{
    /// <inheritdoc/>
    public Task<ReadModelBatchStagingResult> PrepareRecoveryManifestAsync(ReadModelBatch batch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);
        return new ReadModelBatchProtocol(new DaprReadModelBatchStateAccessor(_daprClient, batch.Scope.StoreName), _options, _logger)
            .PrepareRecoveryManifestAsync(batch, null, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ReadModelBatch?> ReadPreparedRecoveryBatchAsync(ReadModelBatchScope scope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return ReadModelBatchProtocol.ReadPreparedRecoveryBatchAsync(new DaprReadModelBatchStateAccessor(_daprClient, scope.StoreName), scope, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ReadModelBatchStagingResult> ReadRecoveryDecisionAsync(ReadModelBatchScope scope, string fingerprint, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return ReadModelBatchProtocol.ReadRecoveryDecisionAsync(new DaprReadModelBatchStateAccessor(_daprClient, scope.StoreName), scope, fingerprint, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ReadModelPendingBatchRecoveryResult> RetireRecoveryScopeAsync(ReadModelBatchScope scope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return ReadModelRecoveryDiscovery.RetireAsync(new DaprReadModelBatchStateAccessor(_daprClient, scope.StoreName),
            _options, _logger, scope, cancellationToken);
    }

    /// <inheritdoc/>
    public Task ReleaseRecoveryScopeAsync(ReadModelBatchScope scope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return ReadModelRecoveryDiscovery.ReleaseAsync(new DaprReadModelBatchStateAccessor(_daprClient, scope.StoreName), scope, cancellationToken);
    }
}
