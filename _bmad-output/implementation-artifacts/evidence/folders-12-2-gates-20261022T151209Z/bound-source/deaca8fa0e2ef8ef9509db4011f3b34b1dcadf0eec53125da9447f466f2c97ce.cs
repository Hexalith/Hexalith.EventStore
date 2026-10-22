namespace Hexalith.EventStore.Server.Commands;

/// <summary>
/// Maps one correlation identifier to a live command message identifier.
/// </summary>
/// <param name="MessageId">The command message identifier.</param>
/// <param name="ExpiresAt">When this mapping expires.</param>
public sealed record CommandCorrelationIndexEntry(string MessageId, DateTimeOffset ExpiresAt);
