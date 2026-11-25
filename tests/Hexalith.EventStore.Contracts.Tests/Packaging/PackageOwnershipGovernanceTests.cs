using System.Text;
using System.Xml.Linq;

namespace Hexalith.EventStore.Contracts.Tests.Packaging;

public sealed class PackageOwnershipGovernanceTests
{
    private const string SharedCatalogPath = "references/Hexalith.Builds/Props/Directory.Packages.props";

    [Fact]
    public void DocumentationVersionCheckReadsSharedBuildsCatalog()
    {
        string root = FindRepositoryRoot();
        string script = File.ReadAllText(Path.Combine(root, "scripts", "check-doc-versions.sh"));

        script.ShouldContain("resolve_effective_builds_catalog");
        script.ShouldContain(SharedCatalogPath);
        script.ShouldContain($"../{SharedCatalogPath}");
        script.ShouldContain($"../../{SharedCatalogPath}");
    }

    [Fact]
    public void DocumentationVersionCheckPinsExpectedDaprRowMultiplicities()
    {
        string root = FindRepositoryRoot();
        string script = File.ReadAllText(Path.Combine(root, "scripts", "check-doc-versions.sh"));

        script.ShouldContain("[Dapr.Client]=2");
        script.ShouldContain("[Dapr.AspNetCore]=0");
        script.ShouldContain("[Dapr.Actors]=1");
        script.ShouldContain("[Dapr.Actors.AspNetCore]=1");
    }

    [Fact]
    public void InternalPackageGuidanceNamesBuildsAsVersionOwner()
    {
        string root = FindRepositoryRoot();
        string[] paths =
        [
            "_bmad-output/project-context.md",
            "docs/brownfield/development-guide.md",
            "docs/brownfield/project-overview.md",
            "docs/brownfield/source-tree-analysis.md",
            "docs/concepts/choose-the-right-tool.md",
            "docs/guides/dapr-faq.md",
            "docs/guides/deployment-kubernetes.md",
            "docs/guides/troubleshooting.md",
            "docs/reference/nuget-packages.md",
        ];

        string[] missingOwnership = paths
            .Where(path => !File.ReadAllText(Path.Combine(root, path)).Contains(
                SharedCatalogPath,
                StringComparison.Ordinal))
            .ToArray();

        missingOwnership.ShouldBeEmpty(
            "Internal package guidance must identify Hexalith.Builds, not the EventStore wrapper, as version owner.");
    }

    [Fact]
    public void NonCpmVersionCategoriesAreClassified()
    {
        string root = FindRepositoryRoot();
        string guide = File.ReadAllText(Path.Combine(root, "docs", "reference", "nuget-packages.md"));

        guide.ShouldContain("dotnet-tools.json");
        guide.ShouldContain("global.json");
        guide.ShouldContain("package-consumer fixture");
        guide.ShouldContain(".csproj.lscache");
    }

    [Fact]
    public void TenantsSourceGitlinkMatchesItsStablePackageReleaseLine()
    {
        string root = FindRepositoryRoot();
        // This check binds the two gitlinks owned by the standalone EventStore checkout.
        // An umbrella controls its own dependency revisions and leaves nested submodules uninitialized.
        if (PackagingRepositoryPaths.RunGit(root, "rev-parse", "--show-superproject-working-tree").Length != 0)
        {
            return;
        }

        const string tenantsPath = "references/Hexalith.Tenants";
        const string buildsPath = "references/Hexalith.Builds";
        string buildsRoot = PackagingRepositoryPaths.ResolveDependency(root, "Hexalith.Builds");
        string buildsGitlink = Encoding.UTF8.GetString(PackagingRepositoryPaths.RunGit(
            root, "rev-parse", $"HEAD:{buildsPath}")).Trim();
        byte[] catalog = PackagingRepositoryPaths.ReadPinnedFile(
            buildsRoot, "Hexalith.Builds", buildsGitlink, "Props/Directory.Packages.props");
        using MemoryStream catalogStream = new(catalog);
        XDocument packageCatalog = XDocument.Load(catalogStream);
        string packageVersion = packageCatalog.Descendants("HexalithTenantsVersion").Single().Value;
        XElement[] tenantsPackages = packageCatalog.Descendants("PackageVersion")
            .Where(package => GetAttributeValue(package, "Include")?.StartsWith("Hexalith.Tenants.", StringComparison.Ordinal) == true)
            .ToArray();
        tenantsPackages.ShouldContain(package => GetAttributeValue(package, "Include") == "Hexalith.Tenants.Contracts");
        foreach (XElement package in tenantsPackages)
        {
            string? version = GetAttributeValue(package, "Version");
            (version == "$(HexalithTenantsVersion)" ? packageVersion : version).ShouldBe(packageVersion);
        }
        string gitlink = Encoding.UTF8.GetString(PackagingRepositoryPaths.RunGit(
            root, "rev-parse", $"HEAD:{tenantsPath}")).Trim();

        // Source development can advance past the package tag between releases.
        string tenantsRoot = PackagingRepositoryPaths.ResolveDependency(root, "Hexalith.Tenants");
        string[] stableTags = Encoding.UTF8.GetString(PackagingRepositoryPaths.RunGit(tenantsRoot, "tag", "--list", "v*"))
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(tag => Version.TryParse(tag.AsSpan(1), out Version? version)
                && version.Build >= 0 && version.Revision == -1 && tag == $"v{version}")
            .ToArray();
        stableTags.ShouldNotBeEmpty("The Tenants checkout must include release tags.");
        string sourceRelease = Encoding.UTF8.GetString(PackagingRepositoryPaths.RunGit(tenantsRoot,
            ["describe", "--first-parent", "--tags", "--abbrev=0",
                .. stableTags.SelectMany(tag => new[] { "--match", tag }), gitlink])).Trim();

        sourceRelease.ShouldBe($"v{packageVersion}",
            $"Tenants gitlink {gitlink} and the shared package catalog must select the same stable release line.");
    }

