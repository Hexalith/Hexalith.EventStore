using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Proves validated scalar reuse retains complete current-input checks without retaining graphs or authority decisions.</summary>
public sealed class RouteCatalogProfileFactTests(ITestOutputHelper output)
{
    /// <summary>Cold and warm reads retain exact Profile values, one current capture per call and canonical-byte accounting.</summary>
    [Fact]
    public void ColdWarmAndPublicDecodeKeepParityCountersAndFreshGraphs()
    {
        byte[] bytes = Document(1);
        string root = Hash(bytes);
        using var scope = RouteCatalogDecodeScope.Enter();
        RouteCatalogCodec.ReadProfile(bytes, root).ShouldBe("Development");
        scope.CaptureCount.ShouldBe(1); scope.CacheHits.ShouldBe(0);
        RouteCatalogCodec.ReadProfile(bytes, root).ShouldBe("Development");
        scope.CaptureCount.ShouldBe(2); scope.CacheHits.ShouldBe(1);
        RouteCatalogEnvelope first = RouteCatalogCodec.Decode(bytes, root);
        RouteCatalogEnvelope second = RouteCatalogCodec.Decode(bytes, root);
        first.ShouldNotBeSameAs(second); first.Declarations.ShouldNotBeSameAs(second.Declarations);
        typeof(RouteCatalogEnvelope).GetProperty(nameof(RouteCatalogEnvelope.Profile))!.SetValue(first, "Poisoned");
        ((IList<RouteCatalogEntry>)first.Routes)[0] = first.Routes[0] with { AppId = "poisoned-owner" };
        RouteCatalogCodec.ReadProfile(bytes, root).ShouldBe("Development");
        RouteCatalogCodec.Decode(bytes, root).Routes[0].AppId.ShouldBe(second.Routes[0].AppId);
        scope.CaptureCount.ShouldBe(6); scope.CacheHits.ShouldBe(5);
        scope.RetainedBytes.ShouldBe(bytes.Length);
    }

