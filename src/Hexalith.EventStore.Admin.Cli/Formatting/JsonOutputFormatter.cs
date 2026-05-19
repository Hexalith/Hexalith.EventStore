using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hexalith.EventStore.Admin.Cli.Formatting;

/// <summary>
/// Formats output as indented JSON with camelCase property names and enum-as-string serialization.
/// </summary>
public class JsonOutputFormatter : IOutputFormatter {
    private const int MaxSanitizeDepth = 64;
    private static readonly Dictionary<string, string> RawToDescriptorProperty = new(StringComparer.OrdinalIgnoreCase) {
        ["payloadJson"] = "payload",
        ["stateJson"] = "state",
        ["eventPayloadJson"] = "eventPayload",
        ["resultingStateJson"] = "resultingState",
        ["oldValue"] = "oldContent",
        ["newValue"] = "newContent",
        ["errorMessage"] = "error",
        ["failureReason"] = "failure",
    };

    /// <inheritdoc/>
    public string Format<T>(T item, IReadOnlyList<ColumnDefinition>? columns = null)
        => JsonSerializer.Serialize(SanitizeNode(JsonSerializer.SerializeToNode(item, JsonDefaults.Options), depth: 0), JsonDefaults.Options);

    /// <inheritdoc/>
    public string FormatCollection<T>(IReadOnlyList<T> items, IReadOnlyList<ColumnDefinition>? columns = null)
        => JsonSerializer.Serialize(SanitizeNode(JsonSerializer.SerializeToNode(items, JsonDefaults.Options), depth: 0), JsonDefaults.Options);

    private static JsonNode? SanitizeNode(JsonNode? node, int depth) {
        if (node is null) {
            return null;
        }

        if (depth >= MaxSanitizeDepth) {
            return JsonValue.Create("[redacted: maximum JSON depth exceeded]");
        }

        if (node is JsonObject obj) {
            var result = new JsonObject();
            foreach (KeyValuePair<string, JsonNode?> property in obj) {
                if (IsRawPropertyWithDescriptor(obj, property.Key)) {
                    continue;
                }

                result[property.Key] = SanitizeNode(property.Value, depth + 1);
            }

            return result;
        }

        if (node is JsonArray array) {
            var result = new JsonArray();
            foreach (JsonNode? item in array) {
                result.Add(SanitizeNode(item, depth + 1));
            }

            return result;
        }

        if (node is JsonValue value && value.TryGetValue(out string? text)) {
            return SafeOutputValueFormatter.SafeText(text);
        }

        return node.DeepClone();
    }

    private static bool IsRawPropertyWithDescriptor(JsonObject obj, string propertyName)
        => RawToDescriptorProperty.TryGetValue(propertyName, out string? descriptorProperty)
        && obj.ContainsKey(descriptorProperty);
}
