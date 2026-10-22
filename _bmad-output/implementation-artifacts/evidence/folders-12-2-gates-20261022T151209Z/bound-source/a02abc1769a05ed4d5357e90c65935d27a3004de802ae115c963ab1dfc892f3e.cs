using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.EventStore.Client.Tests.Aggregates;

namespace Hexalith.EventStore.Client.Tests.Handlers;

/// <summary>Runs a per-test callback while the first event's private payload is being deserialized.</summary>
internal sealed class CommandReplayDeserializationEventConverter : JsonConverter<CommandReplayDeserializationEvent>
{
    /// <inheritdoc/>
    public override CommandReplayDeserializationEvent Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        int amount = document.RootElement.GetProperty("amount").GetInt32();
        LegacyReplayMutationScope scope = LegacyReplayMutationScope.Current;
        Action? callback = scope.OnFirstApply;
        scope.OnFirstApply = null;
        callback?.Invoke();
        return new CommandReplayDeserializationEvent(amount);
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, CommandReplayDeserializationEvent value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("amount", value.Amount);
        writer.WriteEndObject();
    }
}
