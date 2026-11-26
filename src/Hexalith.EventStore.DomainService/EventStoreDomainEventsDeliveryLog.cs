using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Source-generated domain-event delivery refusals. They carry a bounded reason code, the admitted message
/// identifier and at most an exception type name: never payloads, tenant evidence, tokens or exception text.
/// </summary>
internal static partial class EventStoreDomainEventsDeliveryLog
{
    /// <summary>The logger category used by the generic domain-event callback.</summary>
    internal const string Category = "Hexalith.EventStore.DomainService.EventStoreDomainEvents";

    [LoggerMessage(EventId = 200401, Level = LogLevel.Warning,
        Message = "Domain-event delivery refused before admission: ReasonCode={ReasonCode}")]
    public static partial void Refused(ILogger logger, string reasonCode);

    [LoggerMessage(EventId = 200402, Level = LogLevel.Warning,
        Message = "Domain-event delivery left retryable: ReasonCode={ReasonCode}, MessageId={MessageId}")]
    public static partial void Retryable(ILogger logger, string reasonCode, string messageId);

    [LoggerMessage(EventId = 200403, Level = LogLevel.Warning,
        Message = "Domain-event delivery failure was not retained: ReasonCode={ReasonCode}, FailureReason={FailureReason}, MessageId={MessageId}")]
    public static partial void NotRetained(ILogger logger, string reasonCode, string failureReason, string messageId);

    [LoggerMessage(EventId = 200404, Level = LogLevel.Warning,
        Message = "Domain-event delivery failed: ReasonCode={ReasonCode}, MessageId={MessageId}, ExceptionType={ExceptionType}")]
    public static partial void Failed(ILogger logger, string reasonCode, string? messageId, string exceptionType);
}
