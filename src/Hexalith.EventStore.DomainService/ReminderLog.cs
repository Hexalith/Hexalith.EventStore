using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Source-generated reminder log events. They carry digests, reason codes, and counts only: never tenant or
/// aggregate identifiers, payloads, tokens, or exception messages and stack traces.
/// </summary>
internal static partial class ReminderLog
{
    [LoggerMessage(EventId = 200200, Level = LogLevel.Debug,
        Message = "Reminder armed: ActorId={ActorId}, ReminderName={ReminderName}")]
    public static partial void Armed(ILogger logger, string actorId, string reminderName);

    [LoggerMessage(EventId = 200201, Level = LogLevel.Warning,
        Message = "Reminder arming failed; pending state is retained: ActorId={ActorId}, ReminderName={ReminderName}, ExceptionType={ExceptionType}")]
    public static partial void ArmFailed(ILogger logger, string actorId, string reminderName, string exceptionType);

    [LoggerMessage(EventId = 200202, Level = LogLevel.Information,
        Message = "Reminder submitted: ActorId={ActorId}, ReminderName={ReminderName}, EffectId={EffectId}, TargetDisposition={TargetDisposition}, Replayed={Replayed}")]
    public static partial void Submitted(ILogger logger, string actorId, string reminderName, string effectId, string targetDisposition, bool replayed);

    [LoggerMessage(EventId = 200203, Level = LogLevel.Warning,
        Message = "Reminder retained for retry: ActorId={ActorId}, ReminderName={ReminderName}, ReasonCode={ReasonCode}, Attempts={Attempts}")]
    public static partial void Retrying(ILogger logger, string actorId, string reminderName, string reasonCode, int attempts);

    [LoggerMessage(EventId = 200204, Level = LogLevel.Information,
        Message = "Stale reminder witness ignored and cancelled: ActorId={ActorId}, ReminderName={ReminderName}")]
    public static partial void Stale(ILogger logger, string actorId, string reminderName);

    [LoggerMessage(EventId = 200205, Level = LogLevel.Debug,
        Message = "Obsolete reminder cancelled: ActorId={ActorId}, ReminderName={ReminderName}")]
    public static partial void Cancelled(ILogger logger, string actorId, string reminderName);

    [LoggerMessage(EventId = 200206, Level = LogLevel.Warning,
        Message = "Reminder callback admission denied; pending state is retained: ActorId={ActorId}, ReminderName={ReminderName}, ReasonCode={ReasonCode}")]
    public static partial void Denied(ILogger logger, string actorId, string reminderName, string reasonCode);

    [LoggerMessage(EventId = 200207, Level = LogLevel.Error,
        Message = "Reminder evidence quarantined: ActorId={ActorId}, Subject={Subject}, ReasonCode={ReasonCode}")]
    public static partial void Quarantined(ILogger logger, string actorId, string subject, string reasonCode);

    [LoggerMessage(EventId = 200208, Level = LogLevel.Warning,
        Message = "Reminder callback has no persisted witness and was cancelled: ActorId={ActorId}, ReminderName={ReminderName}")]
    public static partial void Orphan(ILogger logger, string actorId, string reminderName);

    [LoggerMessage(EventId = 200209, Level = LogLevel.Warning,
        Message = "Reminder actor call denied at the app channel: ReasonCode={ReasonCode}")]
    public static partial void OriginDenied(ILogger logger, string reasonCode);

    [LoggerMessage(EventId = 200210, Level = LogLevel.Information,
        Message = "Reminder reconciliation pass completed: Tenants={Tenants}, Candidates={Candidates}, Armed={Armed}, Submitted={Submitted}, Cancelled={Cancelled}, Unresolved={Unresolved}, Quarantined={Quarantined}, Incomplete={Incomplete}")]
    public static partial void PassCompleted(ILogger logger, int tenants, int candidates, int armed, int submitted, int cancelled, int unresolved, int quarantined, int incomplete);

    [LoggerMessage(EventId = 200211, Level = LogLevel.Warning,
        Message = "Reminder reconciliation could not converge a candidate: ActorId={ActorId}, ExceptionType={ExceptionType}")]
    public static partial void CandidateFailed(ILogger logger, string actorId, string exceptionType);

    [LoggerMessage(EventId = 200212, Level = LogLevel.Warning,
        Message = "Reminder reconciliation scan is incomplete: Scope={Scope}, ExceptionType={ExceptionType}")]
    public static partial void ScanFailed(ILogger logger, string scope, string exceptionType);

    [LoggerMessage(EventId = 200213, Level = LogLevel.Warning,
        Message = "Reminder reconciliation is disabled; readiness stays degraded")]
    public static partial void ReconciliationDisabled(ILogger logger);

    [LoggerMessage(EventId = 200214, Level = LogLevel.Warning,
        Message = "Reminder state change failed closed: ActorId={ActorId}, ReasonCode={ReasonCode}")]
    public static partial void FailedClosed(ILogger logger, string actorId, string reasonCode);

    [LoggerMessage(EventId = 200215, Level = LogLevel.Warning,
        Message = "Reminder cancellation failed; a later callback will cancel it again: ActorId={ActorId}, ReminderName={ReminderName}, ExceptionType={ExceptionType}")]
    public static partial void CancelFailed(ILogger logger, string actorId, string reminderName, string exceptionType);

    [LoggerMessage(EventId = 200216, Level = LogLevel.Warning,
        Message = "Reminder callback failed before a durable outcome; the witness is retained: ActorId={ActorId}, ReminderName={ReminderName}, ExceptionType={ExceptionType}")]
    public static partial void CallbackFailed(ILogger logger, string actorId, string reminderName, string exceptionType);

    [LoggerMessage(EventId = 200217, Level = LogLevel.Warning,
        Message = "Reminder audit disposition could not be written: ActorId={ActorId}, Subject={Subject}, ExceptionType={ExceptionType}")]
    public static partial void AuditWriteFailed(ILogger logger, string actorId, string subject, string exceptionType);

    [LoggerMessage(EventId = 200218, Level = LogLevel.Debug,
        Message = "Reminder lookup did not confirm the scheduler holds it; re-arming: ActorId={ActorId}, ReminderName={ReminderName}, ExceptionType={ExceptionType}")]
    public static partial void LookupFailed(ILogger logger, string actorId, string reminderName, string exceptionType);
}
