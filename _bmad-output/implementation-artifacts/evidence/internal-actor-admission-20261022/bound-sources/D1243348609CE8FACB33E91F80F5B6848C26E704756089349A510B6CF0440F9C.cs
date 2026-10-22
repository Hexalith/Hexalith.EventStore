using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.DomainService;

internal sealed record DomainProjectionRebuildPreparation(
    IAsyncDomainProjectionHandler[] Handlers,
    ReadModelBatch? Batch,
    ProjectionDispatchResponse? Failure);
