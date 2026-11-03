using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Routing;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Checks exact-byte document facts across independent scopes without retaining graphs or authority.</summary>
public sealed class HostValidatedCatalogBytesMemoTests
{
    /// <summary>A fresh scope benefits only from its explicitly supplied owner's bytes and always returns a fresh graph.</summary>
    [Fact]
    public void IndependentScopesReuseOnlyOwnedCanonicalBytesAndCheckEveryExpectedRoot()
    {
        byte[][] documents = Documents(2);
        byte[] input = documents[0].ToArray();
        string root = Hash(input);
        using var memo = new RouteCatalogValidatedBytesMemo();
        RouteCatalogEnvelope prior;
        var first = RouteCatalogDecodeScope.EnterWithMemo(memo);
        Should.Throw<RouteCatalogValidationException>(() => RouteCatalogCodec.Decode(input, new string('0', 64)))
            .Diagnostics.Single().Code.ShouldBe("root-digest-mismatch");
        memo.Count.ShouldBe(0);
        prior = RouteCatalogCodec.Decode(input, root);
        first.CacheHits.ShouldBe(0);
        first.Dispose();
        first.Count.ShouldBe(0);
        first.RetainedBytes.ShouldBe(0);
        memo.Count.ShouldBe(1);
        ((IList<RouteCatalogEntry>)prior.Routes)[0] = prior.Routes[0] with { AppId = "mutated-output" };
        input[0] = (byte)'[';
        using var second = RouteCatalogDecodeScope.EnterWithMemo(memo);
        Should.Throw<RouteCatalogValidationException>(() => RouteCatalogCodec.Decode(input, root));
        input[0] = (byte)'{';
        RouteCatalogEnvelope fresh = RouteCatalogCodec.Decode(input, root);
        fresh.Routes.ShouldNotBeSameAs(prior.Routes);
        fresh.Routes[0].AppId.ShouldNotBe("mutated-output");
        RouteCatalogCodec.Encode(fresh).ShouldBe(input);
        second.CacheHits.ShouldBe(1);
        Should.Throw<RouteCatalogValidationException>(() => RouteCatalogCodec.Decode(input, new string('0', 64)))
            .Diagnostics.Single().Code.ShouldBe("root-digest-mismatch");
        Should.Throw<RouteCatalogValidationException>(() => RouteCatalogCodec.Decode(documents[1], root))
            .Diagnostics.Single().Code.ShouldBe("root-digest-mismatch");
        memo.Count.ShouldBe(1);
        byte[] noncanonical = [.. " "u8, .. input];
        Should.Throw<RouteCatalogValidationException>(() => RouteCatalogCodec.Decode(noncanonical, new string('0', 64)))
            .Diagnostics.Single().Code.ShouldBe("noncanonical-encoding");
        byte[] unknown = Encoding.UTF8.GetBytes("{\"unknown\":null," + Encoding.UTF8.GetString(input)[1..]);
        Should.Throw<RouteCatalogValidationException>(() => RouteCatalogCodec.Decode(unknown, root))
            .Diagnostics.Single().Code.ShouldBe("invalid-json-or-closed-schema");
        RouteCatalogCodec.Decode(documents[1], Hash(documents[1])).Generation.ShouldBe(2);
        memo.Count.ShouldBe(2);
    }

    /// <summary>Closed borrowed scopes cannot use an open host's memo, and closing the owner permanently clears retained bytes.</summary>
    [Fact]
    public async Task ClosedScopeAndDisposedOwnerCannotReuseOrRepopulateBytes()
    {
        byte[] bytes = Documents(1)[0];
        var memo = new RouteCatalogValidatedBytesMemo();
        var scope = RouteCatalogDecodeScope.EnterWithMemo(memo);
        RouteCatalogCodec.Decode(bytes);
        var go = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task escaped = Task.Run(async () =>
        {
            await go.Task;
            RouteCatalogDecodeScope.Current.ShouldBeSameAs(scope);
            scope.HasHostMemo.ShouldBeFalse();
            RouteCatalogCodec.Decode(bytes, Hash(bytes));
            scope.CaptureCount.ShouldBe(0);
            scope.CacheHits.ShouldBe(0);
        });
        scope.Dispose();
        memo.Count.ShouldBe(1);
        go.SetResult();
        await escaped;
        memo.Dispose();
        memo.IsClosed.ShouldBeTrue();
        memo.Count.ShouldBe(0);
        memo.RetainedBytes.ShouldBe(0);
        using var after = RouteCatalogDecodeScope.EnterWithMemo(memo);
        RouteCatalogCodec.Decode(bytes, Hash(bytes));
        after.HasHostMemo.ShouldBeFalse();
        after.CacheHits.ShouldBe(0);
        memo.Count.ShouldBe(0);
        Should.Throw<RouteCatalogValidationException>(() => RouteCatalogCodec.Decode(bytes, new string('0', 64)));
    }

