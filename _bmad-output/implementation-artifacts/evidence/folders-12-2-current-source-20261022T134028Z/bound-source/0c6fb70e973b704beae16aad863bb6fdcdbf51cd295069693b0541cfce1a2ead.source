using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Server.Tests.Events;

/// <summary>Throws when state is interpreted before its cached envelope version is admitted.</summary>
public sealed class SnapshotVersionProbeStateConverter : JsonConverter<SnapshotVersionProbeState>
{
    /// <inheritdoc/>
    public override SnapshotVersionProbeState Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options) => throw new InvalidOperationException("Unexpected state interpretation.");

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, SnapshotVersionProbeState value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(value);
        value.SerializationCount++;
        throw new InvalidOperationException("Unexpected state serialization.");
    }
}
