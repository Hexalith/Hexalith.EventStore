using System.Security.Cryptography;

using Hexalith.EventStore.DomainService;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Preserves fresh full-image observation while avoiding large temporary image copies.</summary>
public sealed class DevelopmentExecutableImageTests
{
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
