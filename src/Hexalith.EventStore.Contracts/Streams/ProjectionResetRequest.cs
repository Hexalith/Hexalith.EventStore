namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>
/// EventStore-side projection reset request.
/// </summary>
/// <param name="FromPosition">Optional position to reset from.</param>
public sealed record ProjectionResetRequest(long? FromPosition);
