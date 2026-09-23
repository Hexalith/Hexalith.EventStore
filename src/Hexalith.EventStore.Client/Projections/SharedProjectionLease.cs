namespace Hexalith.EventStore.Client.Projections;

/// <summary>Writer admission token bound to one durable tenant/family epoch.</summary>
public sealed record SharedProjectionLease(string ScopeHash, string WriterId, long Epoch);
