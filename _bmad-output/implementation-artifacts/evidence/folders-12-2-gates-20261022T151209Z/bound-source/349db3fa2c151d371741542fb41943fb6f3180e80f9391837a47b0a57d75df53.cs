using Hexalith.EventStore.Client.Projections;

namespace Hexalith.EventStore.Server.Tests.Projections;

internal sealed record AggregateReadModel(
    string Id,
    string Status,
    int EventCount,
    DateTimeOffset? ProjectedAt,
    string? ProjectionVersion) : IReadModelFreshness;
