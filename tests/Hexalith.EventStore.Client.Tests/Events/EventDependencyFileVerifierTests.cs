using Hexalith.EventStore.Client.Events;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Events;

/// <summary>Qualifies exact local artifact bytes with an independent SHA-256 known answer.</summary>
public sealed class EventDependencyFileVerifierTests
{
    // Independent SHA-256 known answer for the exact three ASCII bytes "abc".
    private static readonly byte[] AbcHash = Convert.FromHexString("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");

    /// <summary>Checks the same byte admission and changed/missing refusals for both dependency kinds.</summary>
    /// <param name="kind">The exact declared managed/native dependency kind.</param>
    [Theory]
    [InlineData("managed")]
    [InlineData("native")]
    public void FileHashAdmitsExactBytesAndRefusesChangedOrMissingArtifacts(string kind)
    {
        string directory = Path.Combine(Path.GetTempPath(), "event-artifact-control-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, "artifact");
        try
        {
            using var row = new EventRegistryRow(DependencyRow(kind));
            File.WriteAllBytes(file, "abc"u8.ToArray());
            EventDependencyFileVerifier.RequireExactFile(row, file, CancellationToken.None);

            File.WriteAllBytes(file, "abd"u8.ToArray());
            InvalidOperationException changed = Should.Throw<InvalidOperationException>(() =>
                EventDependencyFileVerifier.RequireExactFile(row, file, CancellationToken.None));
            changed.Message.ShouldStartWith("CapabilityMismatch:");

            File.Delete(file);
            Should.Throw<FileNotFoundException>(() => EventDependencyFileVerifier.RequireExactFile(row, file, CancellationToken.None));
            Should.Throw<DirectoryNotFoundException>(() => EventDependencyFileVerifier.RequireExactFile(
                row, Path.Combine(directory, "missing", "artifact"), CancellationToken.None));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    /// <summary>Checks that pre-cancellation preserves its token and precedes filesystem access.</summary>
    [Fact]
    public void PreCancellationRefusesBeforeOpeningAMissingArtifactWithTheOriginalToken()
    {
        using var row = new EventRegistryRow(DependencyRow("managed"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        OperationCanceledException failure = Should.Throw<OperationCanceledException>(() =>
            EventDependencyFileVerifier.RequireExactFile(row,
                Path.Combine(Path.GetTempPath(), "missing-artifact-" + Guid.NewGuid().ToString("N")), cancellation.Token));
        failure.CancellationToken.ShouldBe(cancellation.Token);
    }

    private static byte[] DependencyRow(string kind)
    {
        using var writer = new EventEvolutionBinaryWriter(4096);
        writer.WriteByte(0x47);
        writer.WriteString("d");
        writer.WriteString("artifact");
        writer.WriteString(kind);
        writer.WriteUInt16(3);
        writer.WriteByte(1); writer.WriteString("declared-version-or-abi");
        writer.WriteByte(2); writer.WriteHash(AbcHash);
        writer.WriteByte(3); writer.WriteString("declared-context");
        return writer.CopyEncodedBytes();
    }
}
