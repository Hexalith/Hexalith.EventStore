using System.Runtime.Serialization;

namespace Hexalith.EventStore.Contracts.Reminders;

/// <summary>The stream whose current reminder intents are converged.</summary>
/// <param name="Tenant">Canonical lowercase tenant identifier.</param>
/// <param name="Domain">Canonical lowercase domain of the target stream.</param>
/// <param name="Aggregate">Aggregate identifier of the target stream.</param>
[DataContract]
public sealed record ReminderTarget(
    [property: DataMember] string Tenant,
    [property: DataMember] string Domain,
    [property: DataMember] string Aggregate);
