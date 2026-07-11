using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Contracts.Queries;

/// <summary>
/// Identifies the authoritative route that produced a query response.
/// </summary>
[DataContract]
[JsonConverter(typeof(QueryResponseProvenanceJsonConverter))]
public enum QueryResponseProvenance
{
    /// <summary>
    /// The producing route is missing or could not be recognized safely.
    /// </summary>
    [EnumMember]
    Unknown = 0,

    /// <summary>
    /// The response was produced by the projection-actor route.
    /// </summary>
    [EnumMember]
    ProjectionBacked = 1,

    /// <summary>
    /// The response was produced by a domain query handler.
    /// </summary>
    [EnumMember]
    HandlerComputed = 2,
}
