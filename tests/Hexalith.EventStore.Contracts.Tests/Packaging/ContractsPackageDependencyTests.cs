using System.Diagnostics;
using System.Xml.Linq;

namespace Hexalith.EventStore.Contracts.Tests.Packaging;

public sealed class ContractsPackageDependencyTests
{
    private const string MsBuildThisFileDirectory = "$(MSBuildThisFileDirectory)";
    private static readonly TimeSpan _consumerAuthorityValidationTimeout = TimeSpan.FromMinutes(3);

    [Fact]
    public void Contracts_package_pins_commons_unique_ids_centrally()
    {
        string root = FindRepositoryRoot();
        XDocument packageVersions = XDocument.Load(Path.Combine(root, "Directory.Packages.props"));
        XDocument sharedPackageVersions = LoadSharedPackageVersions(root, packageVersions);

        // The root props must not redeclare the version: it is centrally managed by the
        // shared Hexalith.Builds package versions.
        packageVersions
            .Descendants("PackageVersion")
            .Where(element => string.Equals(
                element.Attribute("Include")?.Value,
                "Hexalith.Commons.UniqueIds",
                StringComparison.Ordinal))
            .ShouldBeEmpty();

        // The shared props must pin the package to a single concrete version. The specific
        // version value is intentionally not asserted so that Hexalith.Builds submodule
        // bumps do not break this test.
        string packageVersionReference = sharedPackageVersions
            .Descendants("PackageVersion")
            .Single(element => string.Equals(
                element.Attribute("Include")?.Value,
                "Hexalith.Commons.UniqueIds",
                StringComparison.Ordinal))
            .Attribute("Version")
            .ShouldNotBeNull()
            .Value;

        packageVersionReference.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void RootPackagePropsIsAnImportOnlyWrapper()
    {
        string root = FindRepositoryRoot();
        XDocument packageVersions = XDocument.Load(Path.Combine(root, "Directory.Packages.props"));

        packageVersions
            .Descendants()
            .Where(element => element.Name.LocalName == "PackageVersion")
            .ShouldBeEmpty(
            "The root Directory.Packages.props must remain an import-only wrapper around the Hexalith.Builds catalog.");

        string[] fallbackVersionProperties = packageVersions
            .Descendants()
            .Where(element => element.Name.LocalName.EndsWith("Version", StringComparison.OrdinalIgnoreCase))
            .Select(element => element.Name.LocalName)
            .ToArray();

        fallbackVersionProperties.ShouldBeEmpty(
            "The root wrapper must not hide fallback dependency-version properties outside PackageVersion items.");
    }

    [Theory]
    [InlineData("NBomber.Http")]
    [InlineData("xunit.v3.extensibility.core")]
    [InlineData("System.CommandLine")]
    [InlineData("ModelContextProtocol")]
    [InlineData("Microsoft.Extensions.TimeProvider.Testing")]
    [InlineData("NBomber")]
    [InlineData("Microsoft.Playwright")]
    public void SharedCatalogOwnsRequiredPackageExactlyOnce(string packageId)
    {
        string root = FindRepositoryRoot();
        XDocument wrapper = XDocument.Load(Path.Combine(root, "Directory.Packages.props"));
        XDocument sharedPackageVersions = LoadSharedPackageVersions(root, wrapper);

        XElement packageVersion = sharedPackageVersions
            .Descendants("PackageVersion")
            .Single(element => string.Equals(
                element.Attribute("Include")?.Value,
                packageId,
                StringComparison.OrdinalIgnoreCase));

        GetMsBuildMetadataValue(packageVersion, "Version").ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Project_files_do_not_version_override_central_package_versions()
    {
        string root = FindRepositoryRoot();

        // CPM gives PackageReference VersionOverride precedence over every central pin, so a
        // single project-level attribute silently bypasses the Builds-owns-versions invariant
        // without touching Directory.Packages.props.
        string[] projectDirectories = ["src", "tests", "perf", "samples", "tools"];
        List<string> localVersions = [];

        foreach (string projectDirectory in projectDirectories)
        {
            string path = Path.Combine(root, projectDirectory);
            if (!Directory.Exists(path))
            {
                continue;
            }

            foreach (string projectFile in Directory.EnumerateFiles(path, "*.csproj", SearchOption.AllDirectories))
            {
                XDocument project = XDocument.Load(projectFile);
                foreach (XElement packageReference in project
                    .Descendants()
                    .Where(element => element.Name.LocalName == "PackageReference"))
                {
                    foreach (string metadataName in new[] { "Version", "VersionOverride" })
                    {
                        string? localVersion = GetMsBuildMetadataValue(packageReference, metadataName);
                        if (localVersion is null)
                        {
                            continue;
                        }

                        string packageId = GetMsBuildAttributeValue(packageReference, "Include")
                            ?? GetMsBuildAttributeValue(packageReference, "Update")
                            ?? "<unnamed>";
                        localVersions.Add(
                            $"{Path.GetRelativePath(root, projectFile)}: {packageId} {metadataName}={localVersion}");
                    }
                }
            }
        }

        localVersions.ShouldBeEmpty(
            "Project files must not carry PackageReference Version or VersionOverride metadata; it bypasses the centrally managed package versions.");
    }

    [Fact]
    public async Task SharedConsumerAuthorityValidatorPassesForEveryTrackedMsBuildSurfaceAsync()
    {
        string root = FindRepositoryRoot();
        string wrapperPath = Path.Combine(root, "Directory.Packages.props");
        XDocument wrapper = XDocument.Load(wrapperPath);
        string catalogPath = ResolveSharedPackageVersionsPath(root, wrapper);
        string catalogDirectory = Path.GetDirectoryName(catalogPath).ShouldNotBeNull();
        string buildsRoot = Path.GetDirectoryName(catalogDirectory).ShouldNotBeNull();
        string validatorPath = Path.Combine(
            buildsRoot,
            "Tools",
            "validate-consumer-package-authority.ps1");

        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "pwsh",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                WorkingDirectory = root,
            },
        };
        process.StartInfo.ArgumentList.Add("-NoProfile");
        process.StartInfo.ArgumentList.Add("-File");
        process.StartInfo.ArgumentList.Add(validatorPath);
        process.StartInfo.ArgumentList.Add("-RepositoryRoot");
        process.StartInfo.ArgumentList.Add(root);
        process.StartInfo.ArgumentList.Add("-CatalogPath");
        process.StartInfo.ArgumentList.Add(catalogPath);

        process.Start().ShouldBeTrue("Could not start the shared consumer package authority validator.");
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(_consumerAuthorityValidationTimeout);

        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException exception)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException(
                $"Consumer package authority validation timed out after {_consumerAuthorityValidationTimeout}.",
                exception);
        }

        string output = await outputTask.ConfigureAwait(true);
        string error = await errorTask.ConfigureAwait(true);
        process.ExitCode.ShouldBe(
            0,
            $"Shared consumer package authority validation failed.{Environment.NewLine}{output}{Environment.NewLine}{error}");
    }

    [Theory]
    [InlineData("<PackageVersion Include=\"Example\" Version=\"1.2.3\" />", "Version", "1.2.3")]
    [InlineData("<PackageVersion Include=\"Example\"><Version>1.2.3</Version></PackageVersion>", "Version", "1.2.3")]
    [InlineData("<PackageReference Include=\"Example\" VersionOverride=\"1.2.3\" />", "VersionOverride", "1.2.3")]
    [InlineData("<PackageReference Include=\"Example\"><VersionOverride>1.2.3</VersionOverride></PackageReference>", "VersionOverride", "1.2.3")]
    public void MsBuildMetadataReaderRecognizesAttributeAndChildElementForms(
        string xml,
        string metadataName,
        string expectedValue)
    {
        XElement element = XElement.Parse(xml);

        GetMsBuildMetadataValue(element, metadataName).ShouldBe(expectedValue);
    }

    [Fact]
    public void Root_package_props_resolves_hexalith_builds_from_references_layouts()
    {
        string root = FindRepositoryRoot();
        XDocument packageVersions = XDocument.Load(Path.Combine(root, "Directory.Packages.props"));

        string rootBuildsProps = GetProperty(packageVersions, "Hexalith1BuildPackageProps");
        string parentBuildsProps = GetProperty(packageVersions, "Hexalith2BuildPackageProps");
        string grandparentBuildsProps = GetProperty(packageVersions, "Hexalith3BuildPackageProps");

        rootBuildsProps.ShouldBe(
            MsBuildThisFileDirectory + "references/Hexalith.Builds/Props/Directory.Packages.props");
        parentBuildsProps.ShouldBe(
            MsBuildThisFileDirectory + "../references/Hexalith.Builds/Props/Directory.Packages.props");
        grandparentBuildsProps.ShouldBe(
            MsBuildThisFileDirectory + "../../references/Hexalith.Builds/Props/Directory.Packages.props");

        string eventStoreSubmoduleDirectory = Path.Combine(
            Path.GetTempPath(),
            "parent",
            "references",
            "Hexalith.EventStore") + Path.DirectorySeparatorChar;

        ResolveMsBuildPath(eventStoreSubmoduleDirectory, grandparentBuildsProps)
            .ShouldBe(Path.GetFullPath(Path.Combine(
                Path.GetTempPath(),
                "parent",
                "references",
                "Hexalith.Builds",
                "Props",
                "Directory.Packages.props")));
    }

    [Fact]
    public void Contracts_project_uses_central_unique_ids_package_version()
    {
        string root = FindRepositoryRoot();
        XDocument contractsProject = XDocument.Load(Path.Combine(
            root,
            "src",
            "Hexalith.EventStore.Contracts",
            "Hexalith.EventStore.Contracts.csproj"));

        XElement packageReference = contractsProject
            .Descendants("PackageReference")
            .Single(element => string.Equals(
                element.Attribute("Include")?.Value,
                "Hexalith.Commons.UniqueIds",
                StringComparison.Ordinal));

        packageReference.Attribute("Version").ShouldBeNull();
        packageReference.Attribute("Condition")?.Value.ShouldBe("'$(HexalithCommonsFromSource)' != 'true'");
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

    private static string GetProperty(XDocument document, string name)
    {
        return document
            .Descendants()
            .Where(element => element.Name.LocalName == name)
            .Single()
            .Value;
    }

    private static string? GetMsBuildMetadataValue(XElement element, string metadataName)
        => GetMsBuildAttributeValue(element, metadataName)
        ?? element.Elements().FirstOrDefault(child => child.Name.LocalName == metadataName)?.Value;

    private static string? GetMsBuildAttributeValue(XElement element, string attributeName)
        => element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == attributeName)?.Value;

    private static XDocument LoadSharedPackageVersions(string root, XDocument packageVersions)
        => XDocument.Load(ResolveSharedPackageVersionsPath(root, packageVersions));

    private static string ResolveSharedPackageVersionsPath(string root, XDocument packageVersions)
    {
        // Mirror the four-branch conditional import chain of Directory.Packages.props so the
        // guard validates the catalog actually in effect for the current checkout layout.
        string[] importProperties =
        [
            "Hexalith1BuildPackageProps",
            "Hexalith2BuildPackageProps",
            "Hexalith3BuildPackageProps",
            "Hexalith4BuildPackageProps",
        ];

        foreach (string importProperty in importProperties)
        {
            string importPath = Path.GetFullPath(packageVersions
                .Descendants()
                .Single(element => element.Name.LocalName == importProperty)
                .Value
                .Replace(MsBuildThisFileDirectory, root + Path.DirectorySeparatorChar, StringComparison.Ordinal));

            if (File.Exists(importPath))
            {
                return importPath;
            }
        }

        throw new FileNotFoundException(
            "No declared Hexalith.Builds package props fallback exists; the effective central catalog cannot be validated.");
    }

    private static string ResolveMsBuildPath(string msBuildThisFileDirectory, string path)
    {
        return Path.GetFullPath(path.Replace(
            MsBuildThisFileDirectory,
            msBuildThisFileDirectory,
            StringComparison.Ordinal));
    }
}
