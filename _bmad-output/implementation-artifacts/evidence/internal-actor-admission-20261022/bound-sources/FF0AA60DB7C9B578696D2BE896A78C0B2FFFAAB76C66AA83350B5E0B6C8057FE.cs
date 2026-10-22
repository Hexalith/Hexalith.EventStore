namespace Hexalith.EventStore.Server.Commands;

/// <summary>
/// Maintains a bounded tenant-scoped mapping from correlation identifiers to message identifiers.
/// </summary>
public interface ICommandCorrelationIndex
{
    /// <summary>Adds one command message mapping idempotently.</summary>
    Task<CommandCorrelationIndexAddOutcome> AddAsync(
        string tenantId,
        string correlationId,
        string messageId,
        CancellationToken cancellationToken = default);

    /// <summary>Resolves a correlation identifier without selecting implicitly when ambiguous.</summary>
    Task<CommandCorrelationResolution> ResolveAsync(
        string tenantId,
        string correlationId,
        CancellationToken cancellationToken = default);
}
