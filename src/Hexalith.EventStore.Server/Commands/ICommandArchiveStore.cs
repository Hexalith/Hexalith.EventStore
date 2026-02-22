
using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.Server.Commands;
/// <summary>
/// Abstraction for command archive read/write operations against a state store.
/// </summary>
public interface ICommandArchiveStore {
    /// <summary>
    /// Writes an archived command to the state store.
    /// </summary>
    Task WriteCommandAsync(
        string tenantId,
        string correlationId,
        ArchivedCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads an archived command from the state store.
    /// Returns null if the key does not exist or has expired.
    /// </summary>
    Task<ArchivedCommand?> ReadCommandAsync(
        string tenantId,
        string correlationId,
        CancellationToken cancellationToken = default);
}
