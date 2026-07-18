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

        string[] maskingDeclarations = packageVersions
            .Descendants("PackageVersion")
            .Select(element => element.Attribute("Include")?.Value ?? element.Attribute("Update")?.Value)
            .OfType<string>()
            .Where(id => centralPackageIds.Contains(id))
            .Where(id => !allowedOverrides.Contains(id, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        maskingDeclarations.ShouldBeEmpty(
            "The root Directory.Packages.props must not redeclare Builds-central package versions; a local Include/Update silently pins the repository when the central version moves.");
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
        string importPath = packageVersions
            .Descendants("Hexalith1BuildPackageProps")
            .Single()
            .Value
            .Replace("$(MSBuildThisFileDirectory)", root + Path.DirectorySeparatorChar, StringComparison.Ordinal);

        return XDocument.Load(importPath);
    }

    private static string ResolveMsBuildPath(string msBuildThisFileDirectory, string path)
    {
        return Path.GetFullPath(path.Replace(
            MsBuildThisFileDirectory,
            msBuildThisFileDirectory,
            StringComparison.Ordinal));
    }
}
