using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Client.Tests.Aggregates;

/// <summary>Exercises cancellation at every payload-deserialization exit.</summary>
internal sealed class CancellationReplayEventConverter : JsonConverter<CancellationReplayEvent>
{
    /// <inheritdoc/>
    public override CancellationReplayEvent? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        reader.Skip();
        CancellationTestScope scope = CancellationTestScope.Current;
        scope.ConverterReads++;
        scope.OnConverterRead?.Invoke();
        if (scope.ConverterCancellationOutcome > 0)
        {
            scope.Cancellation.Cancel();
        }

        return scope.ConverterCancellationOutcome switch
        {
            2 => null,
            3 => throw new JsonException("Cancelled converter failure."),
            4 => throw new OperationCanceledException(scope.Cancellation.Token),
            _ => new CancellationReplayEvent(),
        };
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, CancellationReplayEvent value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteEndObject();
    }
}
