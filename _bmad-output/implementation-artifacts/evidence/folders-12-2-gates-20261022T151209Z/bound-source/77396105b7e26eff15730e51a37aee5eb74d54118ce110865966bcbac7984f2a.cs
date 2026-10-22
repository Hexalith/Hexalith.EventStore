using Hexalith.EventStore.Client.Projections;

namespace Hexalith.EventStore.Server.Tests.Integration;

internal sealed record PersistedFreshnessReadModel(
    int Value,
    DateTimeOffset? ProjectedAt,
    string? ProjectionVersion) : IReadModelFreshness;
