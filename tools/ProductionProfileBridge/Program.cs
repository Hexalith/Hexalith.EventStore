using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

using Hexalith.EventStore.Contracts.Deployment;

// Read-only structural bridge. Root, separate inventory hash and both owner digest
// domains are bound by the caller. There is no generation, issuance or deployment mode.
try
{
    if (args is not [string repositoryRoot, string inventoryDigest, string resolvedDigest, string infrastructureDigest])
    {
        Console.Error.WriteLine("INPUT_INVALID");
        return 2;
    }

    if (!IsDigest(inventoryDigest) || !IsDigest(resolvedDigest) || infrastructureDigest != "-" && !IsDigest(infrastructureDigest))
        throw new InvalidDataException();
    string root = Path.GetFullPath(repositoryRoot);
    byte[] inventoryBytes = ReadBounded(root, "deploy/dapr/production-composing-inventory.json", 1024 * 1024);
    if (Hash(inventoryBytes) != inventoryDigest) throw new InvalidDataException();
    using JsonDocument inventory = JsonDocument.Parse(inventoryBytes, new JsonDocumentOptions { MaxDepth = 16 });
    JsonElement document = inventory.RootElement;
    Closed(document, ["schema", "source", "expectedApplicationIds", "expectedComponentIds"]);
    int version = document.GetProperty("schema").GetString() switch
    {
        "hexalith.eventstore.production-composing-inventory.v1" => 1,
        "hexalith.eventstore.production-composing-inventory.v2" => 2,
        _ => throw new InvalidDataException(),
    };
    JsonElement source = document.GetProperty("source");
    Closed(source, ["path", "sha256"]);
    string sourcePath = source.GetProperty("path").GetString()!;
    if (sourcePath is "deploy/dapr/production-profile.yaml" or "deploy/dapr/production-composing-inventory.json")
        throw new InvalidDataException();
    string sourceDigest = source.GetProperty("sha256").GetString()!;
    if (!IsDigest(sourceDigest) || Hash(ReadBounded(root, sourcePath, 1024 * 1024)) != sourceDigest)
        throw new InvalidDataException();
    ProductionProfileValidationContext context = new(Ids(document, "expectedApplicationIds"), Ids(document, "expectedComponentIds"));
    byte[] bytes = ReadBounded(root, "deploy/dapr/production-profile.yaml", ProductionProfileCodec.MaximumByteLength);
    // Version comes from the separately bound inventory. Never probe or fall back.
    ProductionProfileValidationResult result = version switch
    {
        1 => ProductionProfileValidator.Validate(ProductionProfileCodec.Decode(bytes, context, resolvedDigest,
            infrastructureDigest == "-" ? null : infrastructureDigest), context),
        2 => ProductionProfileV2Validator.Validate(ProductionProfileV2Codec.Decode(bytes, context, resolvedDigest,
            infrastructureDigest == "-" ? null : infrastructureDigest), context),
        _ => throw new InvalidDataException(),
    };
    Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    return result.IsStructurallyValid ? 0 : 1;
}
catch (ProductionProfileValidationException exception)
{
    Console.Error.WriteLine(JsonSerializer.Serialize(exception.Issues));
    return 1;
}
catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException or JsonException or InvalidOperationException or KeyNotFoundException)
{
    Console.Error.WriteLine("INPUT_INVALID");
    return 2;
}

static bool IsDigest(string? value) => value is not null && Regex.IsMatch(value, "^[0-9a-f]{64}$", RegexOptions.CultureInvariant);

static string Hash(byte[] value) => Convert.ToHexStringLower(SHA256.HashData(value));

static void Closed(JsonElement value, string[] expected)
{
    if (value.ValueKind != JsonValueKind.Object) throw new InvalidDataException();
    string[] names = value.EnumerateObject().Select(p => p.Name).ToArray();
    if (names.Length != expected.Length || !names.Order(StringComparer.Ordinal).SequenceEqual(expected.Order(StringComparer.Ordinal)))
        throw new InvalidDataException();
}

static string[] Ids(JsonElement value, string name)
{
    JsonElement array = value.GetProperty(name);
    if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() is < 1 or > 1000) throw new InvalidDataException();
    string[] ids = array.EnumerateArray().Select(i => i.GetString()!).ToArray();
    if (ids.Any(string.IsNullOrWhiteSpace) || ids.Any(id => id.Length > 512 || id.Any(char.IsControl))
        || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length) throw new InvalidDataException();
    return ids;
}

static byte[] ReadBounded(string root, string relative, int maximum)
{
    if (!Regex.IsMatch(relative, "^[A-Za-z0-9_.@/-]+$", RegexOptions.CultureInvariant)
        || Path.IsPathRooted(relative) || relative.Split('/').Any(part => part is "" or "." or ".."))
        throw new InvalidDataException();
    string candidate = root;
    foreach (string part in relative.Split('/'))
    {
        candidate = Path.Combine(candidate, part);
        if ((File.GetAttributes(candidate) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException();
    }
    if ((File.GetAttributes(candidate) & FileAttributes.Directory) != 0) throw new InvalidDataException();
    using FileStream stream = File.OpenRead(candidate);
    byte[] buffer = new byte[maximum + 1];
    int length = stream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);
    if (length == 0 || length > maximum) throw new InvalidDataException();
    return buffer.AsSpan(0, length).ToArray();
}
