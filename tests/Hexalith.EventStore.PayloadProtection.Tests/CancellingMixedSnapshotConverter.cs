using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Cancels the caller while materializing the authenticated mixed-root snapshot.</summary>
internal sealed class CancellingMixedSnapshotConverter(CancellationTokenSource source) : JsonConverter<JsonElement>
{
    /// <inheritdoc/>
    public override JsonElement Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement state = document.RootElement.Clone();
        source.Cancel();
        return state;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, JsonElement value, JsonSerializerOptions options)
        => throw new NotSupportedException();
}
