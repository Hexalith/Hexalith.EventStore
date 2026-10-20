
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.EventStore.Contracts.Projections;

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
    /// <summary>Gets optional group-filter scope retained with the selected model; it changes no routing or model identity.</summary>
    [DataMember(Order = 1, EmitDefaultValue = false)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NotificationGroupScope { get; init; }

    /// <summary>Gets copied, contract-bounded hint metadata retained for post-persistence publication and replay.</summary>
    [DataMember(Order = 1, EmitDefaultValue = false)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? NotificationMetadata { get; init; }

    /// <summary>
    /// Deserializes <see cref="StateBytes"/> to a <see cref="JsonElement"/>.
    /// </summary>
    public JsonElement GetState() => JsonSerializer.Deserialize<JsonElement>(StateBytes);

    /// <summary>
    /// Creates a <see cref="ProjectionState"/> from a <see cref="JsonElement"/>.
    /// </summary>
    public static ProjectionState FromJsonElement(string projectionType, string tenantId, JsonElement state)
        => new(projectionType, tenantId, JsonSerializer.SerializeToUtf8Bytes(state));

    /// <summary>Creates actor-owned state from a domain response using the server's trusted tenant.</summary>
    /// <param name="response">The projection response, including optional non-authoritative hint detail.</param>
    /// <param name="tenantId">The tenant selected and authorized by the server.</param>
    /// <returns>The model and a bounded copy of its optional notification detail.</returns>
    public static ProjectionState FromProjectionResponse(ProjectionResponse response, string tenantId)
    {
        ArgumentNullException.ThrowIfNull(response);
        return (FromJsonElement(response.ProjectionType, tenantId, response.State) with
        {
            NotificationGroupScope = response.NotificationGroupScope,
            NotificationMetadata = response.NotificationMetadata is null ? null : ProjectionChangedMetadataLimits.Bound(
                response.NotificationMetadata, ProjectionChangedMetadataLimits.MaximumEntries,
                ProjectionChangedMetadataLimits.MaximumSerializedUtf8Bytes)
                .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal),
        }).WithBoundedNotification();
    }

    /// <summary>Copies and bounds optional hints without altering model bytes, routing or authoritative fingerprints.</summary>
    /// <returns>The state with normalized scope and a bounded, independently owned metadata map.</returns>
    public ProjectionState WithBoundedNotification()
    {
        string? scope = string.IsNullOrWhiteSpace(NotificationGroupScope) ? null : NotificationGroupScope;
        if (scope is not null && (scope.Length > 64 || scope.Contains(':', StringComparison.Ordinal)))
        {
            throw new ArgumentException("Notification group scope must be at most 64 characters and contain no colon.", nameof(NotificationGroupScope));
        }
        if (scope is null && NotificationMetadata is null)
        {
            return this;
        }

        IReadOnlyDictionary<string, string> bounded = ProjectionChangedMetadataLimits.Bound(
            NotificationMetadata,
            ProjectionChangedMetadataLimits.MaximumEntries,
            ProjectionChangedMetadataLimits.MaximumSerializedUtf8Bytes);
        return this with
        {
            NotificationGroupScope = scope,
            NotificationMetadata = bounded.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal),
        };
    }
}
