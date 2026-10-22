using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Admin.Abstractions.Models.Dapr;

/// <summary>
/// Describes the result of inspecting one actor instance's state.
/// </summary>
/// <remarks>
/// Ordinals are pinned for wire compatibility. New members must be appended.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DaprActorLookupStatus {
    /// <summary>The lookup path completed and at least one state entry was found.</summary>
    Available = 0,

    /// <summary>The lookup path completed definitively and no known state key was found.</summary>
    NotFound = 1,

    /// <summary>The lookup path failed or was inconclusive, so the actor must not be called inactive.</summary>
    LookupUnavailable = 2,
}
