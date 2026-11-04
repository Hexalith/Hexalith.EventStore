namespace Hexalith.EventStore.Platform.IntegrationTests;

using System.Text.Json;

using Shouldly;

/// <summary>Uses the actual published mapping and explicitly local metadata controls, never a simulated CI owner.</summary>
public sealed class NativeDiagnosticBindingTests
{
    /// <summary>Checks the surviving caller route and immutable cross-document refusals with GitHub variables absent.</summary>
    /// <param name="mutation">The local metadata boundary to exercise.</param>
    [Theory]
    [InlineData("valid-local")]
    [InlineData("foreign-run")]
    [InlineData("foreign-attempt")]
    [InlineData("changed-mapping")]
    [InlineData("changed-context")]
    public void PublishedMappingRoutesScrubbedLocalControlsAndRefusesMismatches(string mutation)
    {
        Environment.GetEnvironmentVariable("GITHUB_ACTIONS").ShouldBeNull();
        Environment.GetEnvironmentVariable("GITHUB_WORKSPACE").ShouldBeNull();
        string prepared = Path.Combine(Path.GetDirectoryName(Environment.GetEnvironmentVariable("CustomAfterMicrosoftCommonProps"))!, "mapping.json");
        using JsonDocument mapping = JsonDocument.Parse(File.ReadAllText(prepared));
        string caller = mapping.RootElement.GetProperty("Root").GetString()!;
        string hash = mapping.RootElement.GetProperty("ContentHash").GetString()!;
        string control = Directory.CreateTempSubdirectory("eventstore-local-handoff-control-").FullName;
        string workspace = Directory.CreateDirectory(Path.Combine(control, "workspace")).FullName;
        string state = Directory.CreateDirectory(Path.Combine(control, "state")).FullName;
        string run = Guid.NewGuid().ToString("N"), attempt = Guid.NewGuid().ToString("N"), environment = Guid.NewGuid().ToString("N");
        string? retained = null;
        try
        {
            Directory.CreateDirectory(Path.Combine(workspace, "source-mapping"));
            Directory.CreateDirectory(Path.Combine(workspace, "native-tests"));
            File.Copy(prepared, Path.Combine(workspace, "source-mapping", "mapping.json"));
            File.WriteAllText(Path.Combine(workspace, "test-environment.json"), JsonSerializer.Serialize(new
            {
                bindings = new
                {
                    run = new { runId = run, ownerRunId = run, attemptId = attempt, environmentId = environment, role = "owner" },
                    environment = new { executionContext = new { kind = "local" } },
                },
            }));
            File.WriteAllText(Path.Combine(workspace, "plan.json"), JsonSerializer.Serialize(new
            {
                schema = "hexalith.g4-run-plan.v1", runId = run, workspace, resourceClaimStateDirectory = state,
                attemptId = mutation == "foreign-attempt" ? Guid.NewGuid().ToString("N") : attempt, environmentId = environment,
                sourceMappingHash = hash, sourceMapping = new { root = caller, contentHash = hash },
                selectedTest = new { name = "platform-package" },
            }));
            File.WriteAllText(Path.Combine(state, run + ".json"), JsonSerializer.Serialize(new
            {
                schema = "hexalith.g4-run-state.v1", runId = run, workspace, attemptId = attempt, environmentId = environment,
                executionContext = new { kind = mutation == "changed-context" ? "unsupported-local-control" : "local" },
            }));
            Dictionary<string, string> handoff = new(StringComparer.Ordinal)
            {
                ["HEXALITH_G4_RUN_ID"] = mutation == "foreign-run" ? Guid.NewGuid().ToString("N") : run,
                ["HEXALITH_TEST_ENVIRONMENT_PATH"] = Path.Combine(workspace, "test-environment.json"),
                ["HEXALITH_G4_PLAN_PATH"] = Path.Combine(workspace, "plan.json"),
                ["HEXALITH_G4_TEST_REPORT_PATH"] = Path.Combine(workspace, "native-tests", "native.trx"),
                ["HEXALITH_G4_SOURCE_MAPPING_PATH"] = Path.Combine(workspace, "source-mapping", "mapping.json"),
                ["HEXALITH_SOURCE_MAPPING_HASH"] = mutation == "changed-mapping" ? new string('0', 64) : hash,
            };
            if (mutation != "valid-local")
            {
                Should.Throw<InvalidDataException>(() => NativeDiagnosticBinding.Read(run, handoff));
                return;
            }
            NativeDiagnosticBinding binding = NativeDiagnosticBinding.Read(run, handoff);
            binding.CallerRoot.ShouldBe(caller);
            binding.OwnerWorkspace.ShouldBe(workspace);
            binding.ExecutionContext.GetProperty("kind").GetString().ShouldBe("local");
            binding.ArtifactDirectory.ShouldBe(Path.Combine(caller, "TestResults", "module-integration"));
            Directory.CreateDirectory(binding.ArtifactDirectory);
            NativeAssemblyObservations.WritePrivateObservation(binding.ArtifactDirectory, "local-control-" + run, "{\"scope\":\"local-handoff-io-only\"}");
            retained = Path.Combine(binding.ArtifactDirectory, "loaded-assemblies-local-control-" + run + ".json");
            byte[] original = File.ReadAllBytes(retained);
            Directory.Delete(workspace, recursive: true);
            File.ReadAllBytes(retained).ShouldBe(original);
        }
        finally
        {
            if (retained is not null) File.Delete(retained);
            Directory.Delete(control, recursive: true);
        }
    }
}