    [Fact]
    public void TenantsGatewayAndDomainServiceUseComplementaryModeEdges()
    {
        string root = FindRepositoryRoot();
        XDocument host = XDocument.Load(Path.Combine(
            PackagingRepositoryPaths.ResolveDependency(root, "Hexalith.Tenants"),
            "src",
            "Hexalith.Tenants",
            "Hexalith.Tenants.csproj"));

        // The optional owner-source read proof is a Debug-only test edge, not a
        // runtime package/source pair. Validate every fence before excluding it.
        XElement[] privateTestReferences = host.Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference"
                && ProjectReferencePackageId(GetAttributeValue(element, "Include")) == "Hexalith.EventStore.Testing")
            .ToArray();
        privateTestReferences.Length.ShouldBeLessThanOrEqualTo(1);
        foreach (XElement reference in privateTestReferences)
        {
            reference.Parent.ShouldNotBeNull().Name.LocalName.ShouldBe("ItemGroup");
            GetAttributeValue(reference.Parent!, "Condition").ShouldBe(
                "'$(Configuration)' == 'Debug' and '$(HexalithEventStoreFromSource)' == 'true' and '$(EnablePrivateOwnerSourceReadProof)' == 'true'");
        }

        IGrouping<string, XElement>[] projectReferences = host
            .Descendants()
            .Where(element => !privateTestReferences.Contains(element))
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => (
                Element: element,
                PackageId: ProjectReferencePackageId(GetAttributeValue(element, "Include"))))
            .Where(reference => reference.PackageId.StartsWith(
                "Hexalith.EventStore.",
                StringComparison.OrdinalIgnoreCase))
            .GroupBy(reference => reference.PackageId, reference => reference.Element, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        IGrouping<string, XElement>[] packageReferences = host
            .Descendants()
            .Where(element => element.Name.LocalName == "PackageReference")
            .Select(element => (
                Element: element,
                PackageId: GetAttributeValue(element, "Include")
                    ?? GetAttributeValue(element, "Update")
                    ?? string.Empty))
            .Where(reference => reference.PackageId.StartsWith(
                "Hexalith.EventStore.",
                StringComparison.OrdinalIgnoreCase))
            .GroupBy(reference => reference.PackageId, reference => reference.Element, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        projectReferences.ShouldNotBeEmpty("The Tenants host must retain EventStore dependencies.");
        projectReferences.Select(group => group.Key).Order(StringComparer.OrdinalIgnoreCase).ShouldBe(
            packageReferences.Select(group => group.Key).Order(StringComparer.OrdinalIgnoreCase));

        foreach (IGrouping<string, XElement> projectGroup in projectReferences)
        {
            projectGroup.Count().ShouldBe(
                1,
                $"'{projectGroup.Key}' must have exactly one source ProjectReference.");
            IGrouping<string, XElement> packageGroup = packageReferences.Single(group => string.Equals(
                group.Key,
                projectGroup.Key,
                StringComparison.OrdinalIgnoreCase));
            packageGroup.Count().ShouldBe(
                1,
                $"'{projectGroup.Key}' must have exactly one package PackageReference.");

            XElement projectReference = projectGroup.Single();
            XElement packageReference = packageGroup.Single();
            GetAttributeValue(projectReference, "Condition")
                .ShouldBe("'$(HexalithEventStoreFromSource)' == 'true'");
            GetAttributeValue(packageReference, "Condition")
                .ShouldBe("'$(HexalithEventStoreFromSource)' != 'true'");
            GetMetadataValue(packageReference, "Version").ShouldBeNull();
            GetMetadataValue(packageReference, "VersionOverride").ShouldBeNull();
        }
    }

    private static string ProjectReferencePackageId(string? include)
    {
        if (string.IsNullOrWhiteSpace(include))
        {
            return string.Empty;
        }

        string normalized = include.Replace('\\', '/');
        string fileName = normalized[(normalized.LastIndexOf('/') + 1)..];
        return fileName.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
            ? fileName[..^".csproj".Length]
            : fileName;
    }

    private static string? GetMetadataValue(XElement element, string metadataName)
        => GetAttributeValue(element, metadataName)
        ?? element.Elements().FirstOrDefault(child => child.Name.LocalName == metadataName)?.Value;

    private static string? GetAttributeValue(XElement element, string attributeName)
        => element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == attributeName)?.Value;

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

        throw new DirectoryNotFoundException(
            "Could not locate repository root from the test working directory.");
    }
}
