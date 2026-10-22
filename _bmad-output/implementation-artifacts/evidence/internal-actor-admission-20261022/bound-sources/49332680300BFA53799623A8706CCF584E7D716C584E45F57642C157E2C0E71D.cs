using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Returns a support-safe exact source classification and optional redirect proof.</summary>
[DataContract]
internal sealed record IdempotencyLegacySourceInspection(
    [property: DataMember] IdempotencyLegacySourceDecision Decision,
    [property: DataMember] string? RedirectDigest = null);
