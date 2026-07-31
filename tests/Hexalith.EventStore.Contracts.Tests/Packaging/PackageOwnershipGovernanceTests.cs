using System.Xml.Linq;
using YamlDotNet.RepresentationModel;

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
    public void EventStoreDependabotDoesNotOwnNuGetCatalogUpdates()
    {
        string root = FindRepositoryRoot();
        using var reader = new StreamReader(Path.Combine(root, ".github", "dependabot.yml"));
        var yaml = new YamlStream();
        yaml.Load(reader);

        YamlMappingNode document = yaml.Documents.Single().RootNode.ShouldBeOfType<YamlMappingNode>();
        YamlSequenceNode updates = document.Children[new YamlScalarNode("updates")]
            .ShouldBeOfType<YamlSequenceNode>();
        string[] ecosystems = updates.Children
            .Select(node => node.ShouldBeOfType<YamlMappingNode>())
            .Select(update => update.Children[new YamlScalarNode("package-ecosystem")])
            .Select(node => node.ShouldBeOfType<YamlScalarNode>().Value.ShouldNotBeNull())
            .ToArray();

        ecosystems.ShouldNotContain(
            ecosystem => string.Equals(ecosystem, "nuget", StringComparison.OrdinalIgnoreCase));
        ecosystems.ShouldContain(
            ecosystem => string.Equals(ecosystem, "npm", StringComparison.OrdinalIgnoreCase));
        ecosystems.ShouldContain(
            ecosystem => string.Equals(ecosystem, "github-actions", StringComparison.OrdinalIgnoreCase));
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
    public void GatewayLedgerDoesNotReportObsoleteUnconditionalSourceEdge()
    {
        string root = FindRepositoryRoot();
        string deferredWork = File.ReadAllText(Path.Combine(
            root,
            "_bmad-output",
            "implementation-artifacts",
            "deferred-work.md"));

        deferredWork.ShouldNotContain(
            "references `Hexalith.EventStore.Gateway` as an unconditional source ProjectReference");
    }

    [Fact]
    public void TenantsGatewayAndDomainServiceUseComplementaryModeEdges()
    {
        string root = FindRepositoryRoot();
        XDocument host = XDocument.Load(Path.Combine(
            root,
            "references",
            "Hexalith.Tenants",
            "src",
            "Hexalith.Tenants",
            "Hexalith.Tenants.csproj"));

        IGrouping<string, XElement>[] projectReferences = host
            .Descendants()
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
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate repository root from the test working directory.");
    }
}
