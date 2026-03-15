
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;

namespace Hexalith.EventStore.Server.Commands;
/// <summary>
/// Extension methods for converting between <see cref="ArchivedCommand"/> and <see cref="SubmitCommand"/>.
/// Centralizes field mapping to avoid duplication across handler and controller.
/// </summary>
public static class ArchivedCommandExtensions {
    /// <summary>
    /// Creates an <see cref="ArchivedCommand"/> from a <see cref="SubmitCommand"/>.
    /// </summary>
    public static ArchivedCommand ToArchivedCommand(this SubmitCommand command) {
        ArgumentNullException.ThrowIfNull(command);
        return new ArchivedCommand(
            command.Tenant,
            command.Domain,
            command.AggregateId,
            command.CommandType,
            command.Payload,
            command.Extensions,
            DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Reconstructs a <see cref="SubmitCommand"/> from an <see cref="ArchivedCommand"/> for replay.
    /// </summary>
    public static SubmitCommand ToSubmitCommand(this ArchivedCommand archived, string correlationId) {
        ArgumentNullException.ThrowIfNull(archived);
        ArgumentNullException.ThrowIfNull(correlationId);
        return new SubmitCommand(
            MessageId: Guid.NewGuid().ToString(),
            Tenant: archived.Tenant,
            Domain: archived.Domain,
            AggregateId: archived.AggregateId,
            CommandType: archived.CommandType,
            Payload: archived.Payload,
            CorrelationId: correlationId,
            UserId: "system",
            Extensions: archived.Extensions);
    }
}
