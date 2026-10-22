using System.Text.Json;
using System.Net;

using Shouldly;

namespace Hexalith.EventStore.ProviderVerification.Tests;

public sealed class ProviderVerificationApplicationTests
{
    [Fact]
    public async Task RunAsync_RepositoryRootUnavailable_WritesMinimalInputFailureReport()
    {
        string reportDirectory = Path.Combine(Path.GetTempPath(), $"eventstore-root-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(reportDirectory);
        string reportPath = Path.Combine(reportDirectory, "report.json");
        try
        {
            ProviderVerificationOptions options = PlaceholderOptions(reportPath);

            int exitCode = await ProviderVerificationApplication.RunAsync(
                options,
                TestContext.Current.CancellationToken,
                startHostAsync: null,
                findRepositoryRoot: static () => throw new ProviderVerificationInputException(
                    "input.repository-root.unavailable"));

            exitCode.ShouldBe(ProviderVerificationApplication.InputFailureExitCode);
            using JsonDocument report = JsonDocument.Parse(File.ReadAllBytes(reportPath));
            JsonElement root = report.RootElement;
            root.GetProperty("reasonCodes").EnumerateArray().Single().GetString()
                .ShouldBe("input.repository-root.unavailable");
            root.GetProperty("verificationMode").GetString().ShouldBe("historical-approval");
            root.GetProperty("requestedInteractionCount").GetInt32().ShouldBe(0);
            root.GetProperty("hostStarted").GetBoolean().ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(reportDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task StartAsync_ReadinessFailsAfterBind_RecordsStoppedAndClosedFacts()
    {
        var timeline = new ProviderVerificationTimeline();
        timeline.BeginStartup();
        var coordinator = new ProviderStateCoordinator(SupportedProviderStates.All);

        await Should.ThrowAsync<InvalidOperationException>(() => ProviderVerificationHost.StartAsync(
            coordinator,
            FindRepositoryRoot(),
            TimeSpan.FromSeconds(15),
            TestContext.Current.CancellationToken,
            timeline,
            static (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))));

        timeline.HostBound.ShouldBeTrue();
        timeline.HostStopped.ShouldBeTrue();
        timeline.PortClosed.ShouldBeTrue();
        timeline.StartupResultCode.ShouldBe("startup.succeeded");
        timeline.ReadinessResultCode.ShouldBe("readiness.failed");
        timeline.CleanupResultCode.ShouldBe("cleanup.succeeded");
    }

    [Fact]
    public async Task RunAsync_FatalStartupAfterInputsLoad_ReportsEveryInteractionAsNotRun()
    {
        string eventStoreRoot = FindRepositoryRoot();
        string frontComposerRoot = Path.Combine(eventStoreRoot, "references", "Hexalith.FrontComposer");
        if (!File.Exists(Path.Combine(
            frontComposerRoot,
            "tests",
            "Hexalith.FrontComposer.Shell.Tests",
            "Pact",
            "interaction-manifest.json")))
        {
            frontComposerRoot = Path.GetFullPath(Path.Combine(eventStoreRoot, "..", ".."));
        }
        string pactDirectory = Path.Combine(
            frontComposerRoot,
            "tests",
            "Hexalith.FrontComposer.Shell.Tests",
            "Pact");
        string reportDirectory = Path.Combine(Path.GetTempPath(), $"eventstore-run-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(reportDirectory);
        string reportPath = Path.Combine(reportDirectory, "report.json");
        try
        {
            var options = new ProviderVerificationOptions(
                ProviderVerificationMode.LiveCompatibility,
                pactDirectory,
                Path.Combine(pactDirectory, "interaction-manifest.json"),
                Path.Combine(pactDirectory, "provider-state-catalog.json"),
                string.Empty,
                string.Empty,
                reportPath,
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(5));

            int exitCode = await ProviderVerificationApplication.RunAsync(
                options,
                TestContext.Current.CancellationToken,
                static (_, _, _, _, _, _) => Task.FromException<ProviderVerificationHost>(
                    new InvalidOperationException("injected-startup-failure")));

            exitCode.ShouldBe(ProviderVerificationApplication.ContractFailureExitCode);
            using JsonDocument report = JsonDocument.Parse(File.ReadAllBytes(reportPath));
            JsonElement root = report.RootElement;
            root.GetProperty("requestedInteractionCount").GetInt32().ShouldBe(19);
            root.GetProperty("reportedInteractionCount").GetInt32().ShouldBe(19);
            root.GetProperty("complete").GetBoolean().ShouldBeFalse();
            root.GetProperty("interactions").EnumerateArray().ShouldAllBe(interaction =>
                interaction.GetProperty("resultCode").GetString() == "interaction.not-run"
                && interaction.GetProperty("stateEvents")[0].GetProperty("resultCode").GetString() == "state.setup.not-run"
                && interaction.GetProperty("stateEvents")[1].GetProperty("resultCode").GetString() == "state.teardown.not-run");
            root.GetProperty("timing").GetProperty("startup").GetProperty("resultCode").GetString()
                .ShouldBe("startup.failed");
        }
        finally
        {
            Directory.Delete(reportDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_WhenNormalizedPactCleanupIsExhausted_ReportsCleanupFailure()
    {
        string eventStoreRoot = FindRepositoryRoot();
        string frontComposerRoot = Path.Combine(eventStoreRoot, "references", "Hexalith.FrontComposer");
        string pactDirectory = Path.Combine(
            frontComposerRoot,
            "tests",
            "Hexalith.FrontComposer.Shell.Tests",
            "Pact");
        string reportDirectory = Path.Combine(Path.GetTempPath(), $"eventstore-cleanup-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(reportDirectory);
        string reportPath = Path.Combine(reportDirectory, "report.json");
        var normalizedPaths = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            var options = new ProviderVerificationOptions(
                ProviderVerificationMode.LiveCompatibility,
                pactDirectory,
                Path.Combine(pactDirectory, "interaction-manifest.json"),
                Path.Combine(pactDirectory, "provider-state-catalog.json"),
                string.Empty,
                string.Empty,
                reportPath,
                TimeSpan.FromSeconds(15),
                TimeSpan.FromSeconds(15),
                TimeSpan.FromSeconds(15));

            int exitCode = await ProviderVerificationApplication.RunAsync(
                options,
                TestContext.Current.CancellationToken,
                deleteNormalizedPact: path =>
                {
                    _ = normalizedPaths.Add(path);
                    throw new IOException("injected-persistent-delete-failure");
                });

            exitCode.ShouldBe(ProviderVerificationApplication.CleanupFailureExitCode);
            normalizedPaths.Count.ShouldBe(1);
            using JsonDocument report = JsonDocument.Parse(File.ReadAllBytes(reportPath));
            JsonElement root = report.RootElement;
            root.GetProperty("finalVerdict").GetString().ShouldBe("failed");
            root.GetProperty("reasonCodes").EnumerateArray().ShouldContain(
                item => item.GetString() == "interaction.normalized-pact-cleanup-failed");
            root.GetProperty("interactions").EnumerateArray().ShouldAllBe(
                interaction => interaction.GetProperty("resultCode").GetString() != "interaction.passed");
        }
        finally
        {
            foreach (string path in normalizedPaths)
            {
                File.Delete(path);
            }

            Directory.Delete(reportDirectory, recursive: true);
        }
    }

    private static string FindRepositoryRoot()
    {
        foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            DirectoryInfo? directory = new(start);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Hexalith.EventStore.slnx")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("EventStore repository root was not found.");
    }

    private static ProviderVerificationOptions PlaceholderOptions(string reportPath)
        => new(
            ProviderVerificationMode.HistoricalAuthorization,
            "missing-pacts",
            "missing-manifest.json",
            "missing-catalog.json",
            "missing-identity.md",
            "missing-evidence",
            reportPath,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(5));
}
