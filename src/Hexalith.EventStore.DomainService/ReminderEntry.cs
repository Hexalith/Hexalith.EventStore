using System.Text.Json.Serialization;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// The persisted witness of one reminder. It carries identifiers and a payload digest only; the payload stays
/// in the domain stream and is re-folded before every submission.
/// </summary>
/// <param name="ReminderName">The <c>date-</c> or <c>expiry-</c> reminder name.</param>
/// <param name="ScheduleToken">The <c>wrs-</c> schedule token carried by the name.</param>
/// <param name="Kind">The reminder kind.</param>
/// <param name="DueUtc">The due instant.</param>
/// <param name="ScheduleRevision">The domain schedule revision.</param>
/// <param name="SourceDomain">The domain of the stream whose event created the intent.</param>
/// <param name="SourceAggregate">The aggregate of the stream whose event created the intent.</param>
/// <param name="SourceSequence">The envelope sequence of the event that created the intent.</param>
/// <param name="PayloadType">The domain payload type name.</param>
/// <param name="PayloadDigest">The Crockford Base32 SHA-256 digest of the payload bytes.</param>
/// <param name="Status">The persisted lifecycle status.</param>
/// <param name="Attempts">The number of uncertain submission attempts.</param>
/// <param name="LastReasonCode">The bounded reason code of the last non-durable outcome.</param>
/// <param name="UpdatedAt">When the witness last changed.</param>
internal sealed record ReminderEntry(
    string ReminderName,
    string ScheduleToken,
    string Kind,
    DateTimeOffset DueUtc,
    long ScheduleRevision,
    string SourceDomain,
    string SourceAggregate,
    long SourceSequence,
    string PayloadType,
    string PayloadDigest,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] ReminderEntryStatus Status,
    int Attempts,
    string? LastReasonCode,
    DateTimeOffset UpdatedAt);
