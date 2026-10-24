using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Emits one custom shape to inspect the configured writer settings at the canonical callback.</summary>
internal sealed class FormattingPolicyConverter : JsonConverter<PolicyLifecyclePayload>
{
    /// <summary>Gets the actual serialization count.</summary>
    internal int Writes { get; private set; }
    /// <inheritdoc/>
    public override PolicyLifecyclePayload Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotSupportedException();
    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, PolicyLifecyclePayload value, JsonSerializerOptions options)
    {
        Writes++; writer.WriteStartObject(); writer.WriteString("secret", value.Secret);
        writer.WriteStartArray("public"); writer.WriteNumberValue(1); writer.WriteEndArray(); writer.WriteEndObject();
    }
}
