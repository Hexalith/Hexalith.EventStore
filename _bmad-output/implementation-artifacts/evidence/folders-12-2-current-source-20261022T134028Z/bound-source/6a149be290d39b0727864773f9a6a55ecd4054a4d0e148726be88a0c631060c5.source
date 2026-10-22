using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Server.Tests.Projections;

/// <summary>Persisted detail/index output carrying the existing batch's selected-owner metadata.</summary>
internal sealed record GuardedTailModel(int Count, string ProjectionVersion, ProjectionOwnerSelection ProjectionOwner)
{
    public string? Padding { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Hexalith.EventStore.Contracts.Streams.NativeSourceProof? NativeSourceProof { get; init; }
}
