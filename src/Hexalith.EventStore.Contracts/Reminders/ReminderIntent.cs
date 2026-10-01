using System.Runtime.Serialization;

namespace Hexalith.EventStore.Contracts.Reminders;

/// <summary>
/// One durable reminder intent that a domain module re-folds from its committed stream. EventStore persists
/// only the intent's identity witness and a payload digest; the payload itself stays in the domain stream.
/// </summary>
/// <param name="Tenant">Canonical lowercase tenant identifier.</param>
/// <param name="TargetDomain">Canonical lowercase domain of the stream the reminder resumes.</param>
/// <param name="TargetAggregate">Aggregate identifier of the stream the reminder resumes; it is the reminder item.</param>
/// <param name="DueUtc">Due instant, expressed with a zero UTC offset.</param>
/// <param name="Kind">Reminder kind from <see cref="Effects.EffectKindCatalog"/> accepted by <see cref="ReminderIdentityCodec"/>.</param>
/// <param name="PayloadType">Non-blank domain-owned type name that tells the intent source how to read <paramref name="Payload"/>.</param>
/// <param name="Payload">Non-null opaque domain payload; EventStore never interprets or stores it.</param>
/// <param name="SourceDomain">Canonical lowercase domain of the stream whose committed event created the intent.</param>
/// <param name="SourceAggregate">Aggregate identifier of the stream whose committed event created the intent.</param>
/// <param name="SourceSequence">Positive EventStore envelope sequence of the committed event that created the intent.</param>
/// <param name="ScheduleRevision">Nonnegative domain schedule revision that, with the due instant, forms the schedule witness.</param>
[DataContract]
public sealed record ReminderIntent(
    [property: DataMember] string Tenant,
    [property: DataMember] string TargetDomain,
    [property: DataMember] string TargetAggregate,
    [property: DataMember] DateTimeOffset DueUtc,
    [property: DataMember] string Kind,
    [property: DataMember] string PayloadType,
    [property: DataMember] byte[] Payload,
    [property: DataMember] string SourceDomain,
    [property: DataMember] string SourceAggregate,
    [property: DataMember] long SourceSequence,
    [property: DataMember] long ScheduleRevision);
