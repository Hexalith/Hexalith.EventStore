namespace Hexalith.EventStore.Server.Tests.Projections;

internal sealed record ProjectionStatusChanged(string AggregateId, string Status);
