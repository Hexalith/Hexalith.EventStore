namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>Identifies the explicitly requested experimental disconnect, rather than protocol corruption.</summary>
internal sealed class PostgresFaultDisconnectException() : IOException("Explicitly broken PostgreSQL fault connection.");
