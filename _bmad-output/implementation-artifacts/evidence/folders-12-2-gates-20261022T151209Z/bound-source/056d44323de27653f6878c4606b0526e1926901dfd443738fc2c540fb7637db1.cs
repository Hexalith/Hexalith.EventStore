namespace Hexalith.EventStore.Client.Projections;

public sealed partial class DaprReadModelStore : IReadModelPendingBatchRecoveryStore
{
    /// <inheritdoc/>
    public Task<ReadModelPendingBatchRecoveryResult> RecoverPendingAsync(ReadModelBatchScope scope, string key,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return ReadModelPendingBatchRecovery.RecoverAsync(new DaprReadModelBatchStateAccessor(_daprClient, scope.StoreName),
            _options, _logger, scope, key, null, cancellationToken);
    }
}
