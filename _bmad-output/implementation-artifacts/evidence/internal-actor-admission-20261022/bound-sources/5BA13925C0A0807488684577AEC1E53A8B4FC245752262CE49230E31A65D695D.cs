using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Identifies the only protected operation a current admission may authorize.</summary>
[DataContract]
public enum IdempotencyExecutionPurpose
{
    /// <summary>The current pending fence may cross mutation and side-effect boundaries.</summary>
    [EnumMember]
    Execute = 1,

    /// <summary>The current unknown-outcome fence may perform read-only reconciliation.</summary>
    [EnumMember]
    Reconcile = 2,
}
