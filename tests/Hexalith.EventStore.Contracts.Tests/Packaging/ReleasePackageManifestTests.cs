using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Hexalith.EventStore.Contracts.Tests.Packaging;

public sealed class ReleasePackageManifestTests
{
    private const string CheckoutActionSha = "9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0";
    private const string DomainServicePackageId = "Hexalith.EventStore.DomainService";
    private const string DomainServiceProjectPath = "src/Hexalith.EventStore.DomainService/Hexalith.EventStore.DomainService.csproj";
    private const int ExpectedManifestPackageCount = 14;
    private const string GeneratorPackageId = "Hexalith.EventStore.RestApi.Generators";
    private const string GeneratorProjectPath = "src/Hexalith.EventStore.RestApi.Generators/Hexalith.EventStore.RestApi.Generators.csproj";
    private const string SemanticReleaseFixture = "tests/Hexalith.EventStore.Contracts.Tests/Packaging/Fixtures/semantic-release-github-success.mjs";
    private const string ServiceDefaultsPackageId = "Hexalith.EventStore.ServiceDefaults";
    private const string ServiceDefaultsProjectPath = "src/Hexalith.EventStore.ServiceDefaults/Hexalith.EventStore.ServiceDefaults.csproj";
    private const string SetupNodeActionSha = "820762786026740c76f36085b0efc47a31fe5020";
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
        string ciJob = ExtractTopLevelWorkflowJobBlock(workflow, "ci");
        string tenantsSourceModeJob = ExtractTopLevelWorkflowJobBlock(workflow, "tenants-source-mode");

        ciJob.ShouldContain("uses: Hexalith/Hexalith.Builds/.github/workflows/domain-ci.yml@main");
        ciJob.ShouldContain("build-timeout-minutes: 40");
        ciJob.ShouldContain("run-consumer-validation: true");
        ciJob.ShouldContain("tests/Hexalith.EventStore.Server.Tests");
        ciJob.ShouldNotContain("tests/Hexalith.EventStore.Server.LiveSidecar.Tests");
        ciJob.ShouldNotContain("run-coverage-gate:");
        ciJob.ShouldNotContain("Category!=LiveSidecar");
        ciJob.ShouldNotContain("runs-on:");
        ciJob.ShouldNotContain("steps:");

