using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>A custom shape used to prove converter rejection and one-serialization behavior.</summary>
internal sealed class PolicyLifecycleConverter : JsonConverter<PolicyLifecyclePayload>
{
    /// <summary>Gets the actual serialization count.</summary>
    internal int WriteCount { get; private set; }

    /// <inheritdoc/>
    public override PolicyLifecyclePayload Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, PolicyLifecyclePayload value, JsonSerializerOptions options)
    {
        WriteCount++;
        writer.WriteStringValue("converter-fixture");
    }
}
