using Hexalith.EventStore.Client.Projections;

namespace Hexalith.EventStore.Server.Tests.Projections;

internal sealed record AggregateIndexReadModel(
    IReadOnlyList<string> AggregateIds,
    DateTimeOffset? ProjectedAt,
    string? ProjectionVersion) : IReadModelFreshness;
