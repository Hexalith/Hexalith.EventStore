using System.Security.Cryptography;

using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Preserves fresh full-image observation while avoiding large temporary image copies.</summary>
public sealed class DevelopmentExecutableImageTests
{
    /// <summary>The Assembly capture path preserves arbitrary complete bytes, same-metadata mutation and every original file failure.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AssemblyOverloadKeepsCurrentBytesReplacementEmptyAndFailureSemantics(bool enabled)
    {
        string directory = Directory.CreateTempSubdirectory("catalog-scoped-image-").FullName;
        string path = Path.Combine(directory, "owner.dll");
        try
        {
            byte[] bytes = new byte[4097]; bytes[0] = 13; bytes[^1] = 97;
            File.WriteAllBytes(path, bytes);
            DateTime timestamp = File.GetLastWriteTimeUtc(path);
            var assembly = new ExecutableImageAssembly(path);
            using var memo = new RouteCatalogImageDigestMemo();
            using var scope = enabled ? RouteCatalogImageDigestScope.Enter(memo) : null;
            string original = DevelopmentDomainRouteCatalog.ReadExecutableDigest(assembly);
            original.ShouldBe(Convert.ToHexStringLower(SHA256.HashData(bytes)));
            DevelopmentDomainRouteCatalog.ReadExecutableDigest(assembly).ShouldBe(original);
            if (enabled) scope!.CacheHits.ShouldBe(1);
            bytes[0] ^= 0x31; bytes[^1] ^= 0x57;
            File.WriteAllBytes(path, bytes); File.SetLastWriteTimeUtc(path, timestamp);
            string changed = DevelopmentDomainRouteCatalog.ReadExecutableDigest(assembly);
            changed.ShouldNotBe(original);
            changed.ShouldBe(Convert.ToHexStringLower(SHA256.HashData(bytes)));
            if (enabled) scope!.CacheHits.ShouldBe(1);
            File.Move(path, path + ".old");
            File.WriteAllBytes(path, [19, 27, 39]);
            DevelopmentDomainRouteCatalog.ReadExecutableDigest(assembly).ShouldBe(DevelopmentDomainRouteCatalog.ReadExecutableDigest(path));
            File.WriteAllBytes(path, []);
            DevelopmentDomainRouteCatalog.ReadExecutableDigest(assembly).ShouldBe(Convert.ToHexStringLower(SHA256.HashData(Array.Empty<byte>())));
            File.Delete(path);
            Should.Throw<FileNotFoundException>(() => DevelopmentDomainRouteCatalog.ReadExecutableDigest(assembly));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    /// <summary>Both ordinary and warm private Assembly reads avoid allocating another image-sized temporary copy.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OrdinaryAndWarmScopedAssemblyReadsKeepBoundedTemporaryAllocation(bool enabled)
    {
        string directory = Directory.CreateTempSubdirectory("catalog-scoped-image-allocation-").FullName;
        string path = Path.Combine(directory, "owner.dll");
        try
        {
            byte[] image = new byte[4 * 1024 * 1024 + 17]; image[^1] = 31;
            File.WriteAllBytes(path, image);
            var assembly = new ExecutableImageAssembly(path);
            string expected = Convert.ToHexStringLower(SHA256.HashData(image));
            using var memo = new RouteCatalogImageDigestMemo();
            using var scope = enabled ? RouteCatalogImageDigestScope.Enter(memo) : null;
            DevelopmentDomainRouteCatalog.ReadExecutableDigest(assembly).ShouldBe(expected);
            long before = GC.GetAllocatedBytesForCurrentThread();
            DevelopmentDomainRouteCatalog.ReadExecutableDigest(assembly).ShouldBe(expected);
            (GC.GetAllocatedBytesForCurrentThread() - before).ShouldBeLessThan(image.Length / 2);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4097)]
    [InlineData(4194321)]
    public void EveryReadHashesTheCurrentCompleteBytesEvenWhenLengthAndTimestampStayEqual(int length)
    {
        string directory = Directory.CreateTempSubdirectory("catalog-image-observation-").FullName;
        string path = Path.Combine(directory, "owner.dll");
        try
        {
            byte[] bytes = new byte[length];
            if (length > 0) { bytes[0] = 1; bytes[length / 2] = 2; bytes[^1] = 3; }
            File.WriteAllBytes(path, bytes);
            DateTime timestamp = File.GetLastWriteTimeUtc(path);
            string original = DevelopmentDomainRouteCatalog.ReadExecutableDigest(path);
            original.ShouldBe(Convert.ToHexStringLower(SHA256.HashData(bytes)));
            if (length > 0)
            {
                bytes[0] ^= 16; bytes[length / 2] ^= 32; bytes[^1] ^= 64;
                File.WriteAllBytes(path, bytes); File.SetLastWriteTimeUtc(path, timestamp);
                string changed = DevelopmentDomainRouteCatalog.ReadExecutableDigest(path);
                changed.ShouldNotBe(original);
                changed.ShouldBe(Convert.ToHexStringLower(SHA256.HashData(bytes)));
            }
            File.Move(path, path + ".old");
            File.WriteAllBytes(path, [19, 27, 39]);
            DevelopmentDomainRouteCatalog.ReadExecutableDigest(path).ShouldBe(Convert.ToHexStringLower(SHA256.HashData(new byte[] { 19, 27, 39 })));
            File.Delete(path);
            Should.Throw<FileNotFoundException>(() => DevelopmentDomainRouteCatalog.ReadExecutableDigest(path));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void ObservingALargeImageDoesNotAllocateAnImageSizedTemporaryCopy()
    {
        string directory = Directory.CreateTempSubdirectory("catalog-image-allocation-").FullName;
        string path = Path.Combine(directory, "owner.dll");
        try
        {
            byte[] image = new byte[4 * 1024 * 1024 + 17]; image[^1] = 31;
            File.WriteAllBytes(path, image);
            string expected = Convert.ToHexStringLower(SHA256.HashData(image));
            DevelopmentDomainRouteCatalog.ReadExecutableDigest(path).ShouldBe(expected);
            long before = GC.GetAllocatedBytesForCurrentThread();
            string actual = DevelopmentDomainRouteCatalog.ReadExecutableDigest(path);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            actual.ShouldBe(expected);
            allocated.ShouldBeLessThan(image.Length / 2);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
