using System.Buffers;
using System.Reflection;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Text.Json;

using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Fresh image ownership, exact byte slices and refusal cleanup survive private buffer reuse.</summary>
public sealed class TechnicalHttpImageObservationTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EveryReadHashesOnlyFreshImageBytesAndReturnsTheBufferAfterUse(bool enabled)
    {
        using var scope = enabled ? RouteCatalogImageDigestScope.Enter() : null;
        using var image = new ImageFixture();
        var pool = new RecordingPool();
        string expected = Expected(image.Assembly);
        TechnicalHttpActionObservation.AssemblyIdentity(image.Assembly, pool).ShouldBe(expected);
        TechnicalHttpActionObservation.AssemblyIdentity(image.Assembly, pool).ShouldBe(expected);
        pool.Rents.ShouldBe(2); pool.Returns.ShouldBe(2);
        pool.Buffer!.ShouldAllBe(value => value == 0xcc);
        if (enabled) scope!.CacheHits.ShouldBe(1);
    }

    [Theory]
    [InlineData("different-module", false)]
    [InlineData("different-module", true)]
    [InlineData("invalid-pe", false)]
    [InlineData("invalid-pe", true)]
    [InlineData("truncated", true)]
    public void FailedImageValidationReturnsItsOwnedBufferAndClosesTheFile(string failure, bool enabled)
    {
        using var scope = enabled ? RouteCatalogImageDigestScope.Enter() : null;
        using var image = new ImageFixture();
        TechnicalHttpActionObservation.AssemblyIdentity(image.Assembly);
        byte[] replacement = failure == "different-module"
            ? File.ReadAllBytes(typeof(Hexalith.EventStore.Contracts.Routing.RouteCatalogActivation).Assembly.Location) : [1, 2, 3];
        if (failure == "truncated")
        {
            using var file = new FileStream(image.Assembly.Location, FileMode.Open, FileAccess.Write);
            file.SetLength(64);
        }
        else File.WriteAllBytes(image.Assembly.Location, replacement);
        var pool = new RecordingPool();
        if (failure == "different-module")
            Should.Throw<RouteCatalogUnavailableException>(() => TechnicalHttpActionObservation.AssemblyIdentity(image.Assembly, pool))
                .Message.ShouldBe("The installed technical SDK image differs from its loaded module.");
        else
            Should.Throw<BadImageFormatException>(() => TechnicalHttpActionObservation.AssemblyIdentity(image.Assembly, pool));
        pool.Rents.ShouldBe(1); pool.Returns.ShouldBe(1);
        File.Delete(image.Assembly.Location);
        File.Exists(image.Assembly.Location).ShouldBeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingImagePreservesReadFailureWithoutBorrowingABuffer(bool enabled)
    {
        using var scope = enabled ? RouteCatalogImageDigestScope.Enter() : null;
        using var image = new ImageFixture();
        TechnicalHttpActionObservation.AssemblyIdentity(image.Assembly);
        File.Delete(image.Assembly.Location);
        var pool = new RecordingPool();
        Should.Throw<FileNotFoundException>(() => TechnicalHttpActionObservation.AssemblyIdentity(image.Assembly, pool));
        pool.Rents.ShouldBe(0); pool.Returns.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LargerImagesKeepTheExistingUnpooledReadAndExactHash(bool enabled)
    {
        using var scope = enabled ? RouteCatalogImageDigestScope.Enter() : null;
        using var image = new ImageFixture();
        using (var file = new FileStream(image.Assembly.Location, FileMode.Open, FileAccess.Write))
            file.SetLength(16 * 1024 * 1024 + 1);
        var pool = new RecordingPool();
        TechnicalHttpActionObservation.AssemblyIdentity(image.Assembly, pool).ShouldBe(Expected(image.Assembly));
        pool.Rents.ShouldBe(0); pool.Returns.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompleteStillRejectsChangedImageBytesWithTheSameLoadedModule(bool enabled)
    {
        using var scope = enabled ? RouteCatalogImageDigestScope.Enter() : null;
        using var image = new ImageFixture();
        var services = new ServiceCollection();
        services.AddSingleton(Options.Create(new JsonOptions()));
        using var provider = services.BuildServiceProvider();
        var pass = new TechnicalHttpObservationPass(provider);
        string before = pass.AssemblyIdentity(image.Assembly);
        using (var file = new FileStream(image.Assembly.Location, FileMode.Append, FileAccess.Write)) file.WriteByte(0x5a);
        TechnicalHttpActionObservation.AssemblyIdentity(image.Assembly).ShouldNotBe(before);
        Should.Throw<RouteCatalogUnavailableException>(pass.Complete).Message.ShouldBe("Technical HTTP loaded image changed during observation.");
        File.Delete(image.Assembly.Location);
        File.Exists(image.Assembly.Location).ShouldBeFalse();
    }

    /// <summary>Same-length changes outside metadata retain the loaded MVID but must change the complete digest and refuse Complete.</summary>
    [Fact]
    public void WarmSameLengthImageMutationOutsideMetadataCannotReuseOldSha()
    {
        using var image = new ImageFixture();
        using (var file = new FileStream(image.Assembly.Location, FileMode.Open, FileAccess.Write)) file.SetLength(file.Length + 128);
        using var scope = RouteCatalogImageDigestScope.Enter();
        var services = new ServiceCollection().AddSingleton(Options.Create(new JsonOptions()));
        using var provider = services.BuildServiceProvider();
        var pass = new TechnicalHttpObservationPass(provider);
        string before = pass.AssemblyIdentity(image.Assembly);
        TechnicalHttpActionObservation.AssemblyIdentity(image.Assembly).ShouldBe(before);
        scope.CacheHits.ShouldBe(1);
        using (var file = new FileStream(image.Assembly.Location, FileMode.Open, FileAccess.Write))
        {
            file.Position = file.Length - 1;
            file.WriteByte(0x5a);
        }
        string changed = TechnicalHttpActionObservation.AssemblyIdentity(image.Assembly);
        changed.ShouldBe(Expected(image.Assembly));
        changed.ShouldNotBe(before);
        scope.CacheHits.ShouldBe(1);
        Should.Throw<RouteCatalogUnavailableException>(pass.Complete).Message.ShouldBe("Technical HTTP loaded image changed during observation.");
    }

    /// <summary>Count, aggregate bytes and per-image limits never permit incomplete or pooled inputs to become retained truth.</summary>
    [Fact]
    public void MemoBoundsAndDisposalUseOrdinaryFallback()
    {
        Assembly assembly = typeof(RouteCatalogAuthority).Assembly;
        using var countMemo = new RouteCatalogImageDigestMemo();
        for (int index = 0; index < 129; index++)
        {
            var key = new DiskImageAssembly(assembly, assembly.Location);
            bool accepted = countMemo.TryCompute(key, [1, 2, 3], out string digest, out bool reused);
            accepted.ShouldBe(index < 128);
            reused.ShouldBeFalse();
            if (accepted) digest.ShouldBe(Convert.ToHexStringLower(SHA256.HashData(new byte[] { 1, 2, 3 })));
        }
        countMemo.Count.ShouldBe(128);
        countMemo.RetainedBytes.ShouldBe(128 * (3 + 512));
        using var byteMemo = new RouteCatalogImageDigestMemo();
        byte[] large = new byte[16 * 1024 * 1024];
        for (int index = 0; index < 5; index++)
            byteMemo.TryCompute(new DiskImageAssembly(assembly, assembly.Location), large, out _, out _).ShouldBe(index < 3);
        byteMemo.Count.ShouldBe(3);
        byteMemo.RetainedBytes.ShouldBe(3 * (16 * 1024 * 1024 + 512));
        byteMemo.TryCompute(assembly, new byte[16 * 1024 * 1024 + 1], out _, out _).ShouldBeFalse();
        byteMemo.Dispose();
        byteMemo.IsClosed.ShouldBeTrue();
        byteMemo.Count.ShouldBe(0);
        byteMemo.RetainedBytes.ShouldBe(0);
        byteMemo.TryCompute(assembly, [1, 2, 3], out _, out _).ShouldBeFalse();
    }

    /// <summary>Closing an inherited scope prevents detached work from using or repopulating its memo.</summary>
    [Fact]
    public async Task ClosedAndForeignScopesKeepIndependentOwnership()
    {
        using var image = new ImageFixture();
        using var first = new RouteCatalogImageDigestMemo();
        using var second = new RouteCatalogImageDigestMemo();
        var outer = RouteCatalogImageDigestScope.Enter(first);
        string expected = TechnicalHttpActionObservation.AssemblyIdentity(image.Assembly);
        using (RouteCatalogImageDigestScope.Suppress())
        {
            RouteCatalogImageDigestScope.Current.ShouldBeNull();
            using var inner = RouteCatalogImageDigestScope.Enter(second);
            TechnicalHttpActionObservation.AssemblyIdentity(image.Assembly).ShouldBe(expected);
            inner.CacheHits.ShouldBe(0);
        }
        RouteCatalogImageDigestScope.Current.ShouldBeSameAs(outer);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task child = Task.Run(async () =>
        {
            await resume.Task;
            RouteCatalogImageDigestScope.Current.ShouldBeSameAs(outer);
            TechnicalHttpActionObservation.AssemblyIdentity(image.Assembly).ShouldBe(expected);
            outer.CacheHits.ShouldBe(0);
        }, TestContext.Current.CancellationToken);
        outer.Dispose();
        resume.SetResult();
        await child;
        first.Count.ShouldBe(1); second.Count.ShouldBe(1);
        RouteCatalogImageDigestScope.Current.ShouldBeNull();
    }

    /// <summary>Concurrent fresh reads share only a successful complete SHA computation; each retains its own file/PE checks.</summary>
    [Fact]
    public async Task ConcurrentScopesReturnExactImageIdentity()
    {
        using var image = new ImageFixture();
        string expected = Expected(image.Assembly);
        using var memo = new RouteCatalogImageDigestMemo();
        int hits = 0;
        await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() =>
        {
            using var scope = RouteCatalogImageDigestScope.Enter(memo);
            TechnicalHttpActionObservation.AssemblyIdentity(image.Assembly).ShouldBe(expected);
            Interlocked.Add(ref hits, scope.CacheHits);
        }, TestContext.Current.CancellationToken)));
        memo.Count.ShouldBe(1); hits.ShouldBe(15);
    }

    /// <summary>Records the entire unchanged fresh read/PE/MVID path plus actual scope/memo cost against the ordinary hash path.</summary>
    [Fact]
    public void MeasureFullFreshImageObservationWithOwnedMemo()
    {
        Assembly assembly = typeof(RouteCatalogAuthority).Assembly;
        string expected = Expected(assembly);
        using var memo = new RouteCatalogImageDigestMemo();
        for (int warmup = 0; warmup < 40; warmup++)
        {
            TechnicalHttpActionObservation.AssemblyIdentity(assembly);
            using var scope = RouteCatalogImageDigestScope.Enter(memo);
            TechnicalHttpActionObservation.AssemblyIdentity(assembly);
        }
        var measurements = new List<object>();
        for (int sample = 0; sample < 5; sample++)
        {
            (double elapsed, long allocated) Measure(bool enabled)
            {
                long allocated = GC.GetAllocatedBytesForCurrentThread(), started = Stopwatch.GetTimestamp();
                for (int iteration = 0; iteration < 80; iteration++)
                {
                    using var boundary = RouteCatalogImageDigestScope.Suppress();
                    using var scope = enabled ? RouteCatalogImageDigestScope.Enter(memo) : null;
                    TechnicalHttpActionObservation.AssemblyIdentity(assembly).ShouldBe(expected);
                }
                return (Stopwatch.GetElapsedTime(started).TotalMilliseconds / 80,
                    (GC.GetAllocatedBytesForCurrentThread() - allocated) / 80);
            }
            var ordinary = Measure(false); var owned = Measure(true);
            measurements.Add(new { sample, ordinaryMs = ordinary.elapsed, ownedMs = owned.elapsed,
                ordinaryAllocatedBytes = ordinary.allocated, ownedAllocatedBytes = owned.allocated });
        }
        output.WriteLine(JsonSerializer.Serialize(new { imageBytes = new FileInfo(assembly.Location).Length,
            fullFreshReadAndPeMvidEveryTime = true, measurements }));
    }

    private static string Expected(Assembly assembly) => $"{assembly.FullName}:{assembly.ManifestModule.ModuleVersionId}:"
        + Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(assembly.Location)));

    private sealed class RecordingPool : ArrayPool<byte>
    {
        internal int Rents { get; private set; }
        internal int Returns { get; private set; }
        internal byte[]? Buffer { get; private set; }
        public override byte[] Rent(int minimumLength)
        {
            Rents++;
            Buffer ??= new byte[minimumLength + 127];
            Array.Fill(Buffer, (byte)0xa5);
            return Buffer;
        }
        public override void Return(byte[] array, bool clearArray = false)
        {
            array.ShouldBeSameAs(Buffer);
            Returns++;
            Array.Fill(array, (byte)0xcc);
        }
    }

    private sealed class ImageFixture : IDisposable
    {
        private readonly string _directory = Directory.CreateTempSubdirectory("catalog-image-proof-").FullName;
        internal Assembly Assembly { get; }
        internal ImageFixture()
        {
            Assembly actual = typeof(RouteCatalogAuthority).Assembly;
            string path = Path.Combine(_directory, "image.dll");
            File.Copy(actual.Location, path);
            Assembly = new DiskImageAssembly(actual, path);
        }
        public void Dispose() => Directory.Delete(_directory, true);
    }

    private sealed class DiskImageAssembly(Assembly actual, string path) : Assembly
    {
        public override string Location => path;
        public override string? FullName => actual.FullName;
        public override Module ManifestModule => actual.ManifestModule;
    }
}
