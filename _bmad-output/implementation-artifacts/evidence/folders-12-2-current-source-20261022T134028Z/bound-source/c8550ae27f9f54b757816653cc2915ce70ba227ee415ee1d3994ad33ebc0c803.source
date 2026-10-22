using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.DomainService;

/// <summary>Builds a full-replay plan from a bounded event stream rather than a complete request array.</summary>
public interface IAsyncDomainProjectionStreamedRebuildHandler : IAsyncDomainProjectionRebuildTransportHandler
{
    /// <summary>Prepares one owner plan from the exact ordered protected-route event stream.</summary>
    Task<DomainProjectionRebuildPlan> PrepareStreamedRebuildAsync(
        ProjectionRequest sourceHeader,
        IAsyncEnumerable<ProjectionEventDto> events,
        string operationId,
        CancellationToken cancellationToken);
}
