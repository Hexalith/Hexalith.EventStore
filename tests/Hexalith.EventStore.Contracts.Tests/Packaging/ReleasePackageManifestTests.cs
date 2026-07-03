using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Hexalith.EventStore.Contracts.Tests.Packaging;

public sealed class ReleasePackageManifestTests
{
    private const string GeneratorPackageId = "Hexalith.EventStore.RestApi.Generators";
    private const string GeneratorProjectPath = "src/Hexalith.EventStore.RestApi.Generators/Hexalith.EventStore.RestApi.Generators.csproj";

    [Fact]
    public void Release_manifest_includes_rest_api_generator_package()
    {
        ReleasePackage[] packages = LoadReleasePackages();

        ReleasePackage generator = packages
            .SingleOrDefault(p => p.Id == GeneratorPackageId)
            .ShouldNotBeNull($"Release manifest must include {GeneratorPackageId}.");

        generator.Project.ShouldBe(GeneratorProjectPath);
    }

    [Fact]
    public void Release_manifest_projects_exist_and_entries_are_unique()
    {
        string root = FindRepositoryRoot();
        ReleasePackage[] packages = LoadReleasePackages();

        packages.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count().ShouldBe(
            packages.Length,
            "Release package IDs must be unique because the pack/validate scripts use the manifest as the package source of truth.");

        packages.Select(p => p.Project).Distinct(StringComparer.Ordinal).Count().ShouldBe(
            packages.Length,
            "Release package project paths must be unique because duplicate project packing would hide release inventory mistakes.");

        foreach (ReleasePackage package in packages)
        {
            File.Exists(Path.Combine(root, package.Project)).ShouldBeTrue(
                $"Release package project must exist: {package.Project}");
        }
    }

    [Fact]
    public void Rest_api_generator_project_packs_analyzer_assets_without_runtime_lib_output()
    {
        string root = FindRepositoryRoot();
        string projectPath = Path.Combine(root, GeneratorProjectPath);
        XDocument project = XDocument.Load(projectPath);

        project
            .Descendants("IncludeBuildOutput")
            .Single()
            .Value
            .ShouldBe("false");

        project
            .Descendants("SuppressDependenciesWhenPacking")
            .Single()
            .Value
            .ShouldBe("true");

        XElement analyzerDll = project
            .Descendants("None")
            .Single(element => string.Equals(
                element.Attribute("Include")?.Value,
                "$(OutputPath)\\$(AssemblyName).dll",
                StringComparison.Ordinal));

        analyzerDll.Attribute("Pack")?.Value.ShouldBe("true");
        analyzerDll.Attribute("PackagePath")?.Value.ShouldBe("analyzers/dotnet/cs");

        project
            .Descendants()
            .Where(element => string.Equals(element.Attribute("PackagePath")?.Value, "lib", StringComparison.Ordinal)
                           || element.Attribute("PackagePath")?.Value.StartsWith("lib/", StringComparison.Ordinal) == true)
            .ShouldBeEmpty("The REST API generator package must not expose runtime lib assets.");
    }

    [Fact]
    public void Semantic_release_delegates_package_inventory_to_manifest_scripts()
    {
        string root = FindRepositoryRoot();
        using JsonDocument releaseConfig = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, ".releaserc.json")));

        string prepareCommand = releaseConfig
            .RootElement
            .GetProperty("plugins")
            .EnumerateArray()
            .Where(plugin => plugin.ValueKind == JsonValueKind.Array)
            .Select(plugin => plugin[1])
            .Where(pluginConfig => pluginConfig.TryGetProperty("prepareCmd", out _))
            .Select(pluginConfig => pluginConfig.GetProperty("prepareCmd").GetString())
            .Single()
            .ShouldNotBeNull();

        prepareCommand.ShouldContain("tools/pack-release-packages.py");
        prepareCommand.ShouldContain("tools/validate-release-packages.py");
        prepareCommand.ShouldNotContain("dotnet pack");
    }

    [Fact]
    public void Active_package_docs_do_not_contain_obsolete_release_package_counts()
    {
        string root = FindRepositoryRoot();
        int manifestPackageCount = LoadReleasePackages().Length;
        manifestPackageCount.ShouldBeGreaterThan(8);

        string[] docPaths =
        [
            "AGENTS.md",
            "CLAUDE.md",
            "docs/reference/nuget-packages.md",
            "docs/brownfield/project-overview.md",
            "docs/brownfield/index.md",
            "docs/guides/upgrade-path.md",
            "docs/ci-secrets-checklist.md",
        ];

        Regex obsoleteCountPattern = new(
            @"\b(?:all\s+)?(?:6|8)\s+(?:published\s+)?(?:Hexalith\.EventStore\s+)?(?:NuGet\s+)?packages\b|\bpublish\s+6\s+NuGet\s+packages\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        foreach (string docPath in docPaths)
        {
            string text = File.ReadAllText(Path.Combine(root, docPath));
            obsoleteCountPattern.IsMatch(text).ShouldBeFalse(
                $"{docPath} must describe the manifest-driven package set instead of stale 6/8-package counts.");
        }
    }

    [Fact]
    public void Active_docs_do_not_use_superseded_ui_host_generator_wording()
    {
        string root = FindRepositoryRoot();
        string[] activeDocs = Directory
            .EnumerateFiles(Path.Combine(root, "docs"), "*.*", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}reference{Path.DirectorySeparatorChar}api{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Concat([Path.Combine(root, "AGENTS.md"), Path.Combine(root, "CLAUDE.md")])
            .ToArray();

        string[] stalePhrases =
        [
            "generated controllers into the domain UI host",
            "generate controllers into Hexalith.Tenants.UI",
            "UI host owns generated controllers",
            "Sample.BlazorUI hosts generated API controllers",
        ];

        List<string> offenders = [];
        foreach (string path in activeDocs)
        {
            string text = File.ReadAllText(path);
            foreach (string stalePhrase in stalePhrases)
            {
                if (text.Contains(stalePhrase, StringComparison.OrdinalIgnoreCase))
                {
                    offenders.Add($"{Path.GetRelativePath(root, path)}: {stalePhrase}");
                }
            }
        }

        offenders.ShouldBeEmpty(
            "Active docs must describe the July 2 external API host/client-library split, not the superseded UI-host controller model.");
    }

    private static ReleasePackage[] LoadReleasePackages()
    {
        string root = FindRepositoryRoot();
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "tools", "release-packages.json")));

        return manifest
            .RootElement
            .GetProperty("packages")
            .EnumerateArray()
            .Select(package => new ReleasePackage(
                package.GetProperty("id").GetString().ShouldNotBeNull(),
                package.GetProperty("project").GetString().ShouldNotBeNull()))
            .ToArray();
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Directory.Packages.props"))
                && File.Exists(Path.Combine(directory.FullName, "tools", "release-packages.json")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from the test working directory.");
    }

    private sealed record ReleasePackage(string Id, string Project);
}
