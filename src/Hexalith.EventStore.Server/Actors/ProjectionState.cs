
using System.Runtime.Serialization;
using System.Text.Json;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// Projection state passed to <see cref="IProjectionWriteActor.UpdateProjectionAsync"/>.
/// Contains the opaque state from the domain service plus routing metadata.
/// </summary>
/// <remarks>
/// Uses [DataContract]/[DataMember] for DAPR actor proxy serialization —
/// same pattern as <see cref="QueryEnvelope"/> and <see cref="QueryResult"/>.
/// Separate from <c>ProjectionResponse</c> (Contracts) because this includes
/// <c>TenantId</c> needed for ETag/SignalR notification.
/// </remarks>
[DataContract]
public record ProjectionState(
    [property: DataMember] string ProjectionType,
    [property: DataMember] string TenantId,
    [property: DataMember] JsonElement State);
