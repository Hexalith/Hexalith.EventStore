namespace Hexalith.EventStore.Server.Projections;

/// <summary>One durable, route-scoped source prefix eligible for bounded suffix delivery.</summary>
internal sealed record ProjectionDeliveryAcceptedPrefix(long SequenceNumber, string Fingerprint);
