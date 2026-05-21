using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>
/// Resolves the EventStore-owned aggregate type for a submitted command.
/// </summary>
public interface ICommandAggregateTypeResolver {
    /// <summary>
    /// Resolves the aggregate type for the command, or <c>null</c> when no catalog entry is available.
    /// </summary>
    /// <param name="command">Command envelope.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolved aggregate type, or <c>null</c> to use the legacy domain fallback.</returns>
    Task<string?> ResolveAsync(CommandEnvelope command, CancellationToken cancellationToken = default);
}