        AssertWorkflowJobCannotBeSkippedOrTolerated(ciJob, "Shared deterministic CI");
        AssertTenantsSourceModeJobIsBlocking(tenantsSourceModeJob);
    }

    [Fact]
    public void Semantic_release_governance_job_is_unique_unconditional_and_blocking()
    {
        string root = FindRepositoryRoot();
        string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "ci.yml"));
        string job = ExtractTopLevelWorkflowJobBlock(workflow, "semantic-release-governance");

        AssertSemanticReleaseGovernanceJobIsBlocking(job);
    }

    [Theory]
    [InlineData("job-skip")]
    [InlineData("step-skip")]
    [InlineData("dependency-skip")]
    [InlineData("job-tolerance")]
    [InlineData("step-tolerance")]
    public void Semantic_release_governance_validation_rejects_skip_or_tolerance_mutations(string mutation)
    {
        string job = CreateValidSemanticReleaseGovernanceJobBlock();
        string mutatedJob = mutation switch
        {
            "job-skip" => job.Replace(
                "    runs-on: ubuntu-latest",
                "    if: ${{ false }}\n    runs-on: ubuntu-latest",
                StringComparison.Ordinal),
            "step-skip" => job.Replace(
                "        run: npm ci",
                "        if: ${{ false }}\n        run: npm ci",
                StringComparison.Ordinal),
            "dependency-skip" => job.Replace(
                "    runs-on: ubuntu-latest",
                "    needs: ci\n    runs-on: ubuntu-latest",
                StringComparison.Ordinal),
            "job-tolerance" => job.Replace(
                "    runs-on: ubuntu-latest",
                "    continue-on-error: true\n    runs-on: ubuntu-latest",
                StringComparison.Ordinal),
            "step-tolerance" => job.Replace(
                "        run: npm ci",
                "        continue-on-error: true\n        run: npm ci",
                StringComparison.Ordinal),
            _ => throw new InvalidOperationException($"Unknown mutation: {mutation}"),
        };

        _ = Should.Throw<Shouldly.ShouldAssertException>(
            () => AssertSemanticReleaseGovernanceJobIsBlocking(mutatedJob));
    }

    [Fact]
    public void TenantsSourceModeJobValidationRejectsSkippedJob()
    {
        string job = CreateValidTenantsSourceModeJobBlock()
            .Replace(
                "    runs-on: ubuntu-latest",
                "    if: ${{ false }}\n    runs-on: ubuntu-latest",
                StringComparison.Ordinal);

        _ = Should.Throw<Shouldly.ShouldAssertException>(() => AssertTenantsSourceModeJobIsBlocking(job));
    }

    [Fact]
    public void TenantsSourceModeJobValidationRejectsTokenOnlyMatch()
    {
        string job = CreateValidTenantsSourceModeJobBlock()
            .Replace(
                "      -m:1",
                "      # -m:1\n      -m:10",
                StringComparison.Ordinal);

        _ = Should.Throw<Shouldly.ShouldAssertException>(() => AssertTenantsSourceModeJobIsBlocking(job));
    }

    [Theory]
    [InlineData("\n", true)]
    [InlineData("\r\n", false)]
    public void Workflow_job_block_extraction_handles_line_endings_reordered_siblings_and_final_job(
        string newline,
        bool targetIsLast)
    {
        string[] targetJob =
        [
            "  target:",
            "    uses: owner/repository/.github/workflows/target.yml@main",
        ];
        string[] siblingJob =
        [
            "  sibling:",
            "    runs-on: ubuntu-latest",
            "    steps:",
            "      - run: dotnet test",
        ];
        string[] workflowLines = targetIsLast
            ? ["name: CI", "jobs:", .. siblingJob, .. targetJob]
            : ["name: CI", "jobs:", .. targetJob, .. siblingJob];
        string workflow = string.Join(newline, workflowLines);

        string targetBlock = ExtractTopLevelWorkflowJobBlock(workflow, "target");

        targetBlock.ShouldStartWith("  target:");
        targetBlock.ShouldContain("uses: owner/repository/.github/workflows/target.yml@main");
        targetBlock.ShouldNotContain("runs-on:");
        targetBlock.ShouldNotContain("steps:");
        targetBlock.ShouldNotContain('\r');
    }

    [Theory]
    [InlineData("jobs:\n  sibling:\n    runs-on: ubuntu-latest", "missing")]
    [InlineData("jobs:\n  target:\n    uses: first\n  target:\n    uses: second", "duplicate")]
    public void Workflow_job_block_extraction_fails_closed_when_target_is_missing_or_duplicate(
        string workflow,
        string scenario)
    {
        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => ExtractTopLevelWorkflowJobBlock(workflow, "target"));

        exception.Message.ShouldContain("exactly one top-level 'target' job");
        exception.Message.ShouldContain(scenario);
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
        string releaseJob = ExtractTopLevelWorkflowJobBlock(workflow, "release");

        Match releaseWorkflow = Regex.Match(
            releaseJob,
            @"uses: Hexalith/Hexalith\.Builds/\.github/workflows/domain-release\.yml@(?<sha>[0-9a-f]{40})");
        releaseWorkflow.Success.ShouldBeTrue();
        releaseJob.ShouldContain($"builds-execution-sha: {releaseWorkflow.Groups["sha"].Value}");
        releaseJob.ShouldContain("needs: verify-source");
        releaseJob.ShouldContain("actions: read");
        releaseJob.ShouldContain("environment-name: production");
        releaseJob.ShouldContain("source-branch: main");
        releaseJob.ShouldContain("source-ci-workflow: ci.yml");
        releaseJob.ShouldContain("package-manifest: tools/release-packages.json");
        releaseJob.ShouldContain("publish-containers: true");
        releaseJob.ShouldContain("src/Hexalith.EventStore/Hexalith.EventStore.csproj|eventstore");
        releaseJob.ShouldNotContain("src/Hexalith.EventStore.Admin");
        releaseJob.ShouldNotContain("samples/");
        releaseJob.ShouldNotContain("runs-on:");
        releaseJob.ShouldNotContain("steps:");
        workflow.ShouldContain("  workflow_dispatch:");
        workflow.ShouldNotContain("workflow_run:");
        releaseJob.ShouldContain("NUGET_API_KEY: ${{ secrets.NUGET_API_KEY }}");
        releaseJob.ShouldContain("HEXALITH_ZOT_USERNAME: ${{ secrets.HEXALITH_ZOT_USERNAME }}");
        releaseJob.ShouldContain("HEXALITH_ZOT_API_KEY: ${{ secrets.HEXALITH_ZOT_API_KEY }}");
        releaseJob.ShouldNotContain("secrets: inherit");
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
        commitlint.ShouldContain("types: [opened, synchronize, reopened, edited]");
        commitlint.ShouldContain("pull-request-title: ${{ github.event.pull_request.title }}");
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

    private static string ExtractTopLevelWorkflowJobBlock(string workflow, string jobId)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);

        string normalizedWorkflow = workflow
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        MatchCollection jobsHeaders = Regex.Matches(
            normalizedWorkflow,
            @"(?m)^jobs:[ \t]*(?:#.*)?$");

        if (jobsHeaders.Count != 1)
        {
            throw new InvalidOperationException(
                $"Workflow must contain exactly one top-level 'jobs' section; found {jobsHeaders.Count}.");
        }

        int jobsContentStart = normalizedWorkflow.IndexOf('\n', jobsHeaders[0].Index + jobsHeaders[0].Length);
        jobsContentStart = jobsContentStart < 0 ? normalizedWorkflow.Length : jobsContentStart + 1;

        Match followingTopLevelSection = Regex.Match(
            normalizedWorkflow[jobsContentStart..],
            @"(?m)^[A-Za-z0-9_-]+:[^\n]*$");
        int jobsContentEnd = followingTopLevelSection.Success
            ? jobsContentStart + followingTopLevelSection.Index
            : normalizedWorkflow.Length;
        string jobsContent = normalizedWorkflow[jobsContentStart..jobsContentEnd];
        Match[] jobHeaders = Regex
            .Matches(jobsContent, @"(?m)^  (?<id>[A-Za-z0-9_-]+):[ \t]*(?:#.*)?$")
            .Cast<Match>()
            .ToArray();
        Match[] matchingHeaders = jobHeaders
            .Where(match => string.Equals(match.Groups["id"].Value, jobId, StringComparison.Ordinal))
            .ToArray();

        if (matchingHeaders.Length != 1)
        {
            string scenario = matchingHeaders.Length == 0 ? "missing" : "duplicate";
            throw new InvalidOperationException(
                $"Workflow must contain exactly one top-level '{jobId}' job; target is {scenario} (found {matchingHeaders.Length}).");
        }

        Match targetHeader = matchingHeaders[0];
        int followingJobIndex = Array.FindIndex(jobHeaders, match => match.Index > targetHeader.Index);
        int jobEnd = followingJobIndex >= 0
            ? jobHeaders[followingJobIndex].Index
            : jobsContent.Length;

        return jobsContent[targetHeader.Index..jobEnd].TrimEnd('\n');
    }

    private static void AssertTenantsSourceModeJobIsBlocking(string jobBlock)
    {
        string[] lines = jobBlock
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
        string[] trimmedLines = lines.Select(static line => line.Trim()).ToArray();

        lines.ShouldContain("    runs-on: ubuntu-latest");
        lines.ShouldContain("      UseHexalithProjectReferences: 'true'");
        trimmedLines.ShouldContain("--configuration Debug");
        trimmedLines.ShouldContain("-m:1");
        lines.ShouldContain("      - name: Verify Tenants source-mode topology guardrails");
        trimmedLines.ShouldContain("--filter FullyQualifiedName~TenantsApiLaunchSettingsTests");
        AssertWorkflowJobCannotBeSkippedOrTolerated(jobBlock, "Tenants source-mode");
    }

    private static void AssertSemanticReleaseGovernanceJobIsBlocking(string jobBlock)
    {
        string normalizedJob = jobBlock
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        string[] lines = normalizedJob.Split('\n');
        string[] trimmedLines = lines.Select(static line => line.Trim()).ToArray();
        Match[] checkoutActions = Regex
            .Matches(
                normalizedJob,
                @"(?m)^      - uses: actions/checkout@(?<sha>[0-9a-f]{40})(?: # .*)?$")
            .Cast<Match>()
            .ToArray();
        Match[] setupNodeActions = Regex
            .Matches(
                normalizedJob,
                @"(?m)^        uses: actions/setup-node@(?<sha>[0-9a-f]{40})(?: # .*)?$")
            .Cast<Match>()
            .ToArray();

        lines.ShouldContain("  semantic-release-governance:");
        lines.ShouldContain("    runs-on: ubuntu-latest");
        lines.ShouldContain("    timeout-minutes: 10");
        checkoutActions.Length.ShouldBe(1);
        checkoutActions[0].Groups["sha"].Value.ShouldBe(CheckoutActionSha);
        setupNodeActions.Length.ShouldBe(1);
        setupNodeActions[0].Groups["sha"].Value.ShouldBe(SetupNodeActionSha);
        lines.ShouldContain("          node-version: '22'");
        lines.ShouldContain("          cache: npm");
        trimmedLines.ShouldContain("run: npm ci");
        trimmedLines.ShouldContain($"run: node {SemanticReleaseFixture}");
        AssertWorkflowJobCannotBeSkippedOrTolerated(jobBlock, "Semantic-release governance");

        int checkout = normalizedJob.IndexOf("uses: actions/checkout@", StringComparison.Ordinal);
        int setupNode = normalizedJob.IndexOf("uses: actions/setup-node@", StringComparison.Ordinal);
        int npmInstall = normalizedJob.IndexOf("run: npm ci", StringComparison.Ordinal);
        int fixture = normalizedJob.IndexOf($"run: node {SemanticReleaseFixture}", StringComparison.Ordinal);
        checkout.ShouldBeLessThan(setupNode);
        setupNode.ShouldBeLessThan(npmInstall);
        npmInstall.ShouldBeLessThan(fixture);
    }

    private static void AssertWorkflowJobCannotBeSkippedOrTolerated(string jobBlock, string jobName)
    {
        string[] lines = jobBlock
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

        lines.Any(static line => line.TrimStart().StartsWith("continue-on-error:", StringComparison.Ordinal))
            .ShouldBeFalse($"The blocking {jobName} job must not tolerate failures.");
        lines.Any(static line => line.TrimStart().StartsWith("if:", StringComparison.Ordinal))
            .ShouldBeFalse($"The blocking {jobName} job and its required steps must not be conditionally skipped.");
        lines.Any(static line => line.TrimStart().StartsWith("needs:", StringComparison.Ordinal))
            .ShouldBeFalse($"The unconditional {jobName} job must not depend on another job's outcome.");
    }

    private static string CreateValidTenantsSourceModeJobBlock()
        => string.Join(
            '\n',
            [
                "  tenants-source-mode:",
                "    runs-on: ubuntu-latest",
                "    env:",
                "      UseHexalithProjectReferences: 'true'",
                "    steps:",
                "      - name: Build source-mode AppHost tests",
                "        run: >-",
                "          dotnet build tests/Hexalith.EventStore.AppHost.Tests/Hexalith.EventStore.AppHost.Tests.csproj",
                "          --configuration Debug",
                "          -m:1",
                "      - name: Verify Tenants source-mode topology guardrails",
                "        run: >-",
                "          dotnet test tests/Hexalith.EventStore.AppHost.Tests/Hexalith.EventStore.AppHost.Tests.csproj",
                "          --filter FullyQualifiedName~TenantsApiLaunchSettingsTests",
            ]);

    private static string CreateValidSemanticReleaseGovernanceJobBlock()
        => string.Join(
            '\n',
            [
                "  semantic-release-governance:",
                "    runs-on: ubuntu-latest",
                "    timeout-minutes: 10",
                "    steps:",
                $"      - uses: actions/checkout@{CheckoutActionSha} # v7.0.0",
                "      - name: Set up supported Node",
                $"        uses: actions/setup-node@{SetupNodeActionSha} # v7.0.0",
                "        with:",
                "          node-version: '22'",
                "          cache: npm",
                "      - name: Install locked npm dependencies",
                "        run: npm ci",
                "      - name: Verify semantic-release GitHub success lifecycle",
                $"        run: node {SemanticReleaseFixture}",
            ]);

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
