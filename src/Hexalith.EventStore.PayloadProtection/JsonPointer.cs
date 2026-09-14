using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Implements the restricted RFC 6901 profile from normative section 7.2.
/// </summary>
internal static class JsonPointer {
    /// <summary>
    /// Escapes one serialized JSON member name.
    /// </summary>
    internal static string Escape(string segment)
        => segment.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);

    /// <summary>
    /// Validates and decodes a pointer into reference tokens.
    /// </summary>
    internal static IReadOnlyList<string> Decode(string pointer, bool allowRoot) {
        ArgumentNullException.ThrowIfNull(pointer);
        _ = CanonicalText.Encode(pointer, allowRoot ? 0 : 1, 2048);
        if (pointer.Length == 0) {
            if (!allowRoot) {
                throw new PayloadProtectionFormatException();
            }

            return Array.Empty<string>();
        }

        if (pointer[0] != '/') {
            throw new PayloadProtectionFormatException();
        }

        string[] encoded = pointer[1..].Split('/');
        var decoded = new string[encoded.Length];
        for (int index = 0; index < encoded.Length; index++) {
            var builder = new StringBuilder(encoded[index].Length);
            for (int offset = 0; offset < encoded[index].Length; offset++) {
                char character = encoded[index][offset];
                if (character != '~') {
                    _ = builder.Append(character);
                    continue;
                }

                if (++offset >= encoded[index].Length) {
                    throw new PayloadProtectionFormatException();
                }

                _ = encoded[index][offset] switch {
                    '0' => builder.Append('~'),
                    '1' => builder.Append('/'),
                    _ => throw new PayloadProtectionFormatException(),
                };
            }

            decoded[index] = builder.ToString();
        }

        return decoded;
    }

    /// <summary>
    /// Resolves a validated pointer in an immutable JSON tree.
    /// </summary>
    internal static JsonElement Resolve(JsonElement root, string pointer, bool allowRoot = false) {
        JsonElement current = root;
        foreach (string segment in Decode(pointer, allowRoot)) {
            if (current.ValueKind == JsonValueKind.Object) {
                if (!current.TryGetProperty(segment, out current)) {
                    throw new PayloadProtectionFormatException();
                }
            }
            else if (current.ValueKind == JsonValueKind.Array) {
                int index = ParseArrayIndex(segment);
                if (index >= current.GetArrayLength()) {
                    throw new PayloadProtectionFormatException();
                }

                current = current[index];
            }
            else {
                throw new PayloadProtectionFormatException();
            }
        }

        return current;
    }

    /// <summary>
    /// Replaces a non-root location in a mutable JSON tree.
    /// </summary>
    internal static void Replace(JsonNode root, string pointer, JsonNode? replacement) {
        IReadOnlyList<string> segments = Decode(pointer, allowRoot: false);
        JsonNode? current = root;
        for (int index = 0; index < segments.Count - 1; index++) {
            current = current switch {
                JsonObject jsonObject when jsonObject.TryGetPropertyValue(segments[index], out JsonNode? child) => child,
                JsonArray jsonArray => jsonArray[ParseArrayIndex(segments[index])],
                _ => throw new PayloadProtectionFormatException(),
            };
        }

        string final = segments[^1];
        switch (current) {
            case JsonObject jsonObject when jsonObject.ContainsKey(final):
                jsonObject[final] = replacement;
                break;
            case JsonArray jsonArray:
                int arrayIndex = ParseArrayIndex(final);
                if (arrayIndex >= jsonArray.Count) {
                    throw new PayloadProtectionFormatException();
                }

                jsonArray[arrayIndex] = replacement;
                break;
            default:
                throw new PayloadProtectionFormatException();
        }
    }

    private static int ParseArrayIndex(string segment) {
        if (segment.Length == 0
            || (segment.Length > 1 && segment[0] == '0')
            || !int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out int result)
            || result < 0) {
            throw new PayloadProtectionFormatException();
        }

        return result;
    }
}
