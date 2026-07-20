using System.Runtime.Serialization;

namespace Hexalith.EventStore.Contracts.Commands;

/// <summary>Identifies a server-registered replay-result retention tier.</summary>
[DataContract]
public enum IdempotencyReplayRetentionTier
{
    /// <summary>Standard mutation replay results are retained for exactly 24 hours.</summary>
    [EnumMember]
    Mutation = 1,

    /// <summary>Commit replay results are retained for seven calendar years.</summary>
    [EnumMember]
    Commit = 2,
}
