using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Contracts.Messages;

/// <summary>
/// Serializes <see cref="MessageType"/> as a plain JSON string (not an object).
/// Writes <see cref="MessageType.ToString()"/> on serialize, calls <see cref="MessageType.Parse(string)"/> on deserialize.
/// </summary>
public sealed class MessageTypeJsonConverter : JsonConverter<MessageType> {
    /// <inheritdoc/>
    public override bool HandleNull => true;

    /// <inheritdoc/>
    public override MessageType? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if (reader.TokenType == JsonTokenType.Null) {
            throw new JsonException("MessageType JSON value cannot be null. Expected a string in the format {domain}-{name}-v{ver}.");
        }

        if (reader.TokenType != JsonTokenType.String) {
            throw new JsonException($"MessageType JSON value must be a string, but was {reader.TokenType}.");
        }

        string? value = reader.GetString() ?? throw new JsonException("MessageType JSON value cannot be null. Expected a string in the format {domain}-{name}-v{ver}.");
        return MessageType.Parse(value);
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, MessageType value, JsonSerializerOptions options) {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);
        writer.WriteStringValue(value.ToString());
    }
}
