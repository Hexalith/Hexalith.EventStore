using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Returns protected admission presence for reader-version discovery without mutation.</summary>
/// <param name="Exists">Whether an admission record exists.</param>
/// <param name="Record">The protected record when present.</param>
/// <param name="RedirectActorId">The durable promotion redirect, if present.</param>
/// <param name="Tombstone">The metadata-only consumed-key tombstone when present.</param>
[DataContract]
public sealed record IdempotencyAdmissionInspection(
    [property: DataMember] bool Exists,
    [property: DataMember] IdempotencyAdmissionRecord? Record = null,
    [property: DataMember] string? RedirectActorId = null,
    [property: DataMember] IdempotencyAdmissionTombstone? Tombstone = null);