    /// <summary>Independent concurrent scopes have fresh graphs, while unrelated owner memos remain cold.</summary>
    [Fact]
    public async Task ConcurrentAndForeignOwnersDoNotShareGraphsOrImplicitEntries()
    {
        byte[] bytes = Documents(1)[0];
        using var owner = new RouteCatalogValidatedBytesMemo();
        using (var seed = RouteCatalogDecodeScope.EnterWithMemo(owner)) RouteCatalogCodec.Decode(bytes);
        RouteCatalogEnvelope[] results = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Task.Run(() =>
        {
            using var scope = RouteCatalogDecodeScope.EnterWithMemo(owner);
            RouteCatalogEnvelope result = RouteCatalogCodec.Decode(bytes, Hash(bytes));
            scope.CacheHits.ShouldBe(1);
            return result;
        })));
        results[0].Routes.ShouldNotBeSameAs(results[1].Routes);
        using var outer = RouteCatalogDecodeScope.EnterWithMemo(owner);
        using var foreign = new RouteCatalogValidatedBytesMemo();
        using (var nested = RouteCatalogDecodeScope.EnterWithMemo(foreign))
        {
            nested.Count.ShouldBe(0);
            RouteCatalogCodec.Decode(bytes);
            nested.CacheHits.ShouldBe(0);
        }
        RouteCatalogDecodeScope.Current.ShouldBeSameAs(outer);
        using (RouteCatalogDecodeScope.Suppress())
        {
            RouteCatalogDecodeScope.Current.ShouldBeNull();
            RouteCatalogCodec.Decode(bytes);
            outer.CaptureCount.ShouldBe(0);
        }
        RouteCatalogDecodeScope.Current.ShouldBeSameAs(outer);
    }

    /// <summary>Entry bounds apply across scope lifetimes and cannot affect successful uncached validation.</summary>
    [Fact]
    public void EntriesStayBoundedAcrossIndependentScopes()
    {
        byte[][] documents = Documents(4);
        using var memo = new RouteCatalogValidatedBytesMemo();
        foreach (byte[] bytes in documents)
        {
            using var scope = RouteCatalogDecodeScope.EnterWithMemo(memo);
            RouteCatalogCodec.Decode(bytes, Hash(bytes));
        }
        memo.Count.ShouldBe(3);
        memo.RetainedBytes.ShouldBe(documents.Take(3).Sum(bytes => bytes.Length));
        using var final = RouteCatalogDecodeScope.EnterWithMemo(memo);
        RouteCatalogCodec.Decode(documents[3], Hash(documents[3])).Generation.ShouldBe(4);
        final.CacheHits.ShouldBe(0);
    }

    /// <summary>The hard byte bound is enforced on real valid canonical schema documents across independent scopes.</summary>
    [Fact]
    public void RealLargeDocumentsCannotExceedOwnedByteLimit()
    {
        var fixture = new RouteCatalogTestFixture(compiledSchemaThree: true);
        using JsonDocument schema = JsonDocument.Parse(JsonSerializer.Serialize(new { description = new string('x', 3 * 1024 * 1024) }));
        RouteOperationContract operation = fixture.Envelope.Operations![0] with { RequestSchema = schema.RootElement.Clone() };
        operation = operation with { Digest = RouteCatalogCodec.ComputeOperationContractDigest(operation) };
        using var memo = new RouteCatalogValidatedBytesMemo();
        var lengths = new List<int>();
        for (int generation = 1; generation <= 3; generation++)
        {
            RouteCatalogEnvelope envelope = RouteCatalogCodec.Decode(fixture.Candidate(generation).CanonicalCatalog);
            envelope = envelope with
            {
                Operations = envelope.Operations!.Select(item => item.RouteId == operation.RouteId ? operation : item).ToArray(),
                Routes = envelope.Routes.Select(item => item.RouteId == operation.RouteId ? item with { OperationContractDigest = operation.Digest } : item).ToArray(),
            };
            byte[] bytes = RouteCatalogCodec.Encode(RouteCatalogCodec.Seal(envelope));
            lengths.Add(bytes.Length);
            using var scope = RouteCatalogDecodeScope.EnterWithMemo(memo);
            RouteCatalogCodec.Decode(bytes, Hash(bytes)).Generation.ShouldBe(generation);
        }
        memo.Count.ShouldBe(2);
        memo.RetainedBytes.ShouldBe(lengths.Take(2).Sum());
        memo.RetainedBytes.ShouldBeLessThanOrEqualTo(8 * 1024 * 1024);
    }

    private static byte[][] Documents(int count)
    {
        var fixture = new RouteCatalogTestFixture();
        return Enumerable.Range(1, count).Select(generation => fixture.Candidate(generation).CanonicalCatalog).ToArray();
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
