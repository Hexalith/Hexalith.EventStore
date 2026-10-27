using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Tests.Security;

/// <summary>An existing owner implementation omitting optional read ports; invoking any effect port fails the test.</summary>
internal sealed class ReservedManifestUnavailableOwner : IDeletionProtectionOwner
{
    /// <inheritdoc/>
    public Task<DeletionConsumptionOutcome> RegisterAsync(DeletionBatchConsumptionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    /// <inheritdoc/>
    public Task<DeletionConsumptionOutcome> ReserveAndConsumeAsync(DeletionBatchConsumptionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    /// <inheritdoc/>
    public Task<DeletionConsumptionOutcome> ActivateAsync(DeletionReattestationActivation activation, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    /// <inheritdoc/>
    public Task<DeletionConsumptionOutcome> LookupAsync(string tenantId, string batchId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
