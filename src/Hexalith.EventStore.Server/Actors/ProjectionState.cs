
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
/// State is stored as <c>byte[]</c> (UTF-8 JSON) because <c>JsonElement</c>
/// is not serializable by <c>DataContractSerializer</c> used by DAPR actor remoting.
/// </remarks>
[DataContract]
public record ProjectionState(
    [property: DataMember] string ProjectionType,
    [property: DataMember] string TenantId,
    [property: DataMember] byte[] StateBytes) {
    /// <summary>
    /// Deserializes <see cref="StateBytes"/> to a <see cref="JsonElement"/>.
    /// </summary>
    public JsonElement GetState() => JsonSerializer.Deserialize<JsonElement>(StateBytes);

    /// <summary>
    /// Creates a <see cref="ProjectionState"/> from a <see cref="JsonElement"/>.
    /// </summary>
    public static ProjectionState FromJsonElement(string projectionType, string tenantId, JsonElement state)
        => new(projectionType, tenantId, JsonSerializer.SerializeToUtf8Bytes(state));
}
