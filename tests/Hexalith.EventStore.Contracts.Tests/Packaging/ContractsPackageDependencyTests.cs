using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace Hexalith.EventStore.Contracts.Tests.Packaging;

public sealed class ContractsPackageDependencyTests
{
    private const string MsBuildThisFileDirectory = "$(MSBuildThisFileDirectory)";
    private static readonly TimeSpan _consumerAuthorityValidationTimeout = TimeSpan.FromMinutes(8);
    private const string PublishedPackageObservation =
        "_bmad-output/implementation-artifacts/evidence/6-1-p1r-31150-published-run/preflight/package-observation/";
    private static readonly (string FileName, string Sha256)[] _sealedPackageObservations =
    [
        ("Directory.Packages.props", "7d5cfc543cb96a49d4ca995b0a1d59d9f503d00f4d74cc568f703c8c92de0f28"),
        ("Identity.csproj", "28bf83cef929e65c35ab501f1b8495c40de3590c318172473a3970a6262ad95c"),
    ];

    // Hash-bound standalone consumers and the recorded verification harness restore published
    // release and rollback packages outside the live build graph. Exclusions name exact files.
    private static readonly string[] _standaloneEvidenceProbeProjects =
    [
        "_bmad-output/implementation-artifacts/evidence/6-1-p1r-3108/consumer/Consumer.csproj",
        "_bmad-output/implementation-artifacts/evidence/6-1-p1r-3108/rollback-probe/v3108/Probe.csproj",
        "_bmad-output/implementation-artifacts/evidence/6-1-p1r-3108/rollback-probe/v370/Probe.csproj",
        "_bmad-output/implementation-artifacts/evidence/6-1-p1r-3109/consumer/Consumer.csproj",
        "_bmad-output/implementation-artifacts/evidence/6-1-p1r-3109/rollback-probe/v3109/Probe.csproj",
        "_bmad-output/implementation-artifacts/evidence/6-1-p1r-3109/rollback-probe/v370/Probe.csproj",
        "_bmad-output/implementation-artifacts/evidence/6-1-p1r-3110/consumer/Consumer.csproj",
        "_bmad-output/implementation-artifacts/evidence/6-1-p1r-3110/verification/Directory.Build.props",
        "_bmad-output/implementation-artifacts/evidence/6-1-p1r-3110/verification/Directory.Build.targets",
        "_bmad-output/implementation-artifacts/evidence/6-1-p1r-3110/verification/Directory.Packages.props",
        "_bmad-output/implementation-artifacts/evidence/6-1-p1r-3110/verification/domain/Domain.csproj",
        "_bmad-output/implementation-artifacts/evidence/6-1-p1r-3110/verification/host/Host.csproj",
        "_bmad-output/implementation-artifacts/evidence/6-1-p1r-3110/verification/probe/Probe.csproj",
        PublishedPackageObservation + "Directory.Packages.props",
        PublishedPackageObservation + "Identity.csproj",
    ];

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
        (int exitCode, string output, string error) = await RunConsumerAuthorityValidatorAsync(root, root, catalogPath).ConfigureAwait(true);
        exitCode.ShouldBe(
            0,
            $"Shared consumer package authority validation failed.{Environment.NewLine}{output}{Environment.NewLine}{error}");
    }

    [Fact]
    public void PublishedPackageObservationExclusions_RequireSealedContent()
    {
        string root = FindRepositoryRoot();
        string[] seals = File.ReadAllLines(Path.Combine(root, PublishedPackageObservation, "..", "SHA256SUMS"));
        foreach ((string fileName, string sha256) in _sealedPackageObservations)
        {
            seals.ShouldContain(sha256 + "  package-observation/" + fileName);
            byte[] bytes = File.ReadAllBytes(Path.Combine(root, PublishedPackageObservation, fileName));
            VerifySealedPackageObservation(bytes, sha256);
            Should.Throw<ShouldAssertException>(() => VerifySealedPackageObservation([.. bytes, (byte)'x'], sha256));
        }
    }

    /// <summary>
    /// Verifies the evidence exemptions cannot hide a new executable-project version override.
    /// </summary>
    /// <param name="metadata">The project-level version metadata to reject.</param>
    /// <param name="directory">The directory containing the nonexempt executable.</param>
    /// <param name="fileName">The nonexempt MSBuild surface.</param>
    /// <param name="tracked">Whether discovery uses an isolated Git index.</param>
    [Theory]
    [InlineData("Version", "src", "Executable.csproj", false)]
    [InlineData("VersionOverride", "src", "Executable.csproj", false)]
    [InlineData("Version", "_bmad-output/implementation-artifacts/evidence/6-1-p1r-3110/verification", "Executable.csproj", false)]
    [InlineData("VersionOverride", "_bmad-output/implementation-artifacts/evidence/6-1-p1r-3110/verification", "Executable.csproj", false)]
    [InlineData("Version", "src", "Executable.csproj", true)]
    [InlineData("VersionOverride", "src", "Executable.csproj", true)]
    [InlineData("Version", "_bmad-output/implementation-artifacts/evidence/6-1-p1r-3110/verification", "Executable.csproj", true)]
    [InlineData("VersionOverride", "_bmad-output/implementation-artifacts/evidence/6-1-p1r-3110/verification", "Executable.csproj", true)]
    [InlineData("Version", "_bmad-output/implementation-artifacts/evidence/6-1-p1r-31150-published-run/preflight/package-observation", "Executable.csproj", false)]
    [InlineData("VersionOverride", "_bmad-output/implementation-artifacts/evidence/6-1-p1r-31150-published-run/preflight/package-observation", "Executable.csproj", false)]
    [InlineData("Version", "_bmad-output/implementation-artifacts/evidence/6-1-p1r-31150-published-run/preflight/package-observation", "Executable.csproj", true)]
    [InlineData("VersionOverride", "_bmad-output/implementation-artifacts/evidence/6-1-p1r-31150-published-run/preflight/package-observation", "Executable.csproj", true)]
    [InlineData("Version", "_bmad-output/implementation-artifacts/evidence/6-1-p1r-31150-published-run/preflight/package-observation", "Adjacent.props", false)]
    [InlineData("Version", "_bmad-output/implementation-artifacts/evidence/6-1-p1r-31150-published-run/preflight/package-observation", "Adjacent.props", true)]
    [InlineData("VersionOverride", "_bmad-output/implementation-artifacts/evidence/6-1-p1r-31150-published-run/preflight/package-observation", "Adjacent.props", false)]
    [InlineData("VersionOverride", "_bmad-output/implementation-artifacts/evidence/6-1-p1r-31150-published-run/preflight/package-observation", "Adjacent.props", true)]
    public async Task SharedConsumerAuthorityValidatorRejectsNonExemptExecutableOverrideAsync(string metadata, string directory, string fileName, bool tracked)
    {
        string root = FindRepositoryRoot();
        string catalogPath = ResolveSharedPackageVersionsPath(root, XDocument.Load(Path.Combine(root, "Directory.Packages.props")));
        string fixture = Path.Combine(Path.GetTempPath(), "consumer-authority-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        try
        {
            foreach (string relative in _standaloneEvidenceProbeProjects)
            {
                string destination = Path.Combine(fixture, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(Path.Combine(root, relative), destination);
            }

            new XDocument(new XElement("Project",
                new XElement("PropertyGroup", new XElement("ManagePackageVersionsCentrally", "true")),
                new XElement("Import", new XAttribute("Project", catalogPath))))
                .Save(Path.Combine(fixture, "Directory.Packages.props"));
            string executable = Path.Combine(fixture, directory, fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(executable)!);
            File.WriteAllText(executable,
                $"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType></PropertyGroup><ItemGroup><PackageReference Include=\"Hexalith.EventStore.Server\" {metadata}=\"9.9.9\" /></ItemGroup></Project>");

            if (tracked)
            {
                RunFixtureGit(fixture, "init", "--quiet");
                RunFixtureGit(fixture, "add", "--", ".");
            }

            (int exitCode, _, string error) = await RunConsumerAuthorityValidatorAsync(root, fixture, catalogPath).ConfigureAwait(true);
            exitCode.ShouldBe(1, error);
            error.ShouldContain($"{fileName} contains PackageReference {metadata} metadata '9.9.9'");
        }
        finally
        {
            Directory.Delete(fixture, recursive: true);
        }
    }

    /// <summary>
    /// Verifies effective catalogs and validators reject undeclared or unowned source directories before use.
    /// </summary>
    /// <param name="layout">The invalid catalog source layout.</param>
    [Theory]
    [InlineData("undeclared")]
    [InlineData("wrong-identity")]
    [InlineData("unowned")]
    public async Task EffectiveCatalogAndValidatorRequireDeclaredBuildsOwnershipAsync(string layout)
    {
        string fixture = Path.Combine(Path.GetTempPath(), "catalog-ownership-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        try
        {
            RunFixtureGit(fixture, "init", "--quiet");
            RunFixtureGit(fixture, "remote", "add", "origin", "https://github.com/Hexalith/Hexalith.EventStore.git");
            string builds = Path.Combine(fixture, "references", "Hexalith.Builds");
            string catalog = Path.Combine(builds, "Props", "Directory.Packages.props");
            Directory.CreateDirectory(Path.GetDirectoryName(catalog)!);
            File.WriteAllText(catalog, "This must never be parsed as XML.");
            if (layout != "unowned")
            {
                RunFixtureGit(builds, "init", "--quiet");
                RunFixtureGit(builds, "remote", "add", "origin", "https://github.com/Hexalith/" +
                    (layout == "wrong-identity" ? "Hexalith.Tenants" : "Hexalith.Builds") + ".git");
            }

            if (layout != "undeclared")
            {
                File.WriteAllText(Path.Combine(fixture, ".gitmodules"),
                    "[submodule \"Builds\"]\n\tpath = references/Hexalith.Builds\n\turl = https://github.com/Hexalith/Hexalith.Builds.git\n");
            }

            XDocument wrapper = new(new XElement("Project", new XElement("PropertyGroup",
                Enumerable.Range(1, 4).Select(index => new XElement($"Hexalith{index}BuildPackageProps", catalog)))));
            Should.Throw<InvalidDataException>(() => LoadSharedPackageVersions(fixture, wrapper));
            _ = await Should.ThrowAsync<InvalidDataException>(() => RunConsumerAuthorityValidatorAsync(fixture, fixture, catalog)).ConfigureAwait(true);
        }
        finally
        {
            Directory.Delete(fixture, recursive: true);
        }
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunConsumerAuthorityValidatorAsync(string repositoryRoot, string root, string catalogPath)
    {
        string buildsRoot = VerifyCatalogRepository(repositoryRoot, catalogPath);
        foreach ((string fileName, string sha256) in _sealedPackageObservations)
        {
            VerifySealedPackageObservation(
                File.ReadAllBytes(Path.Combine(repositoryRoot, PublishedPackageObservation, fileName)), sha256);
        }

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
        PackagingRepositoryPaths.RemoveRepositorySelectors(process.StartInfo);
        process.StartInfo.ArgumentList.Add("-NoProfile");
        process.StartInfo.ArgumentList.Add("-File");
        process.StartInfo.ArgumentList.Add(validatorPath);
        process.StartInfo.ArgumentList.Add("-RepositoryRoot");
        process.StartInfo.ArgumentList.Add(root);
        process.StartInfo.ArgumentList.Add("-CatalogPath");
        process.StartInfo.ArgumentList.Add(catalogPath);
        process.StartInfo.ArgumentList.Add("-ExcludedPath");
        process.StartInfo.ArgumentList.Add(string.Join(';', _standaloneEvidenceProbeProjects));

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
        return (process.ExitCode, output, error);
    }

    private static void VerifySealedPackageObservation(byte[] bytes, string sha256)
    {
        byte[] canonical = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes).Replace("\r\n", "\n", StringComparison.Ordinal));
        Convert.ToHexString(SHA256.HashData(canonical)).ToLowerInvariant().ShouldBe(
            sha256, "Historical package-authority exclusions require their original sealed contents.");
    }

    private static void RunFixtureGit(string root, params string[] arguments)
    {
        ProcessStartInfo start = new("git") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        PackagingRepositoryPaths.RemoveRepositorySelectors(start);
        start.ArgumentList.Add("--no-replace-objects");
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start).ShouldNotBeNull();
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        process.WaitForExit(30_000).ShouldBeTrue("Temporary consumer repository setup timed out.");
        process.ExitCode.ShouldBe(0, error.GetAwaiter().GetResult());
        _ = output.GetAwaiter().GetResult();
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
                return PackagingRepositoryPaths.VerifyRepositoryRoot(directory.FullName, "Hexalith.EventStore");
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
                _ = VerifyCatalogRepository(root, importPath);
                return importPath;
            }
        }

        throw new FileNotFoundException(
            "No declared Hexalith.Builds package props fallback exists; the effective central catalog cannot be validated.");
    }

    private static string VerifyCatalogRepository(string root, string catalogPath)
    {
        string catalogDirectory = Path.GetDirectoryName(catalogPath).ShouldNotBeNull();
        string buildsRoot = Path.GetDirectoryName(catalogDirectory).ShouldNotBeNull();
        return PackagingRepositoryPaths.VerifyDeclaredDependency(root, "Hexalith.Builds", buildsRoot);
    }

    private static string ResolveMsBuildPath(string msBuildThisFileDirectory, string path)
    {
        return Path.GetFullPath(path.Replace(
            MsBuildThisFileDirectory,
            msBuildThisFileDirectory,
            StringComparison.Ordinal));
    }
}
