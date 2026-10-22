using System.Text.Json;

namespace Hexalith.EventStore.ProviderVerification;

internal static class JsonInput
{
    public static JsonDocument Read(string path, long maximumBytes)
    {
        byte[] bytes = ReadSnapshot(path, maximumBytes);
        return Parse(bytes);
    }

    public static byte[] ReadSnapshot(string path, long maximumBytes)
    {
        byte[] bytes = File.ReadAllBytes(path);
        if (bytes.Length == 0 || bytes.LongLength > maximumBytes)
        {
            throw new ProviderVerificationInputException("input.file.size-invalid");
        }

        return bytes;
    }

    public static JsonDocument Parse(ReadOnlyMemory<byte> bytes)
    {
        try
        {
            ReadOnlyMemory<byte> content = bytes;
            if (bytes.Length >= 3 && bytes.Span[0] == 0xEF && bytes.Span[1] == 0xBB && bytes.Span[2] == 0xBF)
            {
                content = bytes[3..];
            }

            return JsonDocument.Parse(content, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 64,
            });
        }
        catch (JsonException exception)
        {
            throw new ProviderVerificationInputException("input.json.malformed", exception);
        }
    }

    public static JsonElement RequiredArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property)
            || property.ValueKind != JsonValueKind.Array)
        {
            throw new ProviderVerificationInputException("input.json.value-invalid");
        }

        return property;
    }

    public static JsonElement RequiredObject(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property)
            || property.ValueKind != JsonValueKind.Object)
        {
            throw new ProviderVerificationInputException("input.json.value-invalid");
        }

        return property;
    }

    public static int RequiredInt32(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property)
            || property.ValueKind != JsonValueKind.Number
            || !property.TryGetInt32(out int value))
        {
            throw new ProviderVerificationInputException("input.json.value-invalid");
        }

        return value;
    }

    public static void RequireExactProperties(JsonElement element, params string[] expected)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new ProviderVerificationInputException("input.json.shape-invalid");
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!names.Add(property.Name))
            {
                throw new ProviderVerificationInputException("input.json.duplicate-field");
            }
        }

        if (!names.SetEquals(expected))
        {
            throw new ProviderVerificationInputException("input.json.extra-or-missing-field");
        }
    }

    public static void RequireAllowedProperties(
        JsonElement element,
        IReadOnlySet<string> allowed,
        params string[] required)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new ProviderVerificationInputException("input.json.shape-invalid");
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!names.Add(property.Name))
            {
                throw new ProviderVerificationInputException("input.json.duplicate-field");
            }

            if (!allowed.Contains(property.Name))
            {
                throw new ProviderVerificationInputException("input.json.extra-or-missing-field");
            }
        }

        if (required.Any(name => !names.Contains(name)))
        {
            throw new ProviderVerificationInputException("input.json.extra-or-missing-field");
        }
    }

    public static string RequiredString(JsonElement element, string propertyName, int maximumLength = 256)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property)
            || property.ValueKind != JsonValueKind.String)
        {
            throw new ProviderVerificationInputException("input.json.value-invalid");
        }

        string? value = property.GetString();
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximumLength)
        {
            throw new ProviderVerificationInputException("input.json.value-invalid");
        }

        return value;
    }
}
