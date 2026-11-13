using System.Buffers;
using System.Reflection;
using System.Security.Cryptography;

using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Checks private concurrent storage reuse without changing fresh image observations or scope ownership.</summary>
[Collection("RouteCatalogImageBufferPool")]
public sealed class RouteCatalogImageBufferPoolTests
{
    /// <summary>Independent scopes share only storage; suppression and completion restore the caller's ordinary pool.</summary>
    [Fact]
    public void OpenScopesShareStorageWhileForeignSuppressionRestoresOrdinarySelection()
    {
        var ordinary = ArrayPool<byte>.Create(4096, 1);
        RouteCatalogImageBuffers.Select(ordinary).ShouldBeSameAs(ordinary);
        using var firstMemo = new RouteCatalogImageDigestMemo();
        using var secondMemo = new RouteCatalogImageDigestMemo();
        using (var first = RouteCatalogImageDigestScope.Enter(firstMemo))
        {
            var selected = RouteCatalogImageBuffers.Select(ordinary);
            selected.ShouldNotBeSameAs(ordinary);
            using (RouteCatalogImageDigestScope.Suppress())
            {
                RouteCatalogImageBuffers.Select(ordinary).ShouldBeSameAs(ordinary);
                using var second = RouteCatalogImageDigestScope.Enter(secondMemo);
                RouteCatalogImageBuffers.Select(ordinary).ShouldBeSameAs(selected);
                using var image = new FragmentedExecutableImageStream([1, 2, 3], 1);
                RouteCatalogFreshStreamImageDigest.Compute(typeof(RouteCatalogAuthority).Assembly, image)
                    .ShouldBe(Hash([1, 2, 3]));
                image.BytesRead.ShouldBe(3); image.EofReads.ShouldBe(1);
            }
            firstMemo.Count.ShouldBe(0); secondMemo.Count.ShouldBe(1);
            RouteCatalogImageBuffers.Select(ordinary).ShouldBeSameAs(selected);
        }
        RouteCatalogImageBuffers.Select(ordinary).ShouldBeSameAs(ordinary);
    }

    /// <summary>Detached work inheriting a closed scope or disposed memo cannot select private storage or retain fresh inputs.</summary>
    [Theory]
    [InlineData("scope")]
    [InlineData("memo")]
    public async Task EscapedClosedOwnersUseOrdinaryStorageAndCompleteFreshStreaming(string close)
    {
        var ordinary = ArrayPool<byte>.Create(4096, 1);
        using var memo = new RouteCatalogImageDigestMemo();
        using var scope = RouteCatalogImageDigestScope.Enter(memo);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task child = Task.Run(async () =>
        {
            await resume.Task;
            RouteCatalogImageDigestScope.Current.ShouldBeSameAs(scope);
            RouteCatalogImageBuffers.Select(ordinary).ShouldBeSameAs(ordinary);
            using var image = new FragmentedExecutableImageStream([5, 7, 11], 1);
            RouteCatalogFreshStreamImageDigest.Compute(typeof(RouteCatalogAuthority).Assembly, image)
                .ShouldBe(Hash([5, 7, 11]));
            image.BytesRead.ShouldBe(3); image.EofReads.ShouldBe(1);
        }, TestContext.Current.CancellationToken);
        if (close == "scope") scope.Dispose();
        else memo.Dispose();
        resume.SetResult();
        await child.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        memo.Count.ShouldBe(0);
    }

