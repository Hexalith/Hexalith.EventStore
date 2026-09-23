namespace Hexalith.EventStore.Client.Projections;

/// <summary>A failed source position and its durable consecutive fold-attempt count.</summary>
public sealed record SharedProjectionFailureStatus(string SourceStream, long SourcePosition, int FailureCount);
