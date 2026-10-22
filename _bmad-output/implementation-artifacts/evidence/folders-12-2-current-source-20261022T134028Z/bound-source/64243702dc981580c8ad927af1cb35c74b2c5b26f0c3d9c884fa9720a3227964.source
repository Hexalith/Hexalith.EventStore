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

        string? value = reader.GetString();
        return value switch
        {
            nameof(QueryResponseProvenance.ProjectionBacked) => QueryResponseProvenance.ProjectionBacked,
            nameof(QueryResponseProvenance.HandlerComputed) => QueryResponseProvenance.HandlerComputed,
            _ => QueryResponseProvenance.Unknown,
        };
    }

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        QueryResponseProvenance value,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStringValue(value switch
        {
            QueryResponseProvenance.ProjectionBacked => nameof(QueryResponseProvenance.ProjectionBacked),
            QueryResponseProvenance.HandlerComputed => nameof(QueryResponseProvenance.HandlerComputed),
            _ => nameof(QueryResponseProvenance.Unknown),
        });
    }
}
