using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Testing.OriginalSource;

/// <summary>Private enrollment/append of supplied original inputs, with no projection or capture values.</summary>
internal sealed record SyntheticOriginalSourceAppend(string Id, DateTimeOffset ExpiresAt, string RunId,
    string Tenant, string Domain, string AggregateId, ProjectionEventDto[] Events);
