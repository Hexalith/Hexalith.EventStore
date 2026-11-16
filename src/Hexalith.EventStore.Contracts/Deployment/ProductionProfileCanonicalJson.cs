using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Hexalith.EventStore.Contracts.Deployment;

/// <summary>Owner canonical rules shared without changing v1 encoding or null handling.</summary>
internal static partial class ProductionProfileCanonicalJson
{
    /// <summary>Gets private closed serializer conventions shared with structural inspection.</summary>
    internal static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        MaxDepth = 32,
    };

    /// <summary>Encodes the unchanged ordinal owner canonical JSON rules.</summary>
    internal static byte[] CanonicalBytes<T>(T value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
            WriteCanonical(writer, JsonSerializer.SerializeToElement(value, Options), "");
        return stream.ToArray();
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element, string field)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (JsonProperty property in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                writer.WritePropertyName(property.Name);
                WriteCanonical(writer, property.Value, property.Name);
            }
            writer.WriteEndObject();
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            JsonElement[] items = element.EnumerateArray().ToArray();
            if (field != "order")
            {
                if (items.Length > 0 && items[0].ValueKind == JsonValueKind.String)
                    items = items.OrderBy(i => i.GetString(), StringComparer.Ordinal).ToArray();
                else if (items.Length > 0 && items[0].ValueKind == JsonValueKind.Object)
                    items = items.OrderBy(i => i.GetProperty("id").GetString(), StringComparer.Ordinal).ToArray();
            }
            foreach (JsonElement item in items) WriteCanonical(writer, item, field);
            writer.WriteEndArray();
        }
        else element.WriteTo(writer);
    }

    /// <summary>Checks exact required fields, types and version-specific null applicability.</summary>
    internal static void CheckClosed(JsonElement element, Type type, string path, List<ProductionProfileIssue> issues, bool v2 = false)
    {
        if (element.ValueKind == JsonValueKind.Null)
        {
            if (v2 && AllowsNull(path)) return;
            issues.Add(new(path, "missing-selection"));
            return;
        }
        if (type == typeof(string) || type == typeof(int) || type == typeof(bool))
        {
            bool valid = type == typeof(string) ? element.ValueKind == JsonValueKind.String
                : type == typeof(int) ? element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out _)
                : element.ValueKind is JsonValueKind.True or JsonValueKind.False;
            if (!valid) issues.Add(new(path, "wrong-type"));
            return;
        }
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
        {
            if (element.ValueKind != JsonValueKind.Array) { issues.Add(new(path, "wrong-type")); return; }
            int index = 0;
            foreach (JsonElement item in element.EnumerateArray()) CheckClosed(item, type.GenericTypeArguments[0], $"{path}[{index++}]", issues, v2);
            return;
        }
        if (element.ValueKind != JsonValueKind.Object) { issues.Add(new(path, "wrong-type")); return; }
        Dictionary<string, PropertyInfo> properties = type.GetProperties().ToDictionary(p => Options.PropertyNamingPolicy!.ConvertName(p.Name), StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            // Never echo an unknown field name or any supplied value into diagnostics.
            if (!names.Add(property.Name)) { issues.Add(new(path, "duplicate-field")); continue; }
            if (!properties.TryGetValue(property.Name, out PropertyInfo? definition)) { issues.Add(new(path, "unknown-field")); continue; }
            CheckClosed(property.Value, definition.PropertyType, path + "." + property.Name, issues, v2);
        }
        foreach (string name in properties.Keys.Order(StringComparer.Ordinal))
            if (!names.Contains(name)) issues.Add(new(path + "." + name, "missing-field"));
    }

    /// <summary>Whether a v2 schema path permits an explicitly inapplicable null.</summary>
    internal static bool AllowsNull(string path) => OptionalV2Path().IsMatch(path);

    [GeneratedRegex(@"\A\$\.components\[[0-9]+\]\.(?:dapr|bundleParentId|scope\.(?:clusterId|nodeId|namespace))\z", RegexOptions.CultureInvariant)]
    private static partial Regex OptionalV2Path();
}
