using System.Runtime.Serialization;

namespace Hexalith.EventStore.Contracts.Reminders;

/// <summary>Counts produced by converging one target's reminders with its current intents.</summary>
/// <param name="Armed">Reminders armed by the convergence scan, including future witnesses and lost backoff reminders; excludes queued settlement retries.</param>
/// <param name="Submitted">Due reminders with a durable target receipt, successful audit, and successful Scheduler cancellation.</param>
/// <param name="Cancelled">Obsolete reminders cancelled.</param>
/// <param name="Unresolved">Retained pending work or discovery awaiting recovery, including discovery without a pending witness.</param>
/// <param name="Quarantined">Collisions or malformed evidence retained for operator disposition.</param>
[DataContract]
public sealed record ReminderConvergenceResult(
    [property: DataMember] int Armed,
    [property: DataMember] int Submitted,
    [property: DataMember] int Cancelled,
    [property: DataMember] int Unresolved,
    [property: DataMember] int Quarantined);
