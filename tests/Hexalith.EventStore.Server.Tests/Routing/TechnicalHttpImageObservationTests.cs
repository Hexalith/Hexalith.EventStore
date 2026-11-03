using System.Buffers;
using System.Reflection;
using System.Security.Cryptography;

using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Fresh image ownership, exact byte slices and refusal cleanup survive private buffer reuse.</summary>
public sealed class TechnicalHttpImageObservationTests
{
    [Fact]
    public void EveryReadHashesOnlyFreshImageBytesAndReturnsTheBufferAfterUse()
    {
        using var image = new ImageFixture();
        var pool = new RecordingPool();
        string expected = Expected(image.Assembly);
        TechnicalHttpActionObservation.AssemblyIdentity(image.Assembly, pool).ShouldBe(expected);
        TechnicalHttpActionObservation.AssemblyIdentity(image.Assembly, pool).ShouldBe(expected);
        pool.Rents.ShouldBe(2); pool.Returns.ShouldBe(2);
        pool.Buffer!.ShouldAllBe(value => value == 0xcc);
    }

    [Theory]
    [InlineData("different-module")]
    [InlineData("invalid-pe")]
    public void FailedImageValidationReturnsItsOwnedBufferAndClosesTheFile(string failure)
    {
        using var image = new ImageFixture();
        byte[] replacement = failure == "different-module"
            ? File.ReadAllBytes(typeof(Hexalith.EventStore.Contracts.Routing.RouteCatalogActivation).Assembly.Location) : [1, 2, 3];
        File.WriteAllBytes(image.Assembly.Location, replacement);
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

    [Fact]
    public void MissingImagePreservesReadFailureWithoutBorrowingABuffer()
    {
        using var image = new ImageFixture();
        File.Delete(image.Assembly.Location);
        var pool = new RecordingPool();
        Should.Throw<FileNotFoundException>(() => TechnicalHttpActionObservation.AssemblyIdentity(image.Assembly, pool));
        pool.Rents.ShouldBe(0); pool.Returns.ShouldBe(0);
    }

    [Fact]
    public void LargerImagesKeepTheExistingUnpooledReadAndExactHash()
    {
        using var image = new ImageFixture();
        using (var file = new FileStream(image.Assembly.Location, FileMode.Open, FileAccess.Write))
            file.SetLength(16 * 1024 * 1024 + 1);
        var pool = new RecordingPool();
        TechnicalHttpActionObservation.AssemblyIdentity(image.Assembly, pool).ShouldBe(Expected(image.Assembly));
        pool.Rents.ShouldBe(0); pool.Returns.ShouldBe(0);
    }

    [Fact]
    public void CompleteStillRejectsChangedImageBytesWithTheSameLoadedModule()
    {
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
