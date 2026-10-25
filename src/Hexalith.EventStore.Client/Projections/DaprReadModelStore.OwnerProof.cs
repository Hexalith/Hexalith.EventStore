using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Client.Projections;

public sealed partial class DaprReadModelStore : IReadModelOwnerProofStore
{
    /// <inheritdoc/>
    public Task<ProjectionOwnerProofResult> ReadOwnerProofAsync(ProjectionOwnerProofRequest request,
        ReadModelOwnerProofBinding binding, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(binding);
        return ReadModelOwnerProofReader.ReadAsync(new DaprReadModelBatchStateAccessor(_daprClient, binding.StoreName),
            _options, request, binding, cancellationToken);
    }
}
