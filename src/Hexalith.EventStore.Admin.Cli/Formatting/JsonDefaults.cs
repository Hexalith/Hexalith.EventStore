using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Admin.Cli.Formatting;

/// <summary>
/// Shared JSON serializer options — single source of truth for both output formatting and API client deserialization.
/// </summary>
public static class JsonDefaults {
    /// <summary>
    /// Gets the shared serializer options with camelCase naming and enum-as-string conversion.
    /// </summary>
    public static readonly JsonSerializerOptions Options = new() {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}
