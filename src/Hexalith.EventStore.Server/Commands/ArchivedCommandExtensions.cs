
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
    /// Validates that critical fields are present -- corrupted archives from state store deserialization
    /// could have null fields despite non-nullable declarations.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the archived command has missing critical fields.</exception>
    public static SubmitCommand ToSubmitCommand(this ArchivedCommand archived, string correlationId, string expectedTenant) {
        ArgumentNullException.ThrowIfNull(archived);
        ArgumentNullException.ThrowIfNull(correlationId);
        ArgumentNullException.ThrowIfNull(expectedTenant);

        if (string.IsNullOrWhiteSpace(archived.Tenant)) {
            throw new InvalidOperationException(
                $"Archived command for correlation '{correlationId}' has missing Tenant. The archive may be corrupted.");
        }

        if (!string.Equals(archived.Tenant, expectedTenant, StringComparison.Ordinal)) {
            throw new InvalidOperationException(
                $"Archived command for correlation '{correlationId}' has tenant mismatch. Expected '{expectedTenant}', got '{archived.Tenant}'. The archive may be corrupted.");
        }

        if (string.IsNullOrWhiteSpace(archived.Domain)) {
            throw new InvalidOperationException(
                $"Archived command for correlation '{correlationId}' has missing Domain. The archive may be corrupted.");
        }

        if (string.IsNullOrWhiteSpace(archived.AggregateId)) {
            throw new InvalidOperationException(
                $"Archived command for correlation '{correlationId}' has missing AggregateId. The archive may be corrupted.");
        }

        if (archived.Payload is null || archived.Payload.Length == 0) {
            throw new InvalidOperationException(
                $"Archived command for correlation '{correlationId}' has missing or empty Payload. The archive may be corrupted.");
        }

        if (string.IsNullOrWhiteSpace(archived.CommandType)) {
            throw new InvalidOperationException(
                $"Archived command for correlation '{correlationId}' has missing CommandType. The archive may be corrupted.");
        }

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
