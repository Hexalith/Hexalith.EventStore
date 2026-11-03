using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Routing;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Checks exact-byte, fresh-graph, lifetime and memory boundaries of deterministic acquisition decoding.</summary>
public sealed class RouteCatalogDecodeScopeTests
{
    /// <summary>Historical golden documents and the actual schema-three producer retain exact bytes on a warm decode.</summary>
    [Theory]
    [InlineData("CanonicalCatalog.json")]
    [InlineData("V2CanonicalCatalog.json")]
    [InlineData("schema-three")]
    public void WarmHistoricalAndCurrentCatalogsRetainEveryCanonicalByte(string fixtureName)
    {
        byte[] bytes;
        if (fixtureName == "schema-three") bytes = new RouteCatalogTestFixture(compiledSchemaThree: true).Candidate().CanonicalCatalog;
        else
        {
            DirectoryInfo? root = new(AppContext.BaseDirectory);
            while (root is not null && !File.Exists(Path.Combine(root.FullName, "Hexalith.EventStore.slnx"))) root = root.Parent;
            bytes = File.ReadAllBytes(Path.Combine(root!.FullName, "tests", "Hexalith.EventStore.Contracts.Tests", "Routing", "Fixtures", fixtureName));
        }
        using var scope = RouteCatalogDecodeScope.Enter();
        RouteCatalogEnvelope cold = RouteCatalogCodec.Decode(bytes, Hash(bytes));
        RouteCatalogEnvelope warm = RouteCatalogCodec.Decode(bytes, Hash(bytes));
        RouteCatalogCodec.Encode(warm).ShouldBe(bytes);
        warm.RouteFacetDigest.ShouldBe(cold.RouteFacetDigest);
        warm.IdempotencyFacetDigest.ShouldBe(cold.IdempotencyFacetDigest);
        warm.Declarations.ShouldNotBeSameAs(cold.Declarations);
        scope.Count.ShouldBe(1);
    }

    /// <summary>Expected roots remain mandatory both before and after an exact document was validated.</summary>
    [Fact]
    public void ColdAndWarmWrongExpectedRootsRefuseWithoutChangingValidationOrder()
    {
        byte[] bytes = Documents(1)[0];
        using var scope = RouteCatalogDecodeScope.Enter();
        Should.Throw<RouteCatalogValidationException>(() => RouteCatalogCodec.Decode(bytes, new string('0', 64)))
            .Diagnostics.Single().Code.ShouldBe("root-digest-mismatch");
        scope.Count.ShouldBe(0);
        RouteCatalogCodec.Decode(bytes, Hash(bytes));
        scope.Count.ShouldBe(1);
        Should.Throw<RouteCatalogValidationException>(() => RouteCatalogCodec.Decode(bytes, new string('0', 64)))
            .Diagnostics.Single().Code.ShouldBe("root-digest-mismatch");
        byte[] noncanonical = [.. " "u8, .. bytes];
        Should.Throw<RouteCatalogValidationException>(() => RouteCatalogCodec.Decode(noncanonical, new string('0', 64)))
            .Diagnostics.Single().Code.ShouldBe("noncanonical-encoding");
        byte[] unknown = Encoding.UTF8.GetBytes("{\"unknown\":null," + Encoding.UTF8.GetString(bytes)[1..]);
        Should.Throw<RouteCatalogValidationException>(() => RouteCatalogCodec.Decode(unknown, new string('0', 64)))
            .Diagnostics.Single().Code.ShouldBe("invalid-json-or-closed-schema");
        scope.Count.ShouldBe(1);
    }

