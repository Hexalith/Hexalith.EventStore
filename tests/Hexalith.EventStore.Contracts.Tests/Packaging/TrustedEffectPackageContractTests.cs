using System.Diagnostics;

namespace Hexalith.EventStore.Contracts.Tests.Packaging;

/// <summary>Runs isolated public-package probes when a release inventory is supplied.</summary>
public sealed class TrustedEffectPackageContractTests
{
    /// <summary>Requires the named Contracts and Client APIs to compile without source references.</summary>
    [Fact]
    public async Task NamedPackagesExposeTrustedEffectContracts()
    {
        string? packageDirectory = Environment.GetEnvironmentVariable("EVENTSTORE_PACKAGE_CONTRACT_DIR");
        if (string.IsNullOrWhiteSpace(packageDirectory))
        {
            return;
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
        };
        start.ArgumentList.Add("scripts/validate-consumer-package-references.py");
        start.ArgumentList.Add(packageDirectory);
        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("The package-only trusted effect process did not start.");
        await process.WaitForExitAsync();
        Assert.Equal(0, process.ExitCode);
    }
}
