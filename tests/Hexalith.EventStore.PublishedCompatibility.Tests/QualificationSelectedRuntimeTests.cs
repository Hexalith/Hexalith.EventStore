using System.Text.Json;

using Shouldly;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Rejects unqualified candidate receipts and executable bytes before copying the rejected executable.</summary>
public sealed class QualificationSelectedRuntimeTests
{
    /// <summary>Changed bytes, self-resealed hashes and mismatched versions never materialize an executable.</summary>
    /// <param name="mutation">The independently rejected candidate input.</param>
    [Theory]
    [InlineData("modified-bytes")]
    [InlineData("resealed-hash")]
    [InlineData("receipt-version")]
    public async Task RejectedCandidateExecutableIsNotMaterializedAsync(string mutation)
    {
        string root = Path.Combine(Path.GetTempPath(), "selected-runtime-rejection-" + Guid.NewGuid().ToString("N"));
        string source = Path.Combine(root, "source");
        string destination = Path.Combine(root, "materialized");
        Directory.CreateDirectory(source);
        try
        {
            byte[] bytes = "synthetic modified executable"u8.ToArray();
            await File.WriteAllBytesAsync(Path.Combine(source, "daprd"), bytes);
            string expected = mutation == "resealed-hash" ? QualificationRuntime.Hash(bytes)
                : "b1ece5da76674bf6e15126aeb019fa58e444b5b8edf4d0ac59e32df895ae8fd2";
            await File.WriteAllTextAsync(Path.Combine(source, "selected-runtime.json"), JsonSerializer.Serialize(new
            {
                version = mutation == "receipt-version" ? "1.18.7" : "1.19.1",
                binaries = new { daprd = expected },
            }));

            await Should.ThrowAsync<InvalidOperationException>(() =>
                QualificationRuntime.MaterializeSelectedDaprAsync(source, destination, "1.19.1"));

            File.Exists(Path.Combine(destination, "daprd")).ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
