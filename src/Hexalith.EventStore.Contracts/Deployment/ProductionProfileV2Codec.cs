using System.Security.Cryptography;
using System.Text.Json;

namespace Hexalith.EventStore.Contracts.Deployment;

/// <summary>Closed v2 canonical UTF-8 encoding and separate infrastructure/resolved digest domains.</summary>
public static class ProductionProfileV2Codec
{
    /// <summary>Gets the largest accepted complete document, four MiB.</summary>
    public const int MaximumByteLength = 4 * 1024 * 1024;

    /// <summary>Gets the unchanged owner canonical serializer conventions.</summary>
    internal static JsonSerializerOptions Options => ProductionProfileCanonicalJson.Options;

    /// <summary>Validates all selections and binds the recomputed infrastructure digest; issues no authority.</summary>
    /// <param name="profile">The complete candidate; its existing infrastructure digest is replaced.</param>
    /// <param name="context">The composing inventory's expected scoped instances.</param>
    /// <returns>A normalized fresh profile graph with the infrastructure identity bound.</returns>
    public static ProductionProfileV2 Seal(ProductionProfileV2 profile, ProductionProfileValidationContext context)
    {
        if (profile is null) throw Failure("$", "missing-document");
        ThrowIssues(ProductionProfileV2Validator.Inspect(profile, context, compareInfrastructureDigest: false));
        ProductionProfileV2 sealedProfile = profile with { InfrastructureDigest = InfrastructureDigest(profile) };
        byte[] bytes = CanonicalBytes(sealedProfile);
        CheckSize(bytes.Length);
        return JsonSerializer.Deserialize<ProductionProfileV2>(bytes, Options)!;
    }

    /// <summary>Encodes a structurally complete document into exact canonical bytes.</summary>
    /// <param name="profile">The complete profile with matching infrastructure digest.</param>
    /// <param name="context">The independently composed expected instance inventory.</param>
    /// <returns>New owned UTF-8 bytes without BOM, whitespace or final LF.</returns>
    public static byte[] Encode(ProductionProfileV2 profile, ProductionProfileValidationContext context)
    {
        if (profile is null) throw Failure("$", "missing-document");
        ThrowIssues(ProductionProfileV2Validator.Inspect(profile, context, compareInfrastructureDigest: true));
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
    public static ProductionProfileV2 Decode(ReadOnlyMemory<byte> bytes, ProductionProfileValidationContext context,
        string? expectedResolvedDigest = null, string? expectedInfrastructureDigest = null)
    {
        CheckSize(bytes.Length);
        // Parsing, canonical comparison and hashing all use one private snapshot.
        byte[] owned = bytes.ToArray();
        ProductionProfileV2 profile;
        try
        {
            using JsonDocument document = JsonDocument.Parse(owned, new JsonDocumentOptions { MaxDepth = 32 });
            var issues = new List<ProductionProfileIssue>();
            CheckClosed(document.RootElement, typeof(ProductionProfileV2), "$", issues);
            ThrowIssues(issues.OrderBy(i => i.Path, StringComparer.Ordinal).ThenBy(i => i.Code, StringComparer.Ordinal).ToArray());
            profile = document.RootElement.Deserialize<ProductionProfileV2>(Options)!;
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
    public static string ComputeInfrastructureDigest(ProductionProfileV2 profile, ProductionProfileValidationContext context)
    {
        if (profile is null) throw Failure("$", "missing-document");
        ThrowIssues(ProductionProfileV2Validator.Inspect(profile, context, compareInfrastructureDigest: false));
        CheckSize(CanonicalBytes(profile).Length);
        return InfrastructureDigest(profile);
    }

    /// <summary>Computes SHA-256 of the complete validated canonical document; the result stays outside its preimage.</summary>
    /// <param name="profile">The complete profile including its matching infrastructure digest.</param>
    /// <param name="context">The independently composed expected instances.</param>
    /// <returns>The lowercase complete-document SHA-256.</returns>
    public static string ComputeResolvedDigest(ProductionProfileV2 profile, ProductionProfileValidationContext context) => Hash(Encode(profile, context));

    /// <summary>Hashes only the versioned infrastructure preimage after structural inspection.</summary>
    internal static string InfrastructureDigest(ProductionProfileV2 profile) => Hash(CanonicalBytes(new
    {
        purpose = "platform-infrastructure-profile",
        schemaVersion = 2,
        profile.Template,
        profile.Infrastructure,
    }));

    /// <summary>Hashes the complete canonical graph after structural inspection.</summary>
    internal static string ResolvedDigest(ProductionProfileV2 profile) => Hash(CanonicalBytes(profile));

    private static void CheckSize(int length)
    {
        if (length is 0 or > MaximumByteLength) throw Failure("$", "size-limit");
    }

    private static void ThrowIssues(IReadOnlyList<ProductionProfileIssue> issues)
    {
        if (issues.Count != 0) throw new ProductionProfileValidationException(issues);
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static byte[] CanonicalBytes<T>(T value) => ProductionProfileCanonicalJson.CanonicalBytes(value);

    private static void CheckClosed(JsonElement element, Type type, string path, List<ProductionProfileIssue> issues) =>
        ProductionProfileCanonicalJson.CheckClosed(element, type, path, issues, v2: true);

    private static ProductionProfileValidationException Failure(string path, string code) => new([new(path, code)]);
}
