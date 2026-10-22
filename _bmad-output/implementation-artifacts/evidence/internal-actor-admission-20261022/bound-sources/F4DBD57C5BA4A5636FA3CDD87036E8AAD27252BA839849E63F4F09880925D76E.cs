using System.Text.Json;

using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Failed preflight disposal retains failure evidence without reading an absent shadow runtime.</summary>
public sealed class Oq8FailedInitializationCleanupTests
{
    /// <summary>An invalid preflight stays failed and disposal emits only a separate initialization-failure receipt.</summary>
    [Fact]
    public async Task FailedPreflightDisposesWithoutSuccessfulRuntimeEvidence()
    {
        string directory = Path.Combine(Path.GetTempPath(), "oq8-failed-initialization-" + Guid.NewGuid().ToString("N"));
        string? previousConfiguration = Environment.GetEnvironmentVariable("HEXALITH_OQ8_CONFIGURATION");
        string? previousEvidence = Environment.GetEnvironmentVariable("HEXALITH_OQ8_EVIDENCE_DIRECTORY");
        Environment.SetEnvironmentVariable("HEXALITH_OQ8_CONFIGURATION", "invalid");
        Environment.SetEnvironmentVariable("HEXALITH_OQ8_EVIDENCE_DIRECTORY", directory);
        try
        {
            Oq8PostgresqlFixture fixture = new();
            await Should.ThrowAsync<InvalidOperationException>(async () => await fixture.InitializeAsync().ConfigureAwait(true));

            await fixture.DisposeAsync().ConfigureAwait(true);

            File.Exists(Path.Combine(directory, "observations.json")).ShouldBeFalse();
            using JsonDocument receipt = JsonDocument.Parse(await File.ReadAllTextAsync(
                Path.Combine(directory, "initialization-failure.json")).ConfigureAwait(true));
            receipt.RootElement.GetProperty("captureKind").GetString().ShouldBe("initialization-failed");
            receipt.RootElement.GetProperty("initializationCompleted").GetBoolean().ShouldBeFalse();
            receipt.RootElement.GetProperty("runtimeProofAvailable").GetBoolean().ShouldBeFalse();
        }
        finally
        {
            Environment.SetEnvironmentVariable("HEXALITH_OQ8_CONFIGURATION", previousConfiguration);
            Environment.SetEnvironmentVariable("HEXALITH_OQ8_EVIDENCE_DIRECTORY", previousEvidence);
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
