using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;

namespace Hexalith.EventStore.Contracts.Tests.Packaging;

public sealed class EffectivePackageVersionEvaluationTests
{
    private const string MsBuildThisFileDirectory = "$(MSBuildThisFileDirectory)";
    private static readonly TimeSpan _msBuildEvaluationTimeout = TimeSpan.FromSeconds(30);

    private static readonly string[] _requiredPackageIds =
    [
        "NBomber.Http",
        "xunit.v3.extensibility.core",
        "System.CommandLine",
        "ModelContextProtocol",
        "Microsoft.Extensions.TimeProvider.Testing",
        "NBomber",
        "Microsoft.Playwright",
    ];

    [Fact]
    public void RequiredPackagesEvaluateExactlyOnceFromBuilds()
    {
        string root = FindRepositoryRoot();
        string sharedCatalogPath = ResolveSharedCatalogPath(root);
        XDocument sharedCatalog = XDocument.Load(sharedCatalogPath);

        using JsonDocument evaluation = EvaluatePackageVersions(root);
        JsonElement packageVersions = evaluation.RootElement
            .GetProperty("Items")
            .GetProperty("PackageVersion");

        foreach (string packageId in _requiredPackageIds)
        {
            JsonElement[] effectiveEntries = packageVersions
                .EnumerateArray()
                .Where(item => string.Equals(
                    item.GetProperty("Identity").GetString(),
                    packageId,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();

            effectiveEntries.Length.ShouldBe(1, $"'{packageId}' must have one effective central version.");

            XElement sourceEntry = sharedCatalog
                .Descendants("PackageVersion")
                .Single(element => string.Equals(
                    element.Attribute("Include")?.Value,
                    packageId,
                    StringComparison.OrdinalIgnoreCase));
            string expectedVersion = sourceEntry.Attribute("Version").ShouldNotBeNull().Value;

            effectiveEntries[0].GetProperty("Version").GetString().ShouldBe(expectedVersion);
            Path.GetFullPath(effectiveEntries[0].GetProperty("DefiningProjectFullPath").GetString().ShouldNotBeNull())
                .ShouldBe(sharedCatalogPath);
        }
    }

    private static JsonDocument EvaluatePackageVersions(string root)
    {
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            },
        };
        process.StartInfo.ArgumentList.Add("msbuild");
        process.StartInfo.ArgumentList.Add("Directory.Packages.props");
        process.StartInfo.ArgumentList.Add("-getItem:PackageVersion");

        process.Start().ShouldBeTrue("Could not start dotnet msbuild for package-version evaluation.");
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit((int)_msBuildEvaluationTimeout.TotalMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException(
                $"Package-version evaluation timed out after {_msBuildEvaluationTimeout}.");
        }

        string standardOutput = outputTask.GetAwaiter().GetResult();
        string standardError = errorTask.GetAwaiter().GetResult();

        process.ExitCode.ShouldBe(0, standardError);
        return JsonDocument.Parse(standardOutput);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Directory.Packages.props"))
                && Directory.Exists(Path.Combine(directory.FullName, "src", "Hexalith.EventStore.Contracts")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from the test working directory.");
    }

    private static string ResolveSharedCatalogPath(string root)
    {
        XDocument wrapper = XDocument.Load(Path.Combine(root, "Directory.Packages.props"));
        string[] importProperties =
        [
            "Hexalith1BuildPackageProps",
            "Hexalith2BuildPackageProps",
            "Hexalith3BuildPackageProps",
            "Hexalith4BuildPackageProps",
        ];

        foreach (string importProperty in importProperties)
        {
            string importExpression = wrapper
                .Descendants()
                .Single(element => element.Name.LocalName == importProperty)
                .Value;
            string candidate = Path.GetFullPath(importExpression.Replace(
                MsBuildThisFileDirectory,
                root + Path.DirectorySeparatorChar,
                StringComparison.Ordinal));

            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException(
            "No supported Hexalith.Builds catalog fallback exists for effective package evaluation.");
    }
}
