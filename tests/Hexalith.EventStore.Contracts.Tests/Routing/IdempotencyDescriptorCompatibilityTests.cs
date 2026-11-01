using System.Text;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.Contracts.Tests.Routing;

public sealed class IdempotencyDescriptorCompatibilityTests
{
    [Fact]
    public void DefaultMetadataPreservesLegacyBytesAndGoldenDigests()
    {
        typeof(IdempotencyDescriptorField).GetConstructor([typeof(string), typeof(string)]).ShouldNotBeNull();
        var (name, type) = new IdempotencyDescriptorField("legacy-field", "string");
        name.ShouldBe("legacy-field"); type.ShouldBe("string");
        JsonSerializer.Serialize(new IdempotencyDescriptorField("legacy-field", "string"))
            .ShouldBe("{\"Name\":\"legacy-field\",\"Type\":\"string\"}");
        byte[] bytes = RouteCatalogCodecTests.FixtureBytes();
        var catalog = RouteCatalogCodec.Decode(bytes);
        RouteCatalogCodec.Encode(catalog).ShouldBe(bytes);
        using var expected = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Routing", "Fixtures", "Digests.json")));
        RouteCatalogCodec.ComputeDescriptorDigest(catalog.Idempotency[0].DescriptorSchema)
            .ShouldBe(expected.RootElement.GetProperty("descriptor").GetString());
    }

    [Fact]
    public void NullablePayloadAndSeparateInputsAreExplicitAndDigestBound()
    {
        var baseline = RouteCatalogCodec.Decode(RouteCatalogCodecTests.FixtureBytes());
        var policy = baseline.Idempotency[0];
        var schema = policy.DescriptorSchema with { Fields =
        [
            new("target", "string", Source: "target"),
            new("parent_folder_id", "string", Nullable: true, Source: "payload"),
            new("semantic_payload_sha256", "string", Source: "semantic-option"),
            new("delegated-task-scope", "string", Source: "delegated-task-scope"),
            new("credential-scope", "string", Nullable: true, Source: "credential-scope"),
        ] };
        RouteCatalogValidator.ValidateDescriptor(schema);
        var entry = policy with { DescriptorSchema = schema, DescriptorDigest = RouteCatalogCodec.ComputeDescriptorDigest(schema) };
        entry = entry with { ContentDigest = RouteCatalogCodec.ComputeIdempotencyEntryDigest(entry) };
        var changed = RouteCatalogCodec.Seal(baseline with { Idempotency = baseline.Idempotency.Select(item => item == policy ? entry : item).ToArray() });
        byte[] encoded = RouteCatalogCodec.Encode(changed);
        RouteCatalogCodec.Encode(RouteCatalogCodec.Decode(encoded)).ShouldBe(encoded);
        RouteCatalogCodec.ComputeDescriptorDigest(schema with { Fields = schema.Fields.Select(field => field with { Nullable = false }).ToArray() })
            .ShouldNotBe(entry.DescriptorDigest);
        Encoding.UTF8.GetString(encoded).ShouldContain("\"source\":\"semantic-option\"");
    }

    [Theory]
    [InlineData("unknown-source")]
    [InlineData("nullable-option")]
    [InlineData("nullable-target")]
    [InlineData("non-string-option")]
    [InlineData("wrong-scope-name")]
    [InlineData("legacy-name")]
    [InlineData("invalid-payload-name")]
    [InlineData("duplicate-payload")]
    [InlineData("duplicate-target")]
    public void MalformedMetadataRefusesWithoutWideningLegacyNames(string fault)
    {
        IdempotencyDescriptorField field = fault switch
        {
            "unknown-source" => new("field", "string", Source: "invented"),
            "nullable-option" => new("field", "string", Nullable: true, Source: "semantic-option"),
            "nullable-target" => new("target", "string", Nullable: true, Source: "target"),
            "non-string-option" => new("field", "int64", Source: "semantic-option"),
            "wrong-scope-name" => new("field", "string", Source: "credential-scope"),
            "legacy-name" => new("legacy_field", "string"),
            "invalid-payload-name" => new("invalid/path", "string", Source: "payload"),
            "duplicate-target" => new("target", "string", Source: "target"),
            _ => new("field", "string", Source: "payload"),
        };
        IdempotencyDescriptorField[] fields = fault == "duplicate-payload" ? [new("field", "string"), field]
            : fault == "duplicate-target" ? [new("target", "string"), field] : [field];
        Should.Throw<RouteCatalogValidationException>(() => RouteCatalogValidator.ValidateDescriptor(new("schema", 1, fields)));
    }
}
