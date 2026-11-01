using System.Text.Json;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Retains actual command metadata and safe input-operation names without retaining credential-bearing stdin.</summary>
/// <param name="Record">The actual completed command, including its original input hash.</param>
/// <param name="Operation">The actual JSON input operation, when present.</param>
/// <param name="Kind">The actual JSON input kind, when present.</param>
/// <param name="Serializer">The actual JSON input serializer, when present.</param>
/// <param name="Action">The actual finite actor-client action, when present.</param>
internal sealed record HistoricalExecutionCommand(JsonElement Record, string? Operation, string? Kind, string? Serializer, string? Action = null)
{
    /// <summary>Captures the real in-memory command before raw stdin is intentionally omitted from serialization.</summary>
    internal static HistoricalExecutionCommand Capture(CommandObservation command)
    {
        string? operation = null, kind = null, serializer = null, action = null;
        if (command.Command.Input is not null)
        {
            try
            {
                using JsonDocument input = JsonDocument.Parse(command.Command.Input);
                if (input.RootElement.ValueKind == JsonValueKind.Object)
                {
                    if (input.RootElement.TryGetProperty("operation", out JsonElement field) && field.ValueKind == JsonValueKind.String) operation = field.GetString();
                    if (input.RootElement.TryGetProperty("kind", out field) && field.ValueKind == JsonValueKind.String) kind = field.GetString();
                    if (input.RootElement.TryGetProperty("serializer", out field) && field.ValueKind == JsonValueKind.String) serializer = field.GetString();
                    if (input.RootElement.TryGetProperty("action", out field) && field.ValueKind == JsonValueKind.String) action = field.GetString() is "common" or "fenced-valid" or "fenced-forged" or "trusted-valid" or "trusted-forged" or "trusted-replay" ? field.GetString() : null;
                }
            }
            catch (JsonException) { }
        }
        return new(JsonSerializer.SerializeToElement(command), operation, kind, serializer, action);
    }
}
