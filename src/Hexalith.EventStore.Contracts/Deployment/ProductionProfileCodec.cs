using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Contracts.Deployment;

/// <summary>Closed v1 canonical UTF-8 encoding and separate infrastructure/resolved digest domains.</summary>
public static class ProductionProfileCodec
{
    /// <summary>Gets the largest accepted complete document, four MiB.</summary>
    public const int MaximumByteLength = 4 * 1024 * 1024;

    /// <summary>Gets private closed serializer conventions shared with structural inspection.</summary>
    internal static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        MaxDepth = 32,
    };

    /// <summary>Validates all selections and binds the recomputed infrastructure digest; issues no authority.</summary>
    /// <param name="profile">The complete candidate; its existing infrastructure digest is replaced.</param>
    /// <param name="context">The composing inventory's expected scoped instances.</param>
    /// <returns>A normalized fresh profile graph with the infrastructure identity bound.</returns>
    public static ProductionProfile Seal(ProductionProfile profile, ProductionProfileValidationContext context)
    {
        if (profile is null) throw Failure("$", "missing-document");
        ThrowIssues(ProductionProfileValidator.Inspect(profile, context, compareInfrastructureDigest: false));
        ProductionProfile sealedProfile = profile with { InfrastructureDigest = InfrastructureDigest(profile) };
        byte[] bytes = CanonicalBytes(sealedProfile);
        CheckSize(bytes.Length);
        return JsonSerializer.Deserialize<ProductionProfile>(bytes, Options)!;
    }

    /// <summary>Encodes a structurally complete document into exact canonical bytes.</summary>
    /// <param name="profile">The complete profile with matching infrastructure digest.</param>
    /// <param name="context">The independently composed expected instance inventory.</param>
    /// <returns>New owned UTF-8 bytes without BOM, whitespace or final LF.</returns>
    public static byte[] Encode(ProductionProfile profile, ProductionProfileValidationContext context)
    {
        if (profile is null) throw Failure("$", "missing-document");
        ThrowIssues(ProductionProfileValidator.Inspect(profile, context, compareInfrastructureDigest: true));
        byte[] bytes = CanonicalBytes(profile);
        CheckSize(bytes.Length);
        return bytes;
    }

    /// <summary>Captures input and refuses duplicate, unknown, missing, malformed or noncanonical bytes.</summary>
    /// <param name="bytes">Caller-owned complete document bytes.</param>
    /// <param name="context">The independently composed expected instance inventory.</param>
    /// <param name="expectedResolvedDigest">Optional expected complete canonical-byte digest.</param>
    /// <param name="expectedInfrastructureDigest">Optional expected infrastructure-only digest.</param>
    /// <returns>A fresh structurally valid graph, with no authentication or authority evaluation.</returns>
    public static ProductionProfile Decode(ReadOnlyMemory<byte> bytes, ProductionProfileValidationContext context,
        string? expectedResolvedDigest = null, string? expectedInfrastructureDigest = null)
    {
        CheckSize(bytes.Length);
        // Parsing, canonical comparison and hashing all use one private snapshot.
        byte[] owned = bytes.ToArray();
        ProductionProfile profile;
        try
        {
            using JsonDocument document = JsonDocument.Parse(owned, new JsonDocumentOptions { MaxDepth = 32 });
            var issues = new List<ProductionProfileIssue>();
            CheckClosed(document.RootElement, typeof(ProductionProfile), "$", issues);
            ThrowIssues(issues.OrderBy(i => i.Path, StringComparer.Ordinal).ThenBy(i => i.Code, StringComparer.Ordinal).ToArray());
            profile = document.RootElement.Deserialize<ProductionProfile>(Options)!;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            throw Failure("$", "invalid-json-or-closed-schema");
        }
        byte[] canonical = Encode(profile, context);
        if (!owned.AsSpan().SequenceEqual(canonical)) throw Failure("$", "noncanonical-encoding");
        if (expectedInfrastructureDigest is not null && expectedInfrastructureDigest != profile.InfrastructureDigest)
            throw Failure("$.infrastructureDigest", "expected-infrastructure-digest-mismatch");
        if (expectedResolvedDigest is not null && expectedResolvedDigest != Hash(owned))
            throw Failure("$", "expected-resolved-digest-mismatch");
        return profile;
    }

    /// <summary>Computes the validated template/provider/topology/tool identity, excluding release and current bindings.</summary>
    /// <param name="profile">The complete candidate; the existing digest is excluded from this calculation.</param>
    /// <param name="context">The independently composed expected instances.</param>
    /// <returns>Lowercase SHA-256 of the versioned infrastructure preimage.</returns>
    public static string ComputeInfrastructureDigest(ProductionProfile profile, ProductionProfileValidationContext context)
    {
        if (profile is null) throw Failure("$", "missing-document");
        ThrowIssues(ProductionProfileValidator.Inspect(profile, context, compareInfrastructureDigest: false));
        CheckSize(CanonicalBytes(profile).Length);
        return InfrastructureDigest(profile);
    }

    /// <summary>Computes SHA-256 of the complete validated canonical document; the result stays outside its preimage.</summary>
    /// <param name="profile">The complete profile including its matching infrastructure digest.</param>
    /// <param name="context">The independently composed expected instances.</param>
    /// <returns>The lowercase complete-document SHA-256.</returns>
    public static string ComputeResolvedDigest(ProductionProfile profile, ProductionProfileValidationContext context) => Hash(Encode(profile, context));

    /// <summary>Hashes only the versioned infrastructure preimage after structural inspection.</summary>
    internal static string InfrastructureDigest(ProductionProfile profile) => Hash(CanonicalBytes(new
    {
        purpose = "platform-infrastructure-profile",
        schemaVersion = 1,
        profile.Template,
        profile.Infrastructure,
    }));

    /// <summary>Hashes the complete canonical graph after structural inspection.</summary>
    internal static string ResolvedDigest(ProductionProfile profile) => Hash(CanonicalBytes(profile));

    private static void CheckSize(int length)
    {
        if (length is 0 or > MaximumByteLength) throw Failure("$", "size-limit");
    }

    private static void ThrowIssues(IReadOnlyList<ProductionProfileIssue> issues)
    {
        if (issues.Count != 0) throw new ProductionProfileValidationException(issues);
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static byte[] CanonicalBytes<T>(T value)
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

    private static void CheckClosed(JsonElement element, Type type, string path, List<ProductionProfileIssue> issues)
    {
        if (element.ValueKind == JsonValueKind.Null)
        {
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
            foreach (JsonElement item in element.EnumerateArray()) CheckClosed(item, type.GenericTypeArguments[0], $"{path}[{index++}]", issues);
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
            CheckClosed(property.Value, definition.PropertyType, path + "." + property.Name, issues);
        }
        foreach (string name in properties.Keys.Order(StringComparer.Ordinal))
            if (!names.Contains(name)) issues.Add(new(path + "." + name, "missing-field"));
    }

    private static ProductionProfileValidationException Failure(string path, string code) => new([new(path, code)]);
}
