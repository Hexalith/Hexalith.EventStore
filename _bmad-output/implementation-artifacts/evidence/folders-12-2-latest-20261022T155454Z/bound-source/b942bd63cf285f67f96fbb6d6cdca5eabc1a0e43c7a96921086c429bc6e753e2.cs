using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Defines durable tenant/key admission states.</summary>
[DataContract]
public enum IdempotencyAdmissionState
{
    /// <summary>The first writer owns the durable reservation.</summary>
    [EnumMember]
    Reserved = 1,

    /// <summary>The fenced writer crossed the side-effect boundary.</summary>
    [EnumMember]
    Pending = 2,

    /// <summary>A persisted checkpoint permits bounded resume.</summary>
    [EnumMember]
    Recoverable = 3,

    /// <summary>An external effect may have occurred and only read-only reconciliation is safe.</summary>
    [EnumMember]
    UnknownProviderOutcome = 4,

    /// <summary>A replayable deterministic result is finalized.</summary>
    [EnumMember]
    Terminal = 5,

    /// <summary>The result and intent were compacted to consumed-key evidence.</summary>
    [EnumMember]
    Expired = 6,
}
