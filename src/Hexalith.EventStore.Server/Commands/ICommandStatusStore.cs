namespace Hexalith.EventStore.Server.Commands;

using Hexalith.EventStore.Contracts.Commands;

/// <summary>
/// Abstraction for command status read/write operations against a state store.
/// </summary>
public interface ICommandStatusStore {
    /// <summary>
    /// Writes a command status record to the state store.
    /// </summary>
    Task WriteStatusAsync(
        string tenantId,
        string correlationId,
        CommandStatusRecord status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a command status record from the state store.
    /// Returns null if the key does not exist or has expired.
    /// </summary>
    Task<CommandStatusRecord?> ReadStatusAsync(
        string tenantId,
        string correlationId,
        CancellationToken cancellationToken = default);
}
