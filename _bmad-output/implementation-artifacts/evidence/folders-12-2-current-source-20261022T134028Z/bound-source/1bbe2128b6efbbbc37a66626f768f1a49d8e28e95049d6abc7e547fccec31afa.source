using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Contracts.Queries;

/// <summary>
/// Describes authoritative projection lifecycle evidence without collapsing operational states into a Boolean.
/// </summary>
[DataContract]
[JsonConverter(typeof(ProjectionLifecycleStateJsonConverter))]
public enum ProjectionLifecycleState
{
    /// <summary>
    /// No authoritative projection lifecycle evidence is available.
    /// </summary>
    [EnumMember]
    Unknown = 0,

    /// <summary>
    /// The authoritative projection is current.
    /// </summary>
    [EnumMember]
    Current = 1,

    /// <summary>
    /// The authoritative projection is stale.
    /// </summary>
    [EnumMember]
    Stale = 2,

    /// <summary>
    /// The authoritative projection is being rebuilt.
    /// </summary>
    [EnumMember]
    Rebuilding = 3,

    /// <summary>
    /// The projection is serviceable with a known degraded dependency or capability.
    /// </summary>
    [EnumMember]
    Degraded = 4,

    /// <summary>
    /// The authoritative projection or its storage is unavailable.
    /// </summary>
    [EnumMember]
    Unavailable = 5,

    /// <summary>
    /// The response contains explicitly non-authoritative local-only evidence.
    /// </summary>
    [EnumMember]
    LocalOnly = 6,
}
