namespace Hexalith.EventStore.Client.Projections;

/// <summary>Durable operation-scoped message identity index entry.</summary>
/// <param name="Sequence">The one original owner sequence allowed for this message.</param>
internal sealed record SharedProjectionCapturedMessageIdentity(long Sequence);
