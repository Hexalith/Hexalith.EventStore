using System.Runtime.Serialization;

namespace Hexalith.EventStore.Contracts.Reminders;

/// <summary>Counts produced by converging one target's reminders with its current intents.</summary>
/// <param name="Armed">Future reminders registered or re-registered with the scheduler.</param>
/// <param name="Submitted">Due reminders that reached a durable target receipt.</param>
/// <param name="Cancelled">Obsolete reminders cancelled.</param>
/// <param name="Unresolved">Reminders whose pending state is retained without a durable outcome.</param>
/// <param name="Quarantined">Collisions or malformed evidence retained for operator disposition.</param>
[DataContract]
public sealed record ReminderConvergenceResult(
    [property: DataMember] int Armed,
    [property: DataMember] int Submitted,
    [property: DataMember] int Cancelled,
    [property: DataMember] int Unresolved,
    [property: DataMember] int Quarantined);
