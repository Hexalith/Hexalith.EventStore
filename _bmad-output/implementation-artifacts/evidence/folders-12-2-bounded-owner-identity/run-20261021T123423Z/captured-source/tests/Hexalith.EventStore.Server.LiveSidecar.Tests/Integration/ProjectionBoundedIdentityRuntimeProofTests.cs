using System.Text.Json;
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
}
