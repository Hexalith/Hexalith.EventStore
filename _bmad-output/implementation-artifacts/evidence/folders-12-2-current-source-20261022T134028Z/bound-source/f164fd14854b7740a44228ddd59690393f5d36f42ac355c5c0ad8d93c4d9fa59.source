using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>One source-streamed mapped replay staged under a durable request identity.</summary>
internal sealed record ProjectionMappedReplayStageResult(
    NamedProjectionRebuildResult Result,
    ProjectionDispatchRequest? Header);
