using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Serializes every command path through the same safe output projection; raw probe output stays in memory.</summary>
internal sealed class QualificationCommandObservationConverter : JsonConverter<CommandObservation>
{
    public override CommandObservation Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var record = JsonSerializer.Deserialize<QualificationCommandRecord>(ref reader, options)!;
        return new(record.Command, record.Started, record.Ended, record.ExitCode, record.StandardOutput, record.StandardError,
            record.OutputHash, record.OutputTruncated, record.DeadlineStarted, record.Interruption, record.OutputProjected);
    }

    public override void Write(Utf8JsonWriter writer, CommandObservation value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, Capture(value), options);

    internal static QualificationCommandRecord Capture(CommandObservation value)
    {
        string output = HistoricalSafeJson.Text(value.StandardOutput), error = HistoricalSafeJson.Error(value.Command, value.StandardError);
        return new(value.Command, value.Started, value.Ended, value.ExitCode, output, error, value.OutputHash, value.OutputTruncated,
            value.DeadlineStarted, value.Interruption, QualificationRuntime.Hash(Encoding.UTF8.GetBytes(output + error)), value.OriginalBytesRedacted || output != value.StandardOutput || error != value.StandardError);
    }
}
