using System.Text.Json;

using Hexalith.EventStore.Server.Projections;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Measures typed source metadata with the pinned actor SDK's default writer and configured type serializer.</summary>
internal static class SourceActorStateBodySize
{
    /// <summary>Matches DaprStateProvider's writer-owned encoding and compact formatting without accessing provider state.</summary>
    internal static long Count<T>(T value, JsonSerializerOptions options)
    {
        using var output = new SizeLimitedWriteStream(long.MaxValue);
        using (var writer = new Utf8JsonWriter(output))
        {
            JsonSerializer.Serialize(writer, value, options);
            writer.Flush();
        }
        return output.Length;
    }
}
