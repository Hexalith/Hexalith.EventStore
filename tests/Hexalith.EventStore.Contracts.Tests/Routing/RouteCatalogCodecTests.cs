using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.Contracts.Tests.Routing;

public class RouteCatalogCodecTests
{
    [Fact]
    public void CompleteFixtureMatchesIndependentGoldenDigestsAndRoundTripsExactly()
    {
        byte[] bytes = FixtureBytes();
        using JsonDocument expected = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Routing", "Fixtures", "Digests.json")));
        string root = expected.RootElement.GetProperty("root").GetString()!;
        RouteCatalogEnvelope catalog = RouteCatalogCodec.Decode(bytes, root);

        RouteCatalogCodec.Encode(catalog).ShouldBe(bytes);
        RouteCatalogCodec.ComputeRootDigest(catalog).ShouldBe(root);
        Convert.ToHexStringLower(SHA256.HashData(bytes)).ShouldBe(root);
        catalog.RouteFacetDigest.ShouldBe(expected.RootElement.GetProperty("routeFacet").GetString());
        catalog.IdempotencyFacetDigest.ShouldBe(expected.RootElement.GetProperty("idempotencyFacet").GetString());
        RouteCatalogCodec.ComputeDeclarationDigest(catalog.Declarations[0]).ShouldBe(expected.RootElement.GetProperty("declaration").GetString());
        RouteCatalogCodec.ComputeDescriptorDigest(catalog.Idempotency[0].DescriptorSchema).ShouldBe(expected.RootElement.GetProperty("descriptor").GetString());
    }

    [Fact]
    public void ConstructionOrderDoesNotAffectBytesOrDigests()
    {
        RouteCatalogEnvelope catalog = RouteCatalogCodec.Decode(FixtureBytes());
        RouteCatalogEnvelope reordered = catalog with
        {
            Declarations = catalog.Declarations.Reverse().Select(d => d with { FallbackTypes = d.FallbackTypes.Reverse().ToArray() }).ToArray(),
            Routes = catalog.Routes.Reverse().ToArray(),
            Idempotency = catalog.Idempotency.Reverse().Select(i => i with
            {
                DescriptorSchema = i.DescriptorSchema with { Fields = i.DescriptorSchema.Fields.Reverse().ToArray() },
                KeyMap = i.KeyMap.Reverse().ToArray(),
            }).ToArray(),
        };
        RouteCatalogCodec.Encode(reordered).ShouldBe(FixtureBytes());
    }

    [Theory]
    [InlineData("whitespace")]
    [InlineData("duplicate-root")]
    [InlineData("duplicate-nested")]
    [InlineData("unknown-root")]
    [InlineData("unknown-nested")]
    [InlineData("missing-required")]
    [InlineData("unsupported-version")]
    [InlineData("escaped-property")]
    [InlineData("property-order")]
    [InlineData("bom")]
    [InlineData("float-integer")]
    [InlineData("null-entry")]
    [InlineData("oversized")]
    public void InvalidEncodingFailsBesidePassingControl(string scenario)
    {
        string valid = Encoding.UTF8.GetString(FixtureBytes());
        RouteCatalogCodec.Decode(Encoding.UTF8.GetBytes(valid)).SchemaVersion.ShouldBe(1);
        string invalid = scenario switch
        {
            "whitespace" => valid + "\n",
            "duplicate-root" => valid.Insert(1, "\"schemaVersion\":1,"),
            "duplicate-nested" => valid.Replace("\"routeId\":\"counter-create\"", "\"routeId\":\"counter-create\",\"routeId\":\"counter-create\"", StringComparison.Ordinal),
            "unknown-root" => valid.Insert(1, "\"secretValue\":\"do-not-disclose\","),
            "unknown-nested" => valid.Replace("\"adapterId\":", "\"secretValue\":\"do-not-disclose\",\"adapterId\":", StringComparison.Ordinal),
            "missing-required" => valid.Replace("\"messageIdVersion\":2,", "", StringComparison.Ordinal),
            "unsupported-version" => valid.Replace("\"schemaVersion\":1", "\"schemaVersion\":2", StringComparison.Ordinal),
            "escaped-property" => valid.Replace("\"profile\"", "\"profi\\u006ce\"", StringComparison.Ordinal),
            "property-order" => "{\"schemaVersion\":1," + valid[1..].Replace(",\"schemaVersion\":1", "", StringComparison.Ordinal),
            "bom" => "\uFEFF" + valid,
            "float-integer" => valid.Replace("\"generation\":7", "\"generation\":7.0", StringComparison.Ordinal),
            "null-entry" => valid.Replace("\"routes\":[", "\"routes\":[null,", StringComparison.Ordinal),
            _ => new string(' ', RouteCatalogCodec.MaximumByteLength + 1),
        };
        RouteCatalogValidationException failure = Should.Throw<RouteCatalogValidationException>(() => RouteCatalogCodec.Decode(Encoding.UTF8.GetBytes(invalid)));
        failure.Message.ShouldNotContain("do-not-disclose");
        failure.Message.Length.ShouldBeLessThan(256);
    }

    [Fact]
    public void ExpectedRootDigestBindsRetainedBytes()
    {
        byte[] bytes = FixtureBytes();
        RouteCatalogCodec.Decode(bytes, Convert.ToHexStringLower(SHA256.HashData(bytes)));
        Should.Throw<RouteCatalogValidationException>(() => RouteCatalogCodec.Decode(bytes, new string('0', 64)))
            .Diagnostics.Single().Code.ShouldBe("root-digest-mismatch");
    }

    [Fact]
    public void ReaderLookupOrderIsPreservedAndBound()
    {
        RouteCatalogEnvelope catalog = RouteCatalogCodec.Decode(FixtureBytes());
        RouteCatalogEnvelope changed = RouteCatalogCodec.Seal(catalog with
        {
            Idempotency = catalog.Idempotency.Select(i => i with { ReaderDigestKeyGenerations = i.ReaderDigestKeyGenerations.Reverse().ToArray() }).ToArray(),
        });
        changed.IdempotencyFacetDigest.ShouldNotBe(catalog.IdempotencyFacetDigest);
    }

    internal static byte[] FixtureBytes() => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Routing", "Fixtures", "CanonicalCatalog.json"));
}
