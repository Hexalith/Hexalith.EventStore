using System.Diagnostics;

namespace Hexalith.EventStore.Contracts.Tests.Packaging;

/// <summary>Runs isolated public-package probes when a release inventory is supplied.</summary>
public sealed class TrustedEffectPackageContractTests
{
    private static readonly TimeSpan _processTimeout = TimeSpan.FromMinutes(30);

    /// <summary>Requires the named Contracts and Client APIs to compile without source references.</summary>
    [Fact]
    public async Task NamedPackagesExposeTrustedEffectContracts()
    {
        string? packageDirectory = Environment.GetEnvironmentVariable("EVENTSTORE_PACKAGE_CONTRACT_DIR");
        if (string.IsNullOrWhiteSpace(packageDirectory))
        {
            Assert.Skip("EVENTSTORE_PACKAGE_CONTRACT_DIR is not set; no release package inventory was supplied.");
        }

        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "tools", "release-packages.json")))
        {
            directory = directory.Parent;
        }

        string root = directory?.FullName
            ?? throw new InvalidOperationException("The EventStore repository root was not found.");
        var start = new ProcessStartInfo("python3")
        {
            WorkingDirectory = root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("scripts/validate-consumer-package-references.py");
        start.ArgumentList.Add(packageDirectory);
        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("The package-only trusted effect process did not start.");
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
            Assert.Fail($"The package-only consumer validation exceeded {_processTimeout}.");
        }

        string output = await standardOutput;
        string error = await standardError;
        Assert.True(
            process.ExitCode == 0,
            $"Package-only consumer validation exited {process.ExitCode}.{Environment.NewLine}"
                + $"stdout:{Environment.NewLine}{output}{Environment.NewLine}stderr:{Environment.NewLine}{error}");
    }
}
