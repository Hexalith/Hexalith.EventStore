namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>
/// EventStore-side projection replay request.
/// </summary>
/// <param name="FromPosition">The starting stream position.</param>
/// <param name="ToPosition">The inclusive ending stream position.</param>
public sealed record ProjectionReplayRequest(long FromPosition, long ToPosition);
