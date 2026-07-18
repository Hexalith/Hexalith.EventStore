using System.Xml.Linq;

namespace Hexalith.EventStore.Contracts.Tests.Packaging;

public sealed class ContractsPackageDependencyTests
{
    private const string MsBuildThisFileDirectory = "$(MSBuildThisFileDirectory)";

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
    public void Root_package_props_does_not_override_builds_central_versions()
    {
        string root = FindRepositoryRoot();
        XDocument packageVersions = XDocument.Load(Path.Combine(root, "Directory.Packages.props"));
        XDocument sharedPackageVersions = LoadSharedPackageVersions(root, packageVersions);

        // Ledgered exception (deferred-work.md 2026-07-17): remove alongside the next
        // Builds-owned version reconciliation. Any other overlap silently freezes the
        // local copy when the Builds-central version moves.
        string[] allowedOverrides = ["Microsoft.Playwright"];

        HashSet<string> centralPackageIds = sharedPackageVersions
            .Descendants("PackageVersion")
            .Select(element => element.Attribute("Include")?.Value ?? element.Attribute("Update")?.Value)
            .OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // A restructured Builds catalog (sub-imports, xmlns, renamed attributes) must fail
        // this guard loudly instead of vacuously disabling it through an empty central set.
        centralPackageIds.ShouldNotBeEmpty(
            "The Builds-central catalog yielded no PackageVersion entries; the masking guard cannot run against an empty central set.");

        string[] maskingDeclarations = packageVersions
            .Descendants("PackageVersion")
            .Select(element => element.Attribute("Include")?.Value ?? element.Attribute("Update")?.Value)
            .OfType<string>()
            .Where(id => centralPackageIds.Contains(id))
            .Where(id => !allowedOverrides.Contains(id, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        maskingDeclarations.ShouldBeEmpty(
            "The root Directory.Packages.props must not redeclare Builds-central package versions; a local Include/Update silently pins the repository when the central version moves.");

        // An allowlisted override is tolerated only as an identical-to-central no-op; a
        // divergent value is the same silent version freeze this guard exists to catch.
        foreach (string allowedOverride in allowedOverrides)
        {
            string? localVersion = packageVersions
                .Descendants("PackageVersion")
                .Where(element => string.Equals(
                    element.Attribute("Include")?.Value ?? element.Attribute("Update")?.Value,
                    allowedOverride,
                    StringComparison.OrdinalIgnoreCase))
                .Select(element => element.Attribute("Version")?.Value)
                .SingleOrDefault();

            if (localVersion is null)
            {
                continue;
            }

            string? centralVersion = sharedPackageVersions
                .Descendants("PackageVersion")
                .Where(element => string.Equals(
                    element.Attribute("Include")?.Value ?? element.Attribute("Update")?.Value,
                    allowedOverride,
                    StringComparison.OrdinalIgnoreCase))
                .Select(element => element.Attribute("Version")?.Value)
                .SingleOrDefault();

            localVersion.ShouldBe(
                centralVersion,
                $"The allowlisted '{allowedOverride}' override must match the Builds-central version; remove the local line or reconcile it with the central catalog.");
        }
    }

    [Fact]
    public void Project_files_do_not_version_override_central_package_versions()
    {
        string root = FindRepositoryRoot();

        // CPM gives PackageReference VersionOverride precedence over every central pin, so a
        // single project-level attribute silently bypasses the Builds-owns-versions invariant
        // without touching Directory.Packages.props.
        string[] projectDirectories = ["src", "tests", "perf", "samples", "tools"];
        List<string> versionOverrides = [];

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
                foreach (XElement packageReference in project.Descendants("PackageReference"))
                {
                    if (packageReference.Attribute("VersionOverride") is null)
                    {
                        continue;
                    }

                    string packageId = packageReference.Attribute("Include")?.Value
                        ?? packageReference.Attribute("Update")?.Value
                        ?? "<unnamed>";
                    versionOverrides.Add($"{Path.GetRelativePath(root, projectFile)}: {packageId}");
                }
            }
        }

        versionOverrides.ShouldBeEmpty(
            "Project files must not carry PackageReference VersionOverride attributes; they bypass the centrally managed package versions.");
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
            .Descendants(name)
            .Single()
            .Value;
    }

    private static XDocument LoadSharedPackageVersions(string root, XDocument packageVersions)
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
                .Descendants(importProperty)
                .Single()
                .Value
                .Replace(MsBuildThisFileDirectory, root + Path.DirectorySeparatorChar, StringComparison.Ordinal));

            if (File.Exists(importPath))
            {
                return XDocument.Load(importPath);
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
