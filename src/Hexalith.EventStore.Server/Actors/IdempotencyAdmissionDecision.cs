using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Defines the safe action for one admission request.</summary>
[DataContract]
public enum IdempotencyAdmissionDecision
{
    /// <summary>The caller owns a new fenced reservation and may begin once.</summary>
    [EnumMember]
    Execute = 1,

    /// <summary>An equivalent reserved or pending request already exists.</summary>
    [EnumMember]
    Pending = 2,

    /// <summary>An equivalent recoverable checkpoint exists.</summary>
    [EnumMember]
    Recoverable = 3,

    /// <summary>An equivalent request has an unknown external outcome.</summary>
    [EnumMember]
    UnknownProviderOutcome = 4,

    /// <summary>An equivalent terminal result may be replayed.</summary>
    [EnumMember]
    Replay = 5,

    /// <summary>The live key is associated with a different canonical intent.</summary>
    [EnumMember]
    Conflict = 6,

    /// <summary>The consumed key's replay result expired.</summary>
    [EnumMember]
    Expired = 7,

    /// <summary>The persisted record cannot be verified safely.</summary>
    [EnumMember]
    Corrupt = 8,
}
