using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>
/// Cancels the caller while a registered snapshot type is being materialized.
/// </summary>
internal sealed class CancellingSnapshotConverter(CancellationTokenSource source) : JsonConverter<PartySnapshotState>
{
    /// <inheritdoc/>
    public override PartySnapshotState? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        source.Cancel();
        return new PartySnapshotState("Alice", 3);
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, PartySnapshotState value, JsonSerializerOptions options)
        => throw new NotSupportedException();
}