    /// <summary>Warm facts never replace original malformed/unknown/noncanonical/size/root failure precedence.</summary>
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("duplicate")]
    [InlineData("noncanonical")]
    [InlineData("wrong-root")]
    [InlineData("changed-valid-bytes")]
    [InlineData("empty")]
    [InlineData("oversized")]
    public void WarmErrorsMatchOriginalDecodeExactly(string scenario)
    {
        byte[] original = Document(1);
        string root = Hash(original);
        byte[] current = scenario switch
        {
            "malformed" => "{malformed"u8.ToArray(),
            "unknown" => Encoding.UTF8.GetBytes("{\"unknown\":null," + Encoding.UTF8.GetString(original)[1..]),
            "duplicate" => Encoding.UTF8.GetBytes("{\"generation\":1," + Encoding.UTF8.GetString(original)[1..]),
            "noncanonical" => [.. " "u8, .. original],
            "changed-valid-bytes" => Document(2),
            "empty" => [],
            "oversized" => new byte[RouteCatalogCodec.MaximumByteLength + 1],
            _ => original,
        };
        string expectedRoot = scenario == "wrong-root" ? new string('0', 64) : root;
        var expected = Should.Throw<RouteCatalogValidationException>(() => RouteCatalogCodec.Decode(current, expectedRoot));
        using var memo = new RouteCatalogValidatedBytesMemo();
        using var scope = RouteCatalogDecodeScope.EnterWithMemo(memo);
        RouteCatalogCodec.ReadProfile(original, root).ShouldBe("Development");

        var actual = Should.Throw<RouteCatalogValidationException>(() => RouteCatalogCodec.ReadProfile(current, expectedRoot));

        actual.Diagnostics.ShouldBe(expected.Diagnostics); actual.Message.ShouldBe(expected.Message);
        memo.Count.ShouldBe(1); memo.RetainedBytes.ShouldBe(original.Length);
        RouteCatalogCodec.ReadProfile(original, root).ShouldBe("Development");
    }

    /// <summary>Expected-root failure cannot remember either canonical bytes or a scalar, including a cold first read.</summary>
    [Fact]
    public void ColdRootMismatchRetainsNoFactOrCanonicalInput()
    {
        byte[] bytes = Document(1);
        using var memo = new RouteCatalogValidatedBytesMemo();
        using var scope = RouteCatalogDecodeScope.EnterWithMemo(memo);
        Should.Throw<RouteCatalogValidationException>(() => RouteCatalogCodec.ReadProfile(bytes, new string('0', 64)))
            .Diagnostics.Single().Code.ShouldBe("root-digest-mismatch");
        memo.Count.ShouldBe(0); memo.RetainedBytes.ShouldBe(0);
        scope.CaptureCount.ShouldBe(1); scope.CacheHits.ShouldBe(0);
    }

    /// <summary>The decoder privately owns source bytes; changing a valid same-size Profile requires a fresh correct digest.</summary>
    [Fact]
    public void MutableCallerBytesCannotChangeRememberedProfile()
    {
        byte[] bytes = Document(1);
        byte[] original = bytes.ToArray();
        string root = Hash(bytes);
        using var scope = RouteCatalogDecodeScope.Enter();
        RouteCatalogCodec.ReadProfile(bytes, root).ShouldBe("Development");
        int at = Encoding.UTF8.GetString(bytes).IndexOf("Development", StringComparison.Ordinal);
        at.ShouldBeGreaterThan(0); bytes[at] = (byte)'P';
        Should.Throw<RouteCatalogValidationException>(() => RouteCatalogCodec.ReadProfile(bytes, root))
            .Diagnostics.Single().Code.ShouldBe("root-digest-mismatch");
        RouteCatalogCodec.ReadProfile(bytes, Hash(bytes)).ShouldBe("Pevelopment");
        RouteCatalogCodec.ReadProfile(original, root).ShouldBe("Development");
        scope.Count.ShouldBe(2);
    }

    /// <summary>Legacy byte-only memo entries fall back to the complete decoder, then may acquire a validated bounded scalar.</summary>
    [Fact]
    public void MissingOrOversizedFactFallsBackAndLearnsOnlyAfterDecodeSuccess()
    {
        byte[] bytes = Document(1); string root = Hash(bytes);
        using var memo = new RouteCatalogValidatedBytesMemo();
        memo.Remember(root, bytes.ToArray(), new string('x', 129));
        memo.Matches(root, bytes, out string? absent).ShouldBeTrue(); absent.ShouldBeNull();
        using var scope = RouteCatalogDecodeScope.EnterWithMemo(memo);
        RouteCatalogCodec.ReadProfile(bytes, root).ShouldBe("Development");
        memo.Matches(root, bytes, out string? profile).ShouldBeTrue(); profile.ShouldBe("Development");
        scope.CaptureCount.ShouldBe(1); scope.CacheHits.ShouldBe(1);
        memo.RetainedBytes.ShouldBe(bytes.Length);
    }

    /// <summary>At most three separately bounded 128-character scalars accompany the unchanged canonical byte budget.</summary>
    [Fact]
    public void ProfileFactsRespectDocumentAndTokenBoundsAndClearOnDisposal()
    {
        byte[][] documents = Enumerable.Range(1, 4).Select(index => Document(index, "P" + new string((char)('a' + index), 127))).ToArray();
        using var memo = new RouteCatalogValidatedBytesMemo();
        using var scope = RouteCatalogDecodeScope.EnterWithMemo(memo);
        foreach (byte[] bytes in documents)
            RouteCatalogCodec.ReadProfile(bytes, Hash(bytes)).Length.ShouldBe(128);
        memo.Count.ShouldBe(3); memo.RetainedBytes.ShouldBe(documents.Take(3).Sum(value => value.Length));
        for (int index = 0; index < documents.Length; index++)
        {
            bool retained = memo.Matches(Hash(documents[index]), documents[index], out string? profile);
            retained.ShouldBe(index < 3);
            if (retained) profile!.Length.ShouldBe(128);
        }
        memo.Dispose();
        memo.Count.ShouldBe(0); memo.RetainedBytes.ShouldBe(0);
        memo.Matches(Hash(documents[0]), documents[0], out _).ShouldBeFalse();
        RouteCatalogCodec.ReadProfile(documents[0], Hash(documents[0])).Length.ShouldBe(128);
        memo.Count.ShouldBe(0);
    }

    /// <summary>Foreign and closed contexts cannot borrow or repopulate another owner's facts.</summary>
    [Fact]
    public async Task ForeignAndClosedScopesUseIndependentFactsOrCompleteFallback()
    {
        byte[] bytes = Document(1); string root = Hash(bytes);
        using var first = new RouteCatalogValidatedBytesMemo(); using var second = new RouteCatalogValidatedBytesMemo();
        var outer = RouteCatalogDecodeScope.EnterWithMemo(first);
        RouteCatalogCodec.ReadProfile(bytes, root).ShouldBe("Development");
        using (RouteCatalogDecodeScope.Suppress())
        {
            RouteCatalogDecodeScope.Current.ShouldBeNull();
            using var inner = RouteCatalogDecodeScope.EnterWithMemo(second);
            RouteCatalogCodec.ReadProfile(bytes, root).ShouldBe("Development"); inner.CacheHits.ShouldBe(0);
        }
        RouteCatalogDecodeScope.Current.ShouldBeSameAs(outer);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task escaped = Task.Run(async () =>
        {
            await resume.Task;
            RouteCatalogDecodeScope.Current.ShouldBeSameAs(outer);
            RouteCatalogCodec.ReadProfile(bytes, root).ShouldBe("Development");
            Should.Throw<RouteCatalogValidationException>(() => RouteCatalogCodec.ReadProfile(bytes, new string('0', 64)));
            outer.CaptureCount.ShouldBe(0); outer.CacheHits.ShouldBe(0);
        }, TestContext.Current.CancellationToken);
        outer.Dispose(); resume.SetResult(); await escaped;
        first.Count.ShouldBe(1); second.Count.ShouldBe(1);
        RouteCatalogDecodeScope.Current.ShouldBeNull();
    }

    /// <summary>Lifecycle/readback calls suppress an unrelated active fact scope while keeping their original fresh observations.</summary>
    [Fact]
    public async Task LifecycleChecksDoNotBorrowAnUnrelatedProfileFact()
    {
        var fixture = new RouteCatalogTestFixture();
        RouteCatalogActivation original = fixture.Candidate();
        await fixture.ActivateAsync(original);
        RouteCatalogActivation next = fixture.Candidate(2, original.RootDigest);
        int observations = 0;
        fixture.Recapture = actual => { observations++; RouteCatalogDecodeScope.Current.ShouldBeNull(); return actual; };
        using var scope = RouteCatalogDecodeScope.Enter();
        RouteCatalogCodec.ReadProfile(original.CanonicalCatalog, original.RootDigest);
        int captures = scope.CaptureCount, hits = scope.CacheHits;
        await fixture.Authorities[0].ReadbackAsync(TestContext.Current.CancellationToken);
        await fixture.Authorities[0].PrepareAsync(next, TestContext.Current.CancellationToken);
        observations.ShouldBe(2);
        RouteCatalogDecodeScope.Current.ShouldBeSameAs(scope);
        scope.CaptureCount.ShouldBe(captures); scope.CacheHits.ShouldBe(hits);
    }

    /// <summary>Concurrent scopes share complete validated input facts while every public result graph stays independently owned.</summary>
    [Fact]
    public async Task ConcurrentProfileReadsKeepFreshIndependentPublicGraphs()
    {
        byte[] bytes = Document(1); string root = Hash(bytes);
        using var memo = new RouteCatalogValidatedBytesMemo();
        var results = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() =>
        {
            using var scope = RouteCatalogDecodeScope.EnterWithMemo(memo);
            string profile = RouteCatalogCodec.ReadProfile(bytes, root);
            return (profile, graph: RouteCatalogCodec.Decode(bytes, root));
        }, TestContext.Current.CancellationToken)));
        results.ShouldAllBe(value => value.profile == "Development");
        results[0].graph.Declarations.ShouldNotBeSameAs(results[1].graph.Declarations);
        typeof(RouteCatalogEnvelope).GetProperty(nameof(RouteCatalogEnvelope.Profile))!.SetValue(results[0].graph, "Poisoned");
        results[1].graph.Profile.ShouldBe("Development");
        memo.Count.ShouldBe(1); memo.RetainedBytes.ShouldBe(bytes.Length);
        RouteCatalogDecodeScope.Current.ShouldBeNull();
    }

    /// <summary>Records real two-read Profile-only versus fresh-graph costs including fresh capture, actual SHA/equality and scope disposal.</summary>
    [Fact]
    public void MeasureActualProfileFactReadsIncludingScopeDisposal()
    {
        byte[] bytes = NativeReadDecodeReuseTests.RepresentativeCatalog(); string root = Hash(bytes);
        using var memo = new RouteCatalogValidatedBytesMemo();
        long firstStarted = Stopwatch.GetTimestamp(), firstAllocated = GC.GetAllocatedBytesForCurrentThread();
        using (var first = RouteCatalogDecodeScope.EnterWithMemo(memo)) RouteCatalogCodec.ReadProfile(bytes, root).ShouldBe("Development");
        double firstScopedMs = Stopwatch.GetElapsedTime(firstStarted).TotalMilliseconds;
        long firstScopedAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - firstAllocated;
        (double elapsed, long allocated, int captures, int hits) Measure(bool fact)
        {
            long allocated = GC.GetAllocatedBytesForCurrentThread(), started = Stopwatch.GetTimestamp();
            int captures, hits;
            using (var scope = RouteCatalogDecodeScope.EnterWithMemo(memo))
            {
                string first = fact ? RouteCatalogCodec.ReadProfile(bytes, root) : RouteCatalogCodec.Decode(bytes, root).Profile;
                string second = fact ? RouteCatalogCodec.ReadProfile(bytes, root) : RouteCatalogCodec.Decode(bytes, root).Profile;
                if (first != "Development" || second != "Development") throw new InvalidOperationException("Profile benchmark parity failure.");
                captures = scope.CaptureCount; hits = scope.CacheHits;
            }
            return (Stopwatch.GetElapsedTime(started).TotalMilliseconds, GC.GetAllocatedBytesForCurrentThread() - allocated, captures, hits);
        }
        for (int warmup = 0; warmup < 80; warmup++) { Measure(false); Measure(true); }
        var samples = new List<object>();
        for (int sample = 0; sample < 7; sample++)
        {
            double fullMs = 0, factMs = 0; long fullBytes = 0, factBytes = 0;
            for (int iteration = 0; iteration < 80; iteration++)
            {
                var full = Measure(false); var fact = Measure(true);
                full.captures.ShouldBe(2); full.hits.ShouldBe(2); fact.captures.ShouldBe(2); fact.hits.ShouldBe(2);
                fullMs += full.elapsed; factMs += fact.elapsed; fullBytes += full.allocated; factBytes += fact.allocated;
            }
            samples.Add(new { sample, fullDecodePairMs = fullMs / 80, profileFactPairMs = factMs / 80,
                fullDecodePairAllocatedBytes = fullBytes / 80, profileFactPairAllocatedBytes = factBytes / 80 });
        }
        output.WriteLine(JsonSerializer.Serialize(new { catalogBytes = bytes.Length, structuredSchemaProperties = 1000,
            completeOwnedCaptureShaRootEqualityAndScopeDisposal = true, captureCountPerPair = 2, hitCountPerPair = 2,
            firstScopedMs, firstScopedAllocatedBytes, measurements = samples }));
    }

    private static byte[] Document(int generation, string profile = "Development")
    {
        var fixture = new RouteCatalogTestFixture();
        RouteCatalogEnvelope envelope = RouteCatalogCodec.Decode(fixture.Candidate(generation).CanonicalCatalog);
        return RouteCatalogCodec.Encode(RouteCatalogCodec.Seal(envelope with { Profile = profile }));
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