    /// <summary>Overlapping warm loans remain disjoint and reuse image-sized storage instead of allocating discarded arrays.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FourConcurrentWarmLoansPreserveOwnershipAndBoundImageAllocation(bool selected)
    {
        const int readers = 4, requested = 2 * 1024 * 1024 - 17, warmups = 3, measured = 6;
        var ordinary = ArrayPool<byte>.Create(16 * 1024 * 1024, 1);
        using var barrier = new Barrier(readers);
        long[] allocated = new long[readers];
        Exception? failure = null;
        Thread[] threads = Enumerable.Range(0, readers).Select(worker => new Thread(() =>
        {
            using var scope = selected ? RouteCatalogImageDigestScope.Enter() : null;
            ArrayPool<byte> pool = RouteCatalogImageBuffers.Select(ordinary);
            long started = 0;
            for (int iteration = 0; iteration < warmups + measured; iteration++)
            {
                if (iteration == warmups) started = GC.GetAllocatedBytesForCurrentThread();
                byte[] bytes = pool.Rent(requested);
                try
                {
                    bytes[0] = bytes[requested - 1] = (byte)(worker + 1);
                    barrier.SignalAndWait();
                    if (bytes[0] != worker + 1 || bytes[requested - 1] != worker + 1)
                        Interlocked.CompareExchange(ref failure, new InvalidOperationException("Concurrent readers shared an outstanding image buffer."), null);
                    barrier.SignalAndWait();
                }
                finally { pool.Return(bytes); }
                barrier.SignalAndWait();
            }
            allocated[worker] = GC.GetAllocatedBytesForCurrentThread() - started;
        }) { IsBackground = true }).ToArray();
        foreach (var thread in threads) thread.Start();
        foreach (var thread in threads) thread.Join(TimeSpan.FromSeconds(10)).ShouldBeTrue();
        failure.ShouldBeNull();
        if (selected) allocated.Sum().ShouldBeLessThan(requested);
        else allocated.Sum().ShouldBeGreaterThan((long)requested * measured);
        RouteCatalogImageDigestScope.Current.ShouldBeNull();
    }

    /// <summary>Both default readers overwrite borrowed storage and still observe same-length changes outside PE metadata.</summary>
    [Fact]
    public void DefaultReadersKeepFreshImageAndDigestAfterPoolPoisoningAndMutation()
    {
        string directory = Directory.CreateTempSubdirectory("catalog-private-pool-").FullName;
        try
        {
            Assembly actual = typeof(RouteCatalogAuthority).Assembly;
            string path = Path.Combine(directory, "image.dll");
            File.Copy(actual.Location, path);
            using (var file = new FileStream(path, FileMode.Append, FileAccess.Write)) file.Write(new byte[31]);
            Assembly image = new RouteCatalogDiskImageAssembly(actual, path);
            using var memo = new RouteCatalogImageDigestMemo();
            using var scope = RouteCatalogImageDigestScope.Enter(memo);
            string expected = Hash(File.ReadAllBytes(path));
            string identity = TechnicalHttpActionObservation.AssemblyIdentity(image);
            identity.ShouldEndWith(":" + expected);
            DevelopmentDomainRouteCatalog.ReadExecutableDigest(image).ShouldBe(expected);
            var pool = RouteCatalogImageBuffers.Select(ArrayPool<byte>.Create(4096, 1));
            byte[] borrowed = pool.Rent(checked((int)new FileInfo(path).Length));
            Array.Fill(borrowed, (byte)0xcc);
            pool.Return(borrowed);
            TechnicalHttpActionObservation.AssemblyIdentity(image).ShouldBe(identity);
            DevelopmentDomainRouteCatalog.ReadExecutableDigest(image).ShouldBe(expected);
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Write))
            {
                file.Position = file.Length - 1;
                file.WriteByte(0x5a);
            }
            string changed = Hash(File.ReadAllBytes(path));
            changed.ShouldNotBe(expected);
            TechnicalHttpActionObservation.AssemblyIdentity(image).ShouldEndWith(":" + changed);
            DevelopmentDomainRouteCatalog.ReadExecutableDigest(image).ShouldBe(changed);
            memo.Count.ShouldBe(1);
        }
        finally { Directory.Delete(directory, true); }
    }

    /// <summary>Repeated failing default captures return their selected loans rather than allocating replacement image arrays.</summary>
    [Fact]
    public void DefaultStreamFailuresReturnSelectedBuffersWithoutRetainingPartialImages()
    {
        byte[] bytes = new byte[2 * 1024 * 1024 - 17];
        using var memo = new RouteCatalogImageDigestMemo();
        using var scope = RouteCatalogImageDigestScope.Enter(memo);
        void Fail()
        {
            using var image = new FragmentedExecutableImageStream(bytes, 1009, bytes.Length) { ThrowAfterBytes = 1009 };
            Should.Throw<IOException>(() => RouteCatalogFreshStreamImageDigest.Compute(typeof(RouteCatalogAuthority).Assembly, image));
            image.BytesRead.ShouldBe(1009); image.EofReads.ShouldBe(0);
        }
        for (int iteration = 0; iteration < 4; iteration++) Fail();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int iteration = 0; iteration < 8; iteration++) Fail();
        (GC.GetAllocatedBytesForCurrentThread() - before).ShouldBeLessThan(bytes.Length);
        memo.Count.ShouldBe(0);
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

}
