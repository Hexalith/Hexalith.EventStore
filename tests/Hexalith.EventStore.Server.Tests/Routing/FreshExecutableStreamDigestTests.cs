using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Checks complete original-stream observations, bounded capture and existing owner-scoped SHA computation reuse.</summary>
public sealed class FreshExecutableStreamDigestTests(ITestOutputHelper output)
{
    /// <summary>Short reads and smaller allocation hints cannot truncate either cold or warm observations.</summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(4097, 0)]
    [InlineData(4097, 16777217)]
    [InlineData(131089, 1024)]
    [InlineData(4194321, -1)]
    [InlineData(16777216, 4096)]
    [InlineData(16777233, 1024)]
    [InlineData(16777233, 16777233)]
    public void CompleteEofAndGrowthAreObservedOnceWithBoundedCapture(int length, int hint)
    {
        byte[] bytes = new byte[length];
        for (int index = 0; index < bytes.Length; index += 1009) bytes[index] = (byte)(index % 251);
        string expected = Convert.ToHexStringLower(SHA256.HashData(bytes));
        Assembly key = typeof(RouteCatalogAuthority).Assembly;
        using var memo = new RouteCatalogImageDigestMemo();
        using var scope = RouteCatalogImageDigestScope.Enter(memo);
        var pool = new RecordingExecutableImagePool();
        for (int iteration = 0; iteration < 2; iteration++)
        {
            using var image = new FragmentedExecutableImageStream(bytes, 1009, hint);
            if (length > 2048)
            {
                image.VisibleLength = 2048;
                image.AfterRead = current => { if (current.BytesRead >= 1009) current.VisibleLength = length; };
            }
            RouteCatalogFreshStreamImageDigest.Compute(key, image, pool).ShouldBe(expected);
            image.BytesRead.ShouldBe(length); image.EofReads.ShouldBe(1); image.SeekCalls.ShouldBe(0);
            pool.Outstanding.ShouldBe(0); pool.Returns.ShouldBe(pool.Rents);
            pool.MaximumRequested.ShouldBeLessThanOrEqualTo(16 * 1024 * 1024);
        }
        bool retained = length is > 0 and <= 16 * 1024 * 1024 && hint <= 16 * 1024 * 1024;
        memo.Count.ShouldBe(retained ? 1 : 0);
        scope.CacheHits.ShouldBe(retained ? 1 : 0);
    }

    /// <summary>Read failure after growth returns every rented buffer and cannot remember a partial image.</summary>
    [Fact]
    public void FailedReadNeverRetainsPartialBytesOrLeaksAGrownBuffer()
    {
        using var memo = new RouteCatalogImageDigestMemo();
        using var scope = RouteCatalogImageDigestScope.Enter(memo);
        using var image = new FragmentedExecutableImageStream(new byte[100000], 1009, 0) { ThrowAfterBytes = 10000 };
        var pool = new RecordingExecutableImagePool();
        Should.Throw<IOException>(() => RouteCatalogFreshStreamImageDigest.Compute(typeof(RouteCatalogAuthority).Assembly, image, pool));
        pool.Rents.ShouldBeGreaterThan(1); pool.Outstanding.ShouldBe(0); pool.Returns.ShouldBe(pool.Rents);
        memo.Count.ShouldBe(0);
    }

    /// <summary>Closing the scope during a complete read falls back to the exact SHA without retaining the captured bytes.</summary>
    [Fact]
    public void ScopeClosureDuringReadStillConsumesOriginalEofAndHashesEveryByte()
    {
        byte[] bytes = new byte[65553]; bytes[^1] = 71;
        using var memo = new RouteCatalogImageDigestMemo();
        using var scope = RouteCatalogImageDigestScope.Enter(memo);
        using var image = new FragmentedExecutableImageStream(bytes, 1009, 0) { AfterRead = _ => scope.Dispose() };
        var pool = new RecordingExecutableImagePool();
        RouteCatalogFreshStreamImageDigest.Compute(typeof(RouteCatalogAuthority).Assembly, image, pool)
            .ShouldBe(Convert.ToHexStringLower(SHA256.HashData(bytes)));
        image.BytesRead.ShouldBe(bytes.Length); image.EofReads.ShouldBe(1); image.SeekCalls.ShouldBe(0);
        pool.Outstanding.ShouldBe(0); memo.Count.ShouldBe(0);
    }

