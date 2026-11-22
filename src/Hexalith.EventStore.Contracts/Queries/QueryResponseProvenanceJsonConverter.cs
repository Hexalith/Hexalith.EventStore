using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Contracts.Queries;

/// <summary>
/// Converts query response provenance using its canonical fail-safe wire names.
/// </summary>
public sealed class QueryResponseProvenanceJsonConverter : JsonConverter<QueryResponseProvenance>
{
    /// <inheritdoc />
    public override bool HandleNull => true;

    /// <inheritdoc />
    public override QueryResponseProvenance Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
            {
                reader.Skip();
            }

            return QueryResponseProvenance.Unknown;
        }

        return QueryResponseProvenancePolicy.Parse(reader.GetString());
    }

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        QueryResponseProvenance value,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStringValue(QueryResponseProvenancePolicy.Format(value));
    }
}
