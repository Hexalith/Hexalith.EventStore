using System.Runtime.Serialization;
using System.Text.Json;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// Result returned by a projection actor's QueryAsync method.
/// The Payload is an opaque JsonElement containing the projection data.
/// </summary>
/// <remarks>
/// [DataContract]/[DataMember] required for DAPR actor proxy serialization
/// (matches CommandProcessingResult pattern).
/// </remarks>
[DataContract]
public record QueryResult(
    [property: DataMember] bool Success,
    [property: DataMember] JsonElement Payload,
    [property: DataMember] string? ErrorMessage = null,
    [property: DataMember] string? ProjectionType = null);
