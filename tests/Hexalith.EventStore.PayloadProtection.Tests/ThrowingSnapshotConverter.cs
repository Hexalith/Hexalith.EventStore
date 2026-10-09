using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Throws a supplied exception while registered snapshot state is materialized.</summary>
/// <param name="exception">The exception to throw from deserialization.</param>
internal sealed class ThrowingSnapshotConverter(Exception exception) : JsonConverter<PartySnapshotState>
{
    /// <inheritdoc/>
    public override PartySnapshotState? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => throw exception;

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, PartySnapshotState value, JsonSerializerOptions options)
        => throw new NotSupportedException();
}
