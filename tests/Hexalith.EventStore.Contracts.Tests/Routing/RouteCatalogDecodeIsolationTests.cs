using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.Contracts.Tests.Routing;

/// <summary>Repeated canonical reads retain exact validation and independent caller-owned models.</summary>
public sealed class RouteCatalogDecodeIsolationTests
{
    /// <summary>A caller may mutate its returned model without changing a later decode of retained bytes.</summary>
    [Theory]
    [InlineData("CanonicalCatalog.json")]
    [InlineData("V2CanonicalCatalog.json")]
    public void ReturnedModelMutationCannotChangeLaterDecodedAuthority(string fixture)
    {
        byte[] bytes = FixtureBytes(fixture);
        string root = Root(bytes);
        RouteCatalogEnvelope first = RouteCatalogCodec.Decode(bytes, root);
        var declarations = (IList<RouteContractDeclaration>)first.Declarations;
        declarations[0] = declarations[0] with { Domain = "changed" };
        var descriptorFields = (IList<IdempotencyDescriptorField>)first.Idempotency[0].DescriptorSchema.Fields;
        descriptorFields.Clear();

        RouteCatalogEnvelope second = RouteCatalogCodec.Decode(bytes, root);
        second.ShouldNotBeSameAs(first);
        second.Declarations.ShouldNotBeSameAs(first.Declarations);
        second.Idempotency[0].DescriptorSchema.Fields.ShouldNotBeSameAs(first.Idempotency[0].DescriptorSchema.Fields);
        RouteCatalogCodec.Encode(second).ShouldBe(bytes);
    }

    /// <summary>Changing the original buffer cannot substitute new authority under prior validation.</summary>
    [Theory]
    [InlineData("CanonicalCatalog.json")]
    [InlineData("V2CanonicalCatalog.json")]
    public void CallerBufferMutationRequiresCompleteValidationAgain(string fixture)
    {
        byte[] bytes = FixtureBytes(fixture);
        byte[] retained = bytes.ToArray();
        string root = Root(bytes);
        RouteCatalogCodec.Decode(bytes, root);
        const string marker = "\"generation\":";
        int generation = Encoding.UTF8.GetString(bytes).IndexOf(marker, StringComparison.Ordinal);
        generation.ShouldBeGreaterThanOrEqualTo(0);
        generation += marker.Length;
        bytes[generation] = bytes[generation] == (byte)'9' ? (byte)'8' : (byte)'9';

        Should.Throw<RouteCatalogValidationException>(() => RouteCatalogCodec.Decode(bytes));
        RouteCatalogCodec.Encode(RouteCatalogCodec.Decode(retained, root)).ShouldBe(retained);
    }

    /// <summary>The exact expected root is checked even after successful validation without one.</summary>
    [Theory]
    [InlineData("CanonicalCatalog.json")]
    [InlineData("V2CanonicalCatalog.json")]
    public void EveryExpectedRootMustMatchTheCurrentExactBytes(string fixture)
    {
        byte[] bytes = FixtureBytes(fixture);
        RouteCatalogCodec.Decode(bytes);
        Should.Throw<RouteCatalogValidationException>(() => RouteCatalogCodec.Decode(bytes, new string('0', 64)))
            .Diagnostics.Single().Code.ShouldBe("root-digest-mismatch");
        RouteCatalogCodec.Encode(RouteCatalogCodec.Decode(bytes, Root(bytes))).ShouldBe(bytes);
    }

    /// <summary>Concurrent independent catalogs, invalid documents and roots cannot share partial authority.</summary>
    [Fact]
    public async Task ConcurrentCatalogReadsKeepCompleteByteAndRootValidation()
    {
        byte[][] catalogs = [FixtureBytes("CanonicalCatalog.json"), FixtureBytes("V2CanonicalCatalog.json")];
        await Task.WhenAll(Enumerable.Range(0, 64).Select(index => Task.Run(() =>
        {
            byte[] bytes = catalogs[index % catalogs.Length].ToArray();
            string root = Root(bytes);
            RouteCatalogCodec.Encode(RouteCatalogCodec.Decode(bytes, root)).ShouldBe(bytes);
            byte[] invalid = [.. bytes, (byte)' '];
            Should.Throw<RouteCatalogValidationException>(() => RouteCatalogCodec.Decode(invalid));
            Should.Throw<RouteCatalogValidationException>(() => RouteCatalogCodec.Decode(bytes, new string('0', 64)))
                .Diagnostics.Single().Code.ShouldBe("root-digest-mismatch");
            RouteCatalogCodec.Encode(RouteCatalogCodec.Decode(bytes, root)).ShouldBe(bytes);
        }, TestContext.Current.CancellationToken)));
    }

    private static byte[] FixtureBytes(string name) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Routing", "Fixtures", name));

    private static string Root(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
