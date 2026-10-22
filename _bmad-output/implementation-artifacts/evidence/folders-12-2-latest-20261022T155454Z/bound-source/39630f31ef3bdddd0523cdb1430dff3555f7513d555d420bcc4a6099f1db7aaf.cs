using System.Reflection.Metadata;
using System.Text.Json;
using Hexalith.EventStore.Client.Projections;
using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>Checks source and loaded-image binding before an expensive Dapr/process qualification.</summary>
public sealed class ProjectionBoundedIdentityRuntimeProofTests
{
    [Fact]
    public void LoadedImagesMatchExactPortablePdbDocuments()
    {
        var proof = ProjectionBoundedIdentityRuntimeProof.Capture();
        proof.Count.ShouldBe(4);
        foreach (var assembly in proof)
        {
            assembly!["loadedMvidMatchesPe"]!.GetValue<bool>().ShouldBeTrue();
            assembly["documents"]!.AsArray().Count.ShouldBeGreaterThan(0);
        }
        if (Environment.GetEnvironmentVariable("BOUNDED_IDENTITY_PREFLIGHT_EVIDENCE") is { } path)
            File.WriteAllText(path, proof.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    [Theory]
    [InlineData("obj", true)]
    [InlineData("ordinary", false)]
    public void EmbeddedGeneratedSourceAndCurrentOrdinarySourceKeepTheirAuthority(string segment, bool generated)
    {
        string pdb = Path.ChangeExtension(typeof(SharedProjectionEpochCoordinator).Assembly.Location, ".pdb");
        using var stream = File.OpenRead(pdb);
        using var provider = MetadataReaderProvider.FromPortablePdbStream(stream);
        var metadata = provider.GetMetadataReader();
        Guid embeddedSourceKind = new("0e8a571b-6926-466e-b4ad-8ab04611f5fe");
        DocumentHandle handle = metadata.Documents.First(candidate =>
            metadata.GetString(metadata.GetDocument(candidate).Name).Replace('\\', '/').Split('/').Contains("obj")
            && metadata.GetCustomDebugInformation(candidate).Any(debugHandle =>
                metadata.GetGuid(metadata.GetCustomDebugInformation(debugHandle).Kind) == embeddedSourceKind));
        string directory = Directory.CreateTempSubdirectory("bounded-runtime-source-").FullName;
        try
        {
            byte[] embedded = ProjectionBoundedIdentityRuntimeProof.ReadValidatedDocumentSource(metadata, handle,
                Path.Combine(directory, "obj", "absent.cs"));
            byte[] changed = "// isolated disk bytes differ from the compiled generated source\n"u8.ToArray();
            changed.AsSpan().SequenceEqual(embedded).ShouldBeFalse();
            string path = Path.Combine(directory, segment, "file.cs");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, changed);
            byte[] selected = ProjectionBoundedIdentityRuntimeProof.ReadDocumentSource(metadata, handle, path);
            selected.ShouldBe(generated ? embedded : changed);
            if (generated)
                ProjectionBoundedIdentityRuntimeProof.ReadValidatedDocumentSource(metadata, handle, path).ShouldBe(embedded);
            else
                Should.Throw<InvalidOperationException>(() =>
                    ProjectionBoundedIdentityRuntimeProof.ReadValidatedDocumentSource(metadata, handle, path))
                    .Message.ShouldBe("Runtime PDB source differs: " + path);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
