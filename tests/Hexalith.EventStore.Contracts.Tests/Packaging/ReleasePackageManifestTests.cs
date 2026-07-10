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

        string publishCommand = releaseConfig
            .RootElement
            .GetProperty("plugins")
            .EnumerateArray()
            .Where(plugin => plugin.ValueKind == JsonValueKind.Array)
            .Select(plugin => plugin[1])
            .Where(pluginConfig => pluginConfig.TryGetProperty("publishCmd", out _))
            .Select(pluginConfig => pluginConfig.GetProperty("publishCmd").GetString())
            .Single()
            .ShouldNotBeNull();

        publishCommand.ShouldContain("scripts/validate-release-secrets.sh");
        publishCommand.ShouldContain("dotnet nuget push");
        publishCommand.ShouldContain("./.hexalith/release/publish-containers.sh ${nextRelease.version}");
        publishCommand.IndexOf("scripts/validate-release-secrets.sh", StringComparison.Ordinal).ShouldBeLessThan(
            publishCommand.IndexOf("dotnet nuget push", StringComparison.Ordinal),
            "Release secrets must be validated before any irreversible NuGet publish command runs.");
    }

    [Fact]
    public void Shared_ci_workflow_uses_domain_ci_with_deterministic_server_tests()
    {
        string root = FindRepositoryRoot();
        string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "ci.yml"));

        workflow.ShouldContain("uses: Hexalith/Hexalith.Builds/.github/workflows/domain-ci.yml@main");
        workflow.ShouldContain("build-timeout-minutes: 40");
        workflow.ShouldContain("run-consumer-validation: true");
        workflow.ShouldContain("tests/Hexalith.EventStore.Server.Tests");
        workflow.ShouldNotContain("tests/Hexalith.EventStore.Server.LiveSidecar.Tests");
        workflow.ShouldNotContain("run-coverage-gate:");
        workflow.ShouldNotContain("Category!=LiveSidecar");
        workflow.ShouldNotContain("runs-on:");
        workflow.ShouldNotContain("steps:");
    }

    [Fact]
    public void Live_sidecar_workflow_targets_live_project_outside_release_gate()
    {
        string root = FindRepositoryRoot();
        string integration = File.ReadAllText(Path.Combine(root, ".github", "workflows", "integration.yml"));
        string release = File.ReadAllText(Path.Combine(root, ".github", "workflows", "release.yml"));

        integration.ShouldContain("dotnet test tests/Hexalith.EventStore.Server.LiveSidecar.Tests/");
        integration.ShouldNotContain("tests/Hexalith.EventStore.Server.Tests/");
        integration.ShouldNotContain("--filter \"Category=LiveSidecar\"");
        release.ShouldNotContain("integration.yml");
        release.ShouldNotContain("Hexalith.EventStore.Server.LiveSidecar.Tests");
    }

    [Fact]
    public void Release_workflow_uses_domain_release_with_approved_eventstore_container_only()
    {
        string root = FindRepositoryRoot();
        string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "release.yml"));

        workflow.ShouldContain("uses: Hexalith/Hexalith.Builds/.github/workflows/domain-release.yml@main");
        workflow.ShouldContain("github.sha == github.event.workflow_run.head_sha");
        workflow.ShouldContain("publish-containers: true");
        workflow.ShouldContain("src/Hexalith.EventStore/Hexalith.EventStore.csproj|eventstore");
        workflow.ShouldContain("NUGET_API_KEY: ${{ secrets.NUGET_API_KEY }}");
        workflow.ShouldNotContain("src/Hexalith.EventStore.Admin");
        workflow.ShouldNotContain("samples/");
        workflow.ShouldNotContain("runs-on:");
        workflow.ShouldNotContain("steps:");
    }

    [Fact]
    public void Security_gate_workflows_remain_shared_callers()
    {
        string root = FindRepositoryRoot();

        string codeQl = File.ReadAllText(Path.Combine(root, ".github", "workflows", "codeql.yml"));
        string dependencyReview = File.ReadAllText(Path.Combine(root, ".github", "workflows", "dependency-review.yml"));
        string commitlint = File.ReadAllText(Path.Combine(root, ".github", "workflows", "commitlint.yml"));

        codeQl.ShouldContain("uses: Hexalith/Hexalith.Builds/.github/workflows/codeql.yml@main");
        dependencyReview.ShouldContain("uses: Hexalith/Hexalith.Builds/.github/workflows/dependency-review.yml@main");
        commitlint.ShouldContain("uses: Hexalith/Hexalith.Builds/.github/workflows/commitlint.yml@main");
        commitlint.ShouldContain("pull_request:");
        commitlint.ShouldContain("push:");
    }

    [Fact]
    public void Advisory_tests_workflow_preserves_non_release_blocking_suites()
    {
        string root = FindRepositoryRoot();
        string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "advisory-tests.yml"));
        string release = File.ReadAllText(Path.Combine(root, ".github", "workflows", "release.yml"));

        workflow.ShouldContain("continue-on-error: true");
        workflow.ShouldContain("playwright.ps1 install --with-deps chromium");
        workflow.ShouldContain("tests/Hexalith.EventStore.Admin.UI.E2E");
        workflow.ShouldContain("tests/Hexalith.EventStore.DeferredWorkGovernance.Tests");
        workflow.ShouldContain("tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests");
        release.ShouldNotContain("Advisory Tests");
        release.ShouldNotContain("advisory-tests");
    }

    [Fact]
    public void Test_projects_are_classified_into_release_live_advisory_or_deferred_lanes()
    {
        string root = FindRepositoryRoot();
        string ci = File.ReadAllText(Path.Combine(root, ".github", "workflows", "ci.yml"));
        string integration = File.ReadAllText(Path.Combine(root, ".github", "workflows", "integration.yml"));
        string advisory = File.ReadAllText(Path.Combine(root, ".github", "workflows", "advisory-tests.yml"));
        string docs = File.ReadAllText(Path.Combine(root, "docs", "ci.md"));
        string[] deferredProjects = DeferredTestLaneProjects(docs);

        string[] ignoredProjects = ["tests/Hexalith.EventStore.TestSubscriber"];
        string[] discovered = Directory
            .EnumerateFiles(Path.Combine(root, "tests"), "*.csproj", SearchOption.AllDirectories)
            .Select(path => Path.GetDirectoryName(Path.GetRelativePath(root, path))!.Replace('\\', '/'))
            .Where(project => !ignoredProjects.Contains(project, StringComparer.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

        List<string> unclassified = [];
        foreach (string project in discovered)
        {
            bool classified = ci.Contains(project, StringComparison.Ordinal)
                || integration.Contains(project, StringComparison.Ordinal)
                || advisory.Contains(project, StringComparison.Ordinal)
                || deferredProjects.Contains(project, StringComparer.Ordinal);
            if (!classified)
            {
                unclassified.Add(project);
            }
        }

        unclassified.ShouldBeEmpty("Every test project must be explicitly assigned to a workflow lane or a documented deferred/advisory category.");
    }

    [Fact]
    public void Server_tests_do_not_contain_live_sidecar_markers()
    {
        string root = FindRepositoryRoot();
        string serverTestsRoot = Path.Combine(root, "tests", "Hexalith.EventStore.Server.Tests");

        string[] offenders = Directory
            .EnumerateFiles(serverTestsRoot, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.Ordinal) || path.EndsWith(".csproj", StringComparison.Ordinal))
            .Where(path =>
            {
                string text = File.ReadAllText(path);
                return text.Contains("Category\", \"LiveSidecar", StringComparison.Ordinal)
                    || text.Contains("DaprTestContainer", StringComparison.Ordinal);
            })
            .Select(path => Path.GetRelativePath(root, path))
            .Order(StringComparer.Ordinal)
            .ToArray();

        offenders.ShouldBeEmpty(
            "Live-sidecar tests and fixtures must stay in Hexalith.EventStore.Server.LiveSidecar.Tests so Server.Tests can run unfiltered in the release gate.");
    }

    [Fact]
    public void Shared_ci_package_scripts_exist_and_remain_manifest_backed()
    {
        string root = FindRepositoryRoot();
        string[] scripts =
        [
            "scripts/pack-release-packages.py",
            "scripts/validate-nuget-packages.py",
            "scripts/validate-consumer-package-references.py",
        ];

        foreach (string script in scripts)
        {
            string path = Path.Combine(root, script);
            File.Exists(path).ShouldBeTrue($"{script} must exist for shared domain-ci consumer validation.");

            string text = File.ReadAllText(path);
            text.ShouldContain("tools");
            text.ShouldContain("release-packages.json");
            if (script.Contains("validate", StringComparison.Ordinal))
            {
                text.ShouldContain("package_id == \"Hexalith.EventStore\" or package_id.startswith(\"Hexalith.EventStore.\")");
            }

            text.ShouldNotContain("references/Hexalith.");
            text.Contains("NU1605", StringComparison.Ordinal).ShouldBeFalse(
                "Package-consumer validation must not suppress package downgrade conflicts.");
        }
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

    private static string[] DeferredTestLaneProjects(string docs)
        => docs
            .Split('\n')
            .Where(line => line.Contains('|', StringComparison.Ordinal)
                && line.Contains("Deferred", StringComparison.OrdinalIgnoreCase))
            .SelectMany(line => Regex
                .Matches(line, "`(?<project>tests/[^`]+)`")
                .Select(match => match.Groups["project"].Value))
            .Distinct(StringComparer.Ordinal)
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