    /// <summary>Suppressed foreign scopes use ordinary streaming SHA with no new capture allocation.</summary>
    [Fact]
    public void ForeignSuppressionAndClosedMemoKeepOrdinaryStreamingFallback()
    {
        byte[] bytes = new byte[131089]; bytes[^1] = 91;
        string expected = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var pool = new RecordingExecutableImagePool();
        using var memo = new RouteCatalogImageDigestMemo();
        using var scope = RouteCatalogImageDigestScope.Enter(memo);
        using (RouteCatalogImageDigestScope.Suppress())
        using (var image = new FragmentedExecutableImageStream(bytes, 127))
        {
            RouteCatalogFreshStreamImageDigest.Compute(typeof(RouteCatalogAuthority).Assembly, image, pool).ShouldBe(expected);
            image.BytesRead.ShouldBe(bytes.Length); image.EofReads.ShouldBe(1);
        }
        pool.Rents.ShouldBe(0); memo.Count.ShouldBe(0);
        memo.Dispose();
        using var closed = new FragmentedExecutableImageStream(bytes, 257);
        RouteCatalogFreshStreamImageDigest.Compute(typeof(RouteCatalogAuthority).Assembly, closed, pool).ShouldBe(expected);
        closed.BytesRead.ShouldBe(bytes.Length); closed.EofReads.ShouldBe(1);
        pool.Rents.ShouldBe(0);
    }

    /// <summary>Many parallel complete file observations use only the supplied memo's successful complete SHA result.</summary>
    [Fact]
    public async Task ConcurrentActualExecutableReadsKeepExactDigestAndOwnerIsolation()
    {
        Assembly assembly = typeof(DevelopmentDomainRouteCatalog).Assembly;
        string expected = DevelopmentDomainRouteCatalog.ReadExecutableDigest(assembly.Location);
        using var first = new RouteCatalogImageDigestMemo();
        using var second = new RouteCatalogImageDigestMemo();
        await Task.WhenAll(Enumerable.Range(0, 12).Select(index => Task.Run(() =>
        {
            using var scope = RouteCatalogImageDigestScope.Enter(index % 2 == 0 ? first : second);
            DevelopmentDomainRouteCatalog.ReadExecutableDigest(assembly).ShouldBe(expected);
        }, TestContext.Current.CancellationToken)));
        first.Count.ShouldBe(1); second.Count.ShouldBe(1);
        RouteCatalogImageDigestScope.Current.ShouldBeNull();
    }

    /// <summary>Measures complete fresh FileStream/EOF/capture/scope/memo costs, with separately recorded first cold retention.</summary>
    [Fact]
    public void MeasureCompleteWarmExecutableStreamObservation()
    {
        foreach (Assembly assembly in new[] { typeof(DevelopmentDomainRouteCatalog).Assembly, typeof(RouteCatalogAuthority).Assembly, typeof(object).Assembly })
        {
            string expected = DevelopmentDomainRouteCatalog.ReadExecutableDigest(assembly.Location);
            using var memo = new RouteCatalogImageDigestMemo();
            long coldAllocated = GC.GetAllocatedBytesForCurrentThread(), coldStarted = Stopwatch.GetTimestamp();
            using (var scope = RouteCatalogImageDigestScope.Enter(memo)) DevelopmentDomainRouteCatalog.ReadExecutableDigest(assembly).ShouldBe(expected);
            double firstScopedMs = Stopwatch.GetElapsedTime(coldStarted).TotalMilliseconds;
            long firstScopedAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - coldAllocated;
            for (int warmup = 0; warmup < 16; warmup++)
            {
                DevelopmentDomainRouteCatalog.ReadExecutableDigest(assembly.Location);
                using var scope = RouteCatalogImageDigestScope.Enter(memo);
                DevelopmentDomainRouteCatalog.ReadExecutableDigest(assembly);
            }
            var samples = new List<object>();
            for (int sample = 0; sample < 5; sample++)
            {
                (double elapsed, long allocated) Measure(bool owned)
                {
                    long allocated = GC.GetAllocatedBytesForCurrentThread(), started = Stopwatch.GetTimestamp();
                    for (int iteration = 0; iteration < 40; iteration++)
                    {
                        using var boundary = RouteCatalogImageDigestScope.Suppress();
                        using var scope = owned ? RouteCatalogImageDigestScope.Enter(memo) : null;
                        DevelopmentDomainRouteCatalog.ReadExecutableDigest(assembly).ShouldBe(expected);
                    }
                    return (Stopwatch.GetElapsedTime(started).TotalMilliseconds / 40,
                        (GC.GetAllocatedBytesForCurrentThread() - allocated) / 40);
                }
                var ordinary = Measure(false); var owned = Measure(true);
                samples.Add(new { sample, ordinaryMs = ordinary.elapsed, ownedMs = owned.elapsed,
                    ordinaryAllocatedBytes = ordinary.allocated, ownedAllocatedBytes = owned.allocated });
            }
            output.WriteLine(JsonSerializer.Serialize(new { assembly = assembly.GetName().Name, imageBytes = new FileInfo(assembly.Location).Length,
                freshFullOriginalStreamToEof = true, perCallScopeIncluded = true, firstScopedMs, firstScopedAllocatedBytes, measurements = samples }));
        }
    }
}
