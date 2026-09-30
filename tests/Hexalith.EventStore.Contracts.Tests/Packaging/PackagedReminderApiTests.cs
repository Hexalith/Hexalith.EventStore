using System.Diagnostics;

namespace Hexalith.EventStore.Contracts.Tests.Packaging;

/// <summary>
/// Story 4.11 package-only proof: isolated consumers of the Contracts, Client, and DomainService packages
/// compile and run the typed-reminder API with synthetic types only, never Works types or project references.
/// </summary>
public sealed class PackagedReminderApiTests
{
    private static readonly TimeSpan _processTimeout = TimeSpan.FromMinutes(30);

    private static readonly string[] _reminderPackages =
    [
        "Hexalith.EventStore.Contracts",
        "Hexalith.EventStore.Client",
        "Hexalith.EventStore.DomainService",
    ];

    /// <summary>Runs the reminder probes against the supplied release inventory.</summary>
    [Fact]
    public async Task PackagedReminderApiRunsWithoutWorksTypes()
    {
        string root = FindRepositoryRoot();
        string script = await File.ReadAllTextAsync(
            Path.Combine(root, "scripts", "validate-consumer-package-references.py"),
            TestContext.Current.CancellationToken);

        // The probes are the consumer contract: they must exercise the published R6 seams with synthetic types.
        script.ShouldContain("ReminderIdentityCodec.ComputeActorId");
        script.ShouldContain("IReminderIntentSource");
        script.ShouldContain("IReminderDelegationTokenProvider");
        script.ShouldContain("AddEventStoreReminders<SyntheticReminderSource>");
        script.ShouldNotContain("Hexalith.Works", Case.Insensitive);

        string? packageDirectory = Environment.GetEnvironmentVariable("EVENTSTORE_PACKAGE_CONTRACT_DIR");
        if (string.IsNullOrWhiteSpace(packageDirectory))
        {
            Assert.Skip("EVENTSTORE_PACKAGE_CONTRACT_DIR is not set; no release package inventory was supplied.");
        }

        var start = new ProcessStartInfo("python3")
        {
            WorkingDirectory = root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("scripts/validate-consumer-package-references.py");
        start.ArgumentList.Add(packageDirectory);
        foreach (string package in _reminderPackages)
        {
            start.ArgumentList.Add("--package");
            start.ArgumentList.Add(package);
        }

        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("The package-only reminder process did not start.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(_processTimeout);
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync(timeout.Token);
        Task<string> standardError = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            Assert.Fail($"The package-only reminder validation exceeded {_processTimeout}.");
        }

        string output = await standardOutput;
        string error = await standardError;
        Assert.True(
            process.ExitCode == 0,
            $"Package-only reminder validation exited {process.ExitCode}.{Environment.NewLine}"
                + $"stdout:{Environment.NewLine}{output}{Environment.NewLine}stderr:{Environment.NewLine}{error}");
        output.ShouldContain("Validated 3 isolated package-only consumers");
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "tools", "release-packages.json")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The EventStore repository root was not found.");
    }
}
