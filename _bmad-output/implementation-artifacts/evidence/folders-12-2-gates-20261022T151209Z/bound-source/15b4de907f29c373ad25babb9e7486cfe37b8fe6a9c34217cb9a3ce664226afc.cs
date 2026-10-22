
using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Server.Events;
/// <summary>
/// Publishes dead-letter messages to per-tenant dead-letter topics.
/// Returns bool -- true on success, false on failure.
/// OperationCanceledException is propagated when cancellation is requested.
/// </summary>
public interface IDeadLetterPublisher {
    /// <summary>
    /// Publishes a dead-letter message for a failed command.
    /// </summary>
    /// <param name="identity">The aggregate identity for topic derivation.</param>
    /// <param name="message">The dead-letter message containing full command context.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>True if published successfully; false if publication failed.</returns>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    Task<bool> PublishDeadLetterAsync(
        AggregateIdentity identity,
        DeadLetterMessage message,
        CancellationToken cancellationToken = default);
}
