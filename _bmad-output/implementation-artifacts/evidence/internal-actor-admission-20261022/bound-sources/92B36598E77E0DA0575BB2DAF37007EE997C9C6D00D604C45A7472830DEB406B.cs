
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Diagnostics;

namespace Hexalith.EventStore.Server.Events;
/// <summary>
/// Dead-letter message containing full command context, error details, and correlation context.
/// Published to per-tenant dead-letter topic when command processing fails due to infrastructure errors.
/// Rule #13: No stack traces -- exception type + message only.
/// </summary>
/// <param name="Command">The full, unmodified command envelope for replay support.</param>
/// <param name="FailureStage">The CommandStatus value at the time of failure.</param>
/// <param name="ExceptionType">The exception type name (no stack trace per rule #13).</param>
/// <param name="ErrorMessage">The safe diagnostic message (no stack trace or provider text per rule #13).</param>
/// <param name="CorrelationId">The correlation identifier for request tracing.</param>
/// <param name="CausationId">The optional causation identifier.</param>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="Domain">The domain name.</param>
/// <param name="AggregateId">The aggregate identifier.</param>
/// <param name="CommandType">The fully qualified command type name.</param>
/// <param name="FailedAt">Timestamp when the failure occurred.</param>
/// <param name="EventCountAtFailure">Number of events at the time of failure (if applicable).</param>
/// <param name="ReplayEligible">
/// Whether <see cref="Command"/> is a faithful, replayable copy of the original command.
/// Infrastructure dead-letters carry the original envelope and stay replay-eligible; the Story 4.4
/// drain-exhaustion sink carries a reduced, reconstructed envelope with no original payload and is
/// therefore explicitly marked <c>false</c>. Trailing-optional so older persisted or in-flight
/// messages still deserialize as replay-eligible.
/// </param>
/// <param name="ReasonCode">The stable bounded reason code, or <c>null</c> for legacy messages.</param>
/// <param name="StartSequence">The first persisted sequence of the affected range, when known.</param>
/// <param name="EndSequence">The last persisted sequence of the affected range, when known.</param>
/// <param name="DrainAttempts">The number of drain attempts made before exhaustion, when applicable.</param>
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
    int? EventCountAtFailure,
    bool ReplayEligible = true,
    string? ReasonCode = null,
    long? StartSequence = null,
    long? EndSequence = null,
    int? DrainAttempts = null) {
    /// <summary>
    /// The payload placeholder carried by a reduced envelope. A drain-exhaustion dead-letter never
    /// has the original command bytes, so the envelope is empty and not replay-eligible.
    /// </summary>
    private static readonly byte[] _reducedPayload = [];

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
        int? eventCount = null) {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(exception);

        return new DeadLetterMessage(
            Command: command,
            FailureStage: failureStage.ToString(),
            ExceptionType: exception.GetType().Name,
            ErrorMessage: ProtectedDataDiagnosticRedactor.RedactException(exception, failureStage.ToString()),
            CorrelationId: command.CorrelationId,
            CausationId: command.CausationId,
            TenantId: command.TenantId,
            Domain: command.Domain,
            AggregateId: command.AggregateId,
            CommandType: command.CommandType,
            FailedAt: DateTimeOffset.UtcNow,
            EventCountAtFailure: eventCount);
    }

    /// <summary>
    /// Story 4.4: creates the exhaustion-sink message for a committed range whose bounded drain
    /// budget ran out. The original command envelope is no longer available at drain time, so the
    /// message carries a <em>reduced</em> envelope reconstructed from the drain record: it has an
    /// empty payload, no user identity, and is explicitly marked not replay-eligible so no consumer
    /// mistakes it for a replayable command.
    /// </summary>
    /// <param name="identity">The aggregate identity that owns the committed range.</param>
    /// <param name="record">The exhausted drain record.</param>
    /// <param name="trackingId">The drain tracking identifier the reminder fired with.</param>
    /// <param name="drainAttempts">The number of attempts made before exhaustion.</param>
    /// <returns>A reduced, non-replay-eligible dead-letter message.</returns>
    public static DeadLetterMessage FromDrainExhaustion(
        AggregateIdentity identity,
        UnpublishedEventsRecord record,
        string trackingId,
        int drainAttempts) {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(record);
        ArgumentException.ThrowIfNullOrWhiteSpace(trackingId);

        // Fall back to the tracking id when the record predates message-keyed drain records.
        string messageId = record.GetTrackingIdentity(trackingId);

        var reducedCommand = new CommandEnvelope(
            MessageId: messageId,
            TenantId: identity.TenantId,
            Domain: identity.Domain,
            AggregateId: identity.AggregateId,
            CommandType: record.CommandType,
            Payload: _reducedPayload,
            CorrelationId: record.CorrelationId,
            CausationId: null,
            UserId: "system",
            Extensions: null);

        return new DeadLetterMessage(
            Command: reducedCommand,
            FailureStage: CommandStatus.PublishFailed.ToString(),
            ExceptionType: nameof(DrainPublishException),
            ErrorMessage: $"Drain attempts exhausted after {drainAttempts} attempt(s); committed events were never published.",
            CorrelationId: record.CorrelationId,
            CausationId: null,
            TenantId: identity.TenantId,
            Domain: identity.Domain,
            AggregateId: identity.AggregateId,
            CommandType: record.CommandType,
            FailedAt: DateTimeOffset.UtcNow,
            EventCountAtFailure: record.EventCount,
            ReplayEligible: false,
            ReasonCode: DrainReasonCodes.AttemptsExhausted,
            StartSequence: record.StartSequence,
            EndSequence: record.EndSequence,
            DrainAttempts: drainAttempts);
    }
}
