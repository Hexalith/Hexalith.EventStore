using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Hexalith.EventStore.Contracts.Tests.Packaging;

public sealed class ReleasePackageManifestTests
{
    private const string DomainServicePackageId = "Hexalith.EventStore.DomainService";
    private const string DomainServiceProjectPath = "src/Hexalith.EventStore.DomainService/Hexalith.EventStore.DomainService.csproj";
    private const int ExpectedManifestPackageCount = 14;
    private const string GeneratorPackageId = "Hexalith.EventStore.RestApi.Generators";
    private const string GeneratorProjectPath = "src/Hexalith.EventStore.RestApi.Generators/Hexalith.EventStore.RestApi.Generators.csproj";
    private const string ServiceDefaultsPackageId = "Hexalith.EventStore.ServiceDefaults";
    private const string ServiceDefaultsProjectPath = "src/Hexalith.EventStore.ServiceDefaults/Hexalith.EventStore.ServiceDefaults.csproj";
    private static readonly TimeSpan MsBuildPropertyTimeout = TimeSpan.FromSeconds(60);

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
    public void Release_manifest_includes_domain_service_sdk_packages()
    {
        string root = FindRepositoryRoot();
        ReleasePackage[] packages = LoadReleasePackages();

        packages.Length.ShouldBe(
            ExpectedManifestPackageCount,
            "Story 1.7 pins the manifest-governed EventStore release inventory at 14 packages; package additions/removals must update the release governance tests and docs together.");

        ReleasePackage domainService = packages
            .SingleOrDefault(p => p.Id == DomainServicePackageId)
            .ShouldNotBeNull($"Release manifest must include the domain-service SDK package {DomainServicePackageId}.");

        ReleasePackage serviceDefaults = packages
            .SingleOrDefault(p => p.Id == ServiceDefaultsPackageId)
            .ShouldNotBeNull($"Release manifest must include the shared service defaults SDK package {ServiceDefaultsPackageId}.");

        domainService.Project.ShouldBe(DomainServiceProjectPath);
        serviceDefaults.Project.ShouldBe(ServiceDefaultsProjectPath);

        string domainServiceProjectPath = Path.Combine(root, domainService.Project);
        string serviceDefaultsProjectPath = Path.Combine(root, serviceDefaults.Project);

        EvaluatedProjectProperty(domainServiceProjectPath, "IsPackable").ShouldBe(
            "true",
            $"{DomainServicePackageId} must evaluate as packable in Release, including imported props/targets.");
        EvaluatedProjectProperty(serviceDefaultsProjectPath, "IsPackable").ShouldBe(
            "true",
            $"{ServiceDefaultsPackageId} must evaluate as packable in Release, including imported props/targets.");

        EvaluatedProjectProperty(domainServiceProjectPath, "PackageId").ShouldBe(
            DomainServicePackageId,
            $"{DomainServicePackageId} must produce the package identity declared by the manifest.");
        EvaluatedProjectProperty(serviceDefaultsProjectPath, "PackageId").ShouldBe(
            ServiceDefaultsPackageId,
            $"{ServiceDefaultsPackageId} must produce the package identity declared by the manifest.");
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
        manifestPackageCount.ShouldBe(ExpectedManifestPackageCount);

        Regex obsoleteCountPattern = new(
            @"\b(?:all[-\s]+)?(?:6|8|13|six|eight|thirteen)[-\s]+(?:published[-\s]+)?(?:Hexalith\.EventStore[-\s]+)?(?:NuGet[-\s]+)?packages?\b|\bpublish[-\s]+(?:6|8|13|six|eight|thirteen)[-\s]+NuGet[-\s]+packages?\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        foreach (string docPath in ActivePackageDocumentationPaths(root))
        {
            string text = File.ReadAllText(Path.Combine(root, docPath));
            obsoleteCountPattern.IsMatch(text).ShouldBeFalse(
                $"{docPath} must describe the manifest-driven package set instead of stale release package counts.");
        }
    }

    [Fact]
    public void Active_package_inventory_docs_match_manifest_package_set()
    {
        string root = FindRepositoryRoot();
        ReleasePackage[] packages = LoadReleasePackages();
        string[] docPaths =
        [
            "AGENTS.md",
            "CLAUDE.md",
            "docs/reference/nuget-packages.md",
            "docs/brownfield/project-overview.md",
            "docs/brownfield/architecture.md",
            "_bmad-output/project-context.md",
        ];

        foreach (string docPath in docPaths)
        {
            string text = File.ReadAllText(Path.Combine(root, docPath));

            text.Contains($"{packages.Length} packages", StringComparison.Ordinal).ShouldBeTrue(
                $"{docPath} must state the current manifest package count.");

            foreach (ReleasePackage package in packages)
            {
                text.Contains(package.Id, StringComparison.Ordinal).ShouldBeTrue(
                    $"{docPath} must name every manifest package, including {package.Id}.");
            }
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

    private static string EvaluatedProjectProperty(string projectPath, string propertyName)
    {
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            },
        };

        process.StartInfo.ArgumentList.Add("msbuild");
        process.StartInfo.ArgumentList.Add(projectPath);
        process.StartInfo.ArgumentList.Add($"-getProperty:{propertyName}");
        process.StartInfo.ArgumentList.Add("-p:Configuration=Release");
        process.StartInfo.ArgumentList.Add("-p:UseHexalithProjectReferences=false");

        process.StartInfo.WorkingDirectory = FindRepositoryRoot();

        process.Start().ShouldBeTrue($"Could not start dotnet msbuild for {projectPath}.");
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit((int)MsBuildPropertyTimeout.TotalMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException(
                $"dotnet msbuild -getProperty:{propertyName} timed out after {MsBuildPropertyTimeout} for {projectPath}.");
        }

        string output = outputTask.GetAwaiter().GetResult();
        string error = errorTask.GetAwaiter().GetResult();

        process.ExitCode.ShouldBe(
            0,
            $"dotnet msbuild -getProperty:{propertyName} failed for {projectPath}: {error}");

        return output.Trim();
    }

    private static string[] ActivePackageDocumentationPaths(string root)
        => Directory
            .EnumerateFiles(Path.Combine(root, "docs"), "*.md", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}reference{Path.DirectorySeparatorChar}api{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Concat(
            [
                Path.Combine(root, "AGENTS.md"),
                Path.Combine(root, "CLAUDE.md"),
                Path.Combine(root, "_bmad-output", "project-context.md"),
            ])
            .Select(path => Path.GetRelativePath(root, path))
            .Order(StringComparer.Ordinal)
            .ToArray();

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
