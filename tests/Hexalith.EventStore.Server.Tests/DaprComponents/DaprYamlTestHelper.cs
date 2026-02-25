
using YamlDotNet.Serialization;

namespace Hexalith.EventStore.Server.Tests.DaprComponents;

/// <summary>
/// Shared YAML parsing helpers for DAPR component validation tests.
/// Used by DaprComponentValidationTests (Story 7.2) and ProductionDaprComponentValidationTests (Story 7.3).
/// </summary>
internal static class DaprYamlTestHelper {
    private static readonly IDeserializer YamlParser = new DeserializerBuilder().Build();

    internal static Dictionary<string, object> LoadYaml(string path) {
        string content = File.ReadAllText(path);
        return YamlParser.Deserialize<Dictionary<string, object>>(content);
    }

    internal static object? Nav(object root, params string[] path) {
        object? current = root;
        foreach (string key in path) {
            current = current switch {
                Dictionary<string, object> stringDict when stringDict.TryGetValue(key, out object? val) => val,
                Dictionary<object, object> objDict when objDict.TryGetValue(key, out object? val) => val,
                _ => null,
            };
            if (current is null) {
                return null;
            }
        }
        return current;
    }

    internal static List<object>? NavList(object root, params string[] path)
        => Nav(root, path) as List<object>;

    internal static string GetString(Dictionary<object, object> map, string key)
        => map.TryGetValue(key, out object? val) ? val?.ToString() ?? string.Empty : string.Empty;

    internal static List<object>? GetScopes(Dictionary<string, object> doc)
        => doc.TryGetValue("scopes", out object? scopesObj) ? scopesObj as List<object> : null;

    internal static string? GetComponentMetadataValue(Dictionary<string, object> doc, string metadataName) {
        List<object>? metadataList = NavList(doc, "spec", "metadata");
        if (metadataList is null) {
            return null;
        }

        Dictionary<object, object>? entry = metadataList
            .Cast<Dictionary<object, object>>()
            .FirstOrDefault(m => GetString(m, "name") == metadataName);
        return entry is not null ? GetString(entry, "value") : null;
    }
}