    /// <summary>Neither caller bytes nor a previously returned mutable graph can poison retained canonical truth.</summary>
    [Fact]
    public void MutatedInputAndReturnedGraphCannotBecomeCachedTruth()
    {
        byte[][] documents = Documents(2);
        byte[] bytes = documents[0].ToArray();
        string root = Hash(bytes);
        using var scope = RouteCatalogDecodeScope.Enter();
        RouteCatalogEnvelope first = RouteCatalogCodec.Decode(bytes, root);
        string appId = first.Routes[0].AppId;
        ((IList<RouteCatalogEntry>)first.Routes)[0] = first.Routes[0] with { AppId = "tampered" };
        bytes[0] = (byte)'[';
        Should.Throw<RouteCatalogValidationException>(() => RouteCatalogCodec.Decode(bytes, root));
        bytes[0] = (byte)'{';
        RouteCatalogEnvelope fresh = RouteCatalogCodec.Decode(bytes, root);
        fresh.Routes[0].AppId.ShouldBe(appId);
        fresh.Routes.ShouldNotBeSameAs(first.Routes);
        RouteCatalogCodec.Encode(fresh).ShouldBe(bytes);
        Should.Throw<RouteCatalogValidationException>(() => RouteCatalogCodec.Decode(documents[1], root))
            .Diagnostics.Single().Code.ShouldBe("root-digest-mismatch");
        RouteCatalogCodec.Decode(documents[1], Hash(documents[1])).Generation.ShouldBe(2);
        scope.Count.ShouldBe(2);
    }

    /// <summary>Warm schema-three graphs own all nested schema and declaration data independently of prior returned graphs.</summary>
    [Fact]
    public void WarmSchemaGraphsRemainIndependentAfterNestedOutputPoisoning()
    {
        byte[] bytes = new RouteCatalogTestFixture(compiledSchemaThree: true).Candidate().CanonicalCatalog;
        using var scope = RouteCatalogDecodeScope.Enter();
        RouteCatalogEnvelope first = RouteCatalogCodec.Decode(bytes, Hash(bytes));
        using var poison = JsonDocument.Parse("{\"type\":\"null\"}");
        ((IList<RouteOperationContract>)first.Operations!)[0] = first.Operations![0] with { RequestSchema = poison.RootElement };
        RouteContractDeclaration declaration = first.Declarations.First(entry => entry.Execution!.Aliases.Count > 0);
        ((IList<string>)declaration.Execution!.Aliases)[0] = "changed-output-alias";
        poison.Dispose();

        RouteCatalogEnvelope warm = RouteCatalogCodec.Decode(bytes, Hash(bytes));
        warm.Operations.ShouldNotBeSameAs(first.Operations);
        warm.Operations![0].RequestSchema.ValueKind.ShouldBe(JsonValueKind.Object);
        warm.Declarations.SelectMany(entry => entry.Execution!.Aliases).ShouldNotContain("changed-output-alias");
        RouteCatalogCodec.Encode(warm).ShouldBe(bytes);
        scope.CacheHits.ShouldBe(1);

        byte[] duplicate = Encoding.UTF8.GetBytes("{\"generation\":1," + Encoding.UTF8.GetString(bytes)[1..]);
        Should.Throw<RouteCatalogValidationException>(() => RouteCatalogCodec.Decode(duplicate, Hash(duplicate)))
            .Diagnostics.Single().Code.ShouldBe("duplicate-field");
        scope.CacheHits.ShouldBe(1);
        RouteCatalogCodec.Encode(RouteCatalogCodec.Decode(bytes, Hash(bytes))).ShouldBe(bytes);
    }

    /// <summary>Parallel decoders share bounded bytes while each result remains an independent graph.</summary>
    [Fact]
    public async Task ConcurrentDecodesReturnIndependentGraphsAndNestedScopesStayIndependent()
    {
        byte[] bytes = Documents(1)[0];
        using var outer = RouteCatalogDecodeScope.Enter();
        RouteCatalogEnvelope[] results = await Task.WhenAll(Enumerable.Range(0, 12)
            .Select(_ => Task.Run(() => RouteCatalogCodec.Decode(bytes, Hash(bytes)))));
        outer.Count.ShouldBe(1);
        results[0].Routes.ShouldNotBeSameAs(results[1].Routes);
        using (var nested = RouteCatalogDecodeScope.Enter())
        {
            nested.Count.ShouldBe(0);
            RouteCatalogCodec.Decode(bytes);
            nested.Count.ShouldBe(1);
            outer.Count.ShouldBe(1);
        }
        RouteCatalogDecodeScope.Current.ShouldBeSameAs(outer);
    }

