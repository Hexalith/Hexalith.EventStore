using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Produces one oversized scalar in a single converter call.</summary>
internal sealed class OversizedScalarConverter : JsonConverter<OversizedScalarPayload>
{
    /// <summary>Gets the observed conversion count.</summary>
    internal int Writes { get; private set; }
    /// <inheritdoc/>
    public override OversizedScalarPayload? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotSupportedException();
    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, OversizedScalarPayload value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(value); Writes++; writer.WriteStringValue(value.Value);
    }
}
