using System.Diagnostics;

namespace Hexalith.EventStore.Contracts.Tests.Packaging;

/// <summary>Runs the executable R3–R4 API probes against an isolated package inventory when supplied.</summary>
public sealed class ProjectionPackageContractTests
{
    [Fact]
    public async Task PackagedProjectionApisCompileAndRunWithoutProjectReferences()
    {
        string? packageDirectory = Environment.GetEnvironmentVariable("EVENTSTORE_PACKAGE_CONTRACT_DIR");
        if (string.IsNullOrWhiteSpace(packageDirectory))
        {
            return;
        }

        string root = FindRepositoryRoot();
        var start = new ProcessStartInfo("python3")
        {
            WorkingDirectory = root,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("scripts/validate-consumer-package-references.py");
        start.ArgumentList.Add(packageDirectory);
        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("The package-only projection contract process did not start.");
        await process.WaitForExitAsync();
        Assert.Equal(0, process.ExitCode);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "tools", "release-packages.json")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("The EventStore repository root was not found.");
    }
}