    /// <summary>Escaped contexts cannot reuse or repopulate a closed acquisition scope.</summary>
    [Fact]
    public async Task CloseClearsOwnedBytesAndEscapedWorkPerformsNormalValidation()
    {
        byte[] bytes = Documents(1)[0];
        var scope = RouteCatalogDecodeScope.Enter();
        RouteCatalogCodec.Decode(bytes);
        var proceed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task escaped = Task.Run(async () =>
        {
            await proceed.Task;
            RouteCatalogDecodeScope.Current.ShouldBeSameAs(scope);
            RouteCatalogCodec.Decode(bytes, Hash(bytes));
            Should.Throw<RouteCatalogValidationException>(() => RouteCatalogCodec.Decode(bytes, new string('0', 64)));
            scope.Count.ShouldBe(0);
        });
        scope.Dispose();
        scope.Count.ShouldBe(0);
        scope.RetainedBytes.ShouldBe(0);
        RouteCatalogDecodeScope.Current.ShouldBeNull();
        proceed.SetResult();
        await escaped;
    }

    /// <summary>The entry limit refuses additional retention without changing successful decoding.</summary>
    [Fact]
    public void EntryLimitBoundsRetentionAndDisposeClearsIt()
    {
        byte[][] documents = Documents(4);
        var scope = RouteCatalogDecodeScope.Enter();
        foreach (byte[] bytes in documents) RouteCatalogCodec.Decode(bytes, Hash(bytes));
        scope.Count.ShouldBe(3);
        scope.RetainedBytes.ShouldBe(documents.Take(3).Sum(bytes => bytes.Length));
        scope.Dispose();
        scope.Count.ShouldBe(0);
        scope.RetainedBytes.ShouldBe(0);
    }

    /// <summary>The byte limit applies independently of entry count to actual valid large schema documents.</summary>
    [Fact]
    public void ByteLimitBoundsActualCanonicalSchemas()
    {
        var fixture = new RouteCatalogTestFixture(compiledSchemaThree: true);
        using JsonDocument schema = JsonDocument.Parse(JsonSerializer.Serialize(new { description = new string('x', 3 * 1024 * 1024) }));
        RouteOperationContract operation = fixture.Envelope.Operations![0] with { RequestSchema = schema.RootElement.Clone() };
        operation = operation with { Digest = RouteCatalogCodec.ComputeOperationContractDigest(operation) };
        byte[][] documents = Enumerable.Range(1, 3).Select(generation =>
        {
            RouteCatalogEnvelope envelope = RouteCatalogCodec.Decode(fixture.Candidate(generation).CanonicalCatalog);
            envelope = envelope with
            {
                Operations = envelope.Operations!.Select(item => item.RouteId == operation.RouteId ? operation : item).ToArray(),
                Routes = envelope.Routes.Select(item => item.RouteId == operation.RouteId ? item with { OperationContractDigest = operation.Digest } : item).ToArray(),
            };
            return RouteCatalogCodec.Encode(RouteCatalogCodec.Seal(envelope));
        }).ToArray();
        using var scope = RouteCatalogDecodeScope.Enter();
        foreach (byte[] bytes in documents) RouteCatalogCodec.Decode(bytes, Hash(bytes));
        scope.Count.ShouldBe(2);
        scope.RetainedBytes.ShouldBeLessThanOrEqualTo(8 * 1024 * 1024);
        scope.RetainedBytes.ShouldBe(documents.Take(2).Sum(bytes => bytes.Length));
    }

    private static byte[][] Documents(int count)
    {
        var fixture = new RouteCatalogTestFixture();
        return Enumerable.Range(1, count).Select(generation => fixture.Candidate(generation).CanonicalCatalog).ToArray();
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
