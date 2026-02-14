namespace Hexalith.EventStore.Server.Events;

using Hexalith.EventStore.Contracts.Commands;

/// <summary>
/// Dead-letter message containing full command context, error details, and correlation context.
/// Published to per-tenant dead-letter topic when command processing fails due to infrastructure errors.
/// Rule #13: No stack traces -- exception type + message only.
/// </summary>
/// <param name="Command">The full, unmodified command envelope for replay support.</param>
/// <param name="FailureStage">The CommandStatus value at the time of failure.</param>
/// <param name="ExceptionType">The exception type name (no stack trace per rule #13).</param>
/// <param name="ErrorMessage">The exception message (no stack trace per rule #13).</param>
/// <param name="CorrelationId">The correlation identifier for request tracing.</param>
/// <param name="CausationId">The optional causation identifier.</param>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="Domain">The domain name.</param>
/// <param name="AggregateId">The aggregate identifier.</param>
/// <param name="CommandType">The fully qualified command type name.</param>
/// <param name="FailedAt">Timestamp when the failure occurred.</param>
/// <param name="EventCountAtFailure">Number of events at the time of failure (if applicable).</param>
public record DeadLetterMessage(
    CommandEnvelope Command,
    string FailureStage,
    string ExceptionType,
    string ErrorMessage,
    string CorrelationId,
    string? CausationId,
    string TenantId,
    string Domain,
    string AggregateId,
    string CommandType,
    DateTimeOffset FailedAt,
    int? EventCountAtFailure)
{
    /// <summary>
    /// Creates a DeadLetterMessage from an exception and command context.
    /// Uses outer exception type (not inner) per convention.
    /// </summary>
    /// <param name="command">The original command envelope.</param>
    /// <param name="failureStage">The CommandStatus at the time of failure.</param>
    /// <param name="exception">The infrastructure exception that triggered dead-letter routing.</param>
    /// <param name="eventCount">Optional event count at the time of failure.</param>
    /// <returns>A new DeadLetterMessage with all context extracted.</returns>
    public static DeadLetterMessage FromException(
        CommandEnvelope command,
        CommandStatus failureStage,
        Exception exception,
        int? eventCount = null)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(exception);

        return new DeadLetterMessage(
            Command: command,
            FailureStage: failureStage.ToString(),
            ExceptionType: exception.GetType().Name,
            ErrorMessage: exception.Message,
            CorrelationId: command.CorrelationId,
            CausationId: command.CausationId,
            TenantId: command.TenantId,
            Domain: command.Domain,
            AggregateId: command.AggregateId,
            CommandType: command.CommandType,
            FailedAt: DateTimeOffset.UtcNow,
            EventCountAtFailure: eventCount);
    }
}
