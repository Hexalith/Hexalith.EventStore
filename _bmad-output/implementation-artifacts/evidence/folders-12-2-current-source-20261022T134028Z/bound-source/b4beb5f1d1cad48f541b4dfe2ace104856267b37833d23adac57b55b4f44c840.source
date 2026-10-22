using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.EventStore.Server.Projections;

namespace Hexalith.EventStore.Server.Tests.Projections;

/// <summary>Observes the actual active streaming writer around the unchanged production digest converter.</summary>
internal sealed class ObservedProjectionMessageIdDigestsConverter(Action<Utf8JsonWriter> observe)
    : JsonConverter<IReadOnlyList<string>>
{
    private readonly ProjectionMessageIdDigestsJsonConverter _inner = new();

    public override IReadOnlyList<string>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => _inner.Read(ref reader, typeToConvert, options);

    public override void Write(Utf8JsonWriter writer, IReadOnlyList<string> value, JsonSerializerOptions options)
    {
        observe(writer);
        _inner.Write(writer, value, options);
        observe(writer);
    }
}
