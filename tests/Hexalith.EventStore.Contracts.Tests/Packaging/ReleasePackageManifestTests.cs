using System.Diagnostics;
using System.IO.Compression;
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
    private const string GatewayPackageId = "Hexalith.EventStore.Gateway";
    private const string PackageFixtureVersion = "999.3.6-fixture";
    private const string SemanticReleaseFixture = "tests/Hexalith.EventStore.Contracts.Tests/Packaging/Fixtures/semantic-release-github-success.mjs";
    private const string ServiceDefaultsPackageId = "Hexalith.EventStore.ServiceDefaults";
    private const string ServiceDefaultsProjectPath = "src/Hexalith.EventStore.ServiceDefaults/Hexalith.EventStore.ServiceDefaults.csproj";
    private const string SetupNodeActionSha = "820762786026740c76f36085b0efc47a31fe5020";
    private const string ToolPackageId = "Hexalith.EventStore.Admin.Cli";

    /// <summary>
    /// Loads the shared contract and exercises its manifest normalizer against an
    /// injected manifest and repository root, which no CLI entry point exposes.
    /// </summary>
    private const string ManifestLoaderProbe = """
        import pathlib, sys
        sys.path.insert(0, 'tools')
        from release_package_contract import load_release_manifest
        try:
            load_release_manifest(pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2]))
        except Exception as error:
            print(error, file=sys.stderr)
            raise SystemExit(1)
        """;

    private static readonly string[] GatewayRequiredDependencies =
    [
        "Hexalith.EventStore.Admin.Abstractions",
        "Hexalith.EventStore.Contracts",
        "Hexalith.EventStore.Server",
        "Hexalith.EventStore.ServiceDefaults",
    ];
    private static readonly TimeSpan MsBuildPropertyTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// The packer evaluates every manifest project with MSBuild before printing any
    /// command, so it needs a whole-inventory budget rather than the single-property one.
    /// </summary>
    private static readonly TimeSpan ManifestEvaluationTimeout = TimeSpan.FromMinutes(5);

    private static readonly XNamespace NuspecNamespace =
        "http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd";

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
            string projectPath = Path.Combine(root, package.Project);
            File.Exists(projectPath).ShouldBeTrue(
                $"Release package project must exist: {package.Project}");
            Path.GetFullPath(projectPath).StartsWith(
                Path.Combine(root, "src") + Path.DirectorySeparatorChar,
                StringComparison.Ordinal).ShouldBeTrue(
                $"Release package project must be owned by the root src directory: {package.Project}");
            EvaluatedProjectProperty(projectPath, "IsPackable").ShouldBe(
                "true",
                $"Release package project must evaluate as packable: {package.Project}");
            EvaluatedProjectProperty(projectPath, "PackageId").ShouldBe(
                package.Id,
                $"Release package project identity must match its manifest entry: {package.Project}");
        }
    }

    [Fact]
    public void Non_manifest_src_projects_cannot_produce_release_packages()
    {
        string root = FindRepositoryRoot();
        HashSet<string> manifestProjects = LoadReleasePackages()
            .Select(package => Path.GetFullPath(Path.Combine(root, package.Project)))
            .ToHashSet(StringComparer.Ordinal);

        string[] unlistedProjects = Directory
            .GetFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories)
            .Where(project => !manifestProjects.Contains(Path.GetFullPath(project)))
            .ToArray();

        unlistedProjects.ShouldNotBeEmpty(
            "The complement must be non-empty, otherwise this assertion proves nothing.");

        // Directory.Build.props defaults IsPackable to true, so a new src project is
        // packable unless it opts out. Without this the manifest only proves
        // manifest => packable, never packable => manifest.
        foreach (string project in unlistedProjects)
        {
            File.ReadAllText(project)
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .ShouldContain(
                    "<IsPackable>false</IsPackable>",
                    Case.Sensitive,
                    $"A src project outside tools/release-packages.json must be explicitly unpackable: "
                    + Path.GetRelativePath(root, project));
        }
    }

    [Theory]
    [InlineData("empty-packages", "non-empty 'packages' array")]
    [InlineData("non-object-entry", "must be an object")]
    [InlineData("missing-fields", "string 'id' and 'project' values")]
    [InlineData("foreign-scope", "outside EventStore scope")]
    [InlineData("unnormalized-project", "must be normalized as")]
    [InlineData("traversal-project", "not a normalized relative .csproj path")]
    [InlineData("outside-src", "outside the root-owned src directory")]
    [InlineData("missing-project", "does not exist")]
    [InlineData("duplicate-id", "Duplicate package id")]
    [InlineData("duplicate-project", "Duplicate project")]
    public void Manifest_loader_fails_closed_before_packing(string mutation, string expectedDiagnostic)
    {
        string sandbox = Directory.CreateTempSubdirectory("eventstore-manifest-mutation-").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(sandbox, "src", "First"));
            Directory.CreateDirectory(Path.Combine(sandbox, "src", "Second"));
            Directory.CreateDirectory(Path.Combine(sandbox, "outside"));
            File.WriteAllText(Path.Combine(sandbox, "src", "First", "First.csproj"), "<Project />");
            File.WriteAllText(Path.Combine(sandbox, "src", "Second", "Second.csproj"), "<Project />");
            File.WriteAllText(Path.Combine(sandbox, "outside", "Other.csproj"), "<Project />");

            string manifestPath = Path.Combine(sandbox, "release-packages.json");
            File.WriteAllText(manifestPath, MutatedManifestJson(mutation));

            (int exitCode, _, string error) = RunPythonScript(
                "-c",
                ManifestLoaderProbe,
                manifestPath,
                sandbox);

            exitCode.ShouldBe(1, $"The '{mutation}' manifest must be rejected before any pack command.");
            error.ShouldContain(expectedDiagnostic);
        }
        finally
        {
            Directory.Delete(sandbox, recursive: true);
        }
    }

    [Fact]
    public void Manifest_loader_accepts_the_normalized_control_manifest()
    {
        string sandbox = Directory.CreateTempSubdirectory("eventstore-manifest-control-").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(sandbox, "src", "First"));
            Directory.CreateDirectory(Path.Combine(sandbox, "src", "Second"));
            File.WriteAllText(Path.Combine(sandbox, "src", "First", "First.csproj"), "<Project />");
            File.WriteAllText(Path.Combine(sandbox, "src", "Second", "Second.csproj"), "<Project />");

            string manifestPath = Path.Combine(sandbox, "release-packages.json");
            File.WriteAllText(manifestPath, MutatedManifestJson("valid"));

            (int exitCode, _, string error) = RunPythonScript(
                "-c",
                ManifestLoaderProbe,
                manifestPath,
                sandbox);

            exitCode.ShouldBe(0, error);
        }
        finally
        {
            Directory.Delete(sandbox, recursive: true);
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
        prepareCommand.IndexOf("tools/pack-release-packages.py", StringComparison.Ordinal).ShouldBeLessThan(
            prepareCommand.IndexOf("tools/validate-release-packages.py", StringComparison.Ordinal),
            "Packing must precede validation; the GitHub asset glob is unscoped, so the fail-closed "
            + "validator running over the packed output is what keeps release assets manifest-only.");

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
        publishCommand.ShouldContain("dotnet nuget push \"./nupkgs/Hexalith.EventStore.*.nupkg\"");
        publishCommand.ShouldNotContain("dotnet nuget push \"./nupkgs/*.nupkg\"");
        Regex.Matches(
            publishCommand,
            "\\\"\\./nupkgs/Hexalith\\.EventStore\\.\\*\\.nupkg\\\"",
            RegexOptions.CultureInvariant).Count.ShouldBe(
                1,
                "Semantic-release must expose exactly one EventStore-scoped NuGet publication glob.");
        publishCommand.ShouldContain("./.hexalith/release/publish-containers.sh ${nextRelease.version}");
        publishCommand.IndexOf("scripts/validate-release-secrets.sh", StringComparison.Ordinal).ShouldBeLessThan(
            publishCommand.IndexOf("dotnet nuget push", StringComparison.Ordinal),
            "Release secrets must be validated before any irreversible NuGet publish command runs.");
    }

    [Fact]
    public void ManifestPackerDryRunEmitsExactReleasePackageCommands()
    {
        ReleasePackage[] packages = LoadReleasePackages();
        string packageDirectory = Path.Combine(
            Path.GetTempPath(),
            $"eventstore-release-manifest-test-{Guid.NewGuid():N}");

        (int exitCode, string output, string error) = RunPythonScript(
            "tools/pack-release-packages.py",
            ManifestEvaluationTimeout,
            packageDirectory,
            PackageFixtureVersion,
            "--dry-run");

        exitCode.ShouldBe(0, error);
        string[] commands = output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.StartsWith("dotnet pack ", StringComparison.Ordinal))
            .ToArray();
        commands.Length.ShouldBe(ExpectedManifestPackageCount);
        commands.Select((command, index) => (command, package: packages[index])).ToList().ForEach(item =>
        {
            item.command.ShouldStartWith($"dotnet pack {item.package.Project} ");
            Regex.Matches(item.command, "--configuration Release", RegexOptions.CultureInvariant).Count.ShouldBe(1);
            Regex.Matches(item.command, "-p:GeneratePackageOnBuild=false", RegexOptions.CultureInvariant).Count.ShouldBe(1);
            Regex.Matches(item.command, "-p:UseHexalithProjectReferences=false", RegexOptions.CultureInvariant).Count.ShouldBe(1);
            Regex.Matches(item.command, $"-p:Version={PackageFixtureVersion}", RegexOptions.CultureInvariant).Count.ShouldBe(1);
        });

        Directory.Exists(packageDirectory).ShouldBeFalse();
    }

    [Theory]
    [InlineData("tools/validate-release-packages.py")]
    [InlineData("scripts/validate-nuget-packages.py")]
    public void ArchiveValidatorsAcceptExactEmbeddedInventory(string validator)
    {
        ArgumentNullException.ThrowIfNull(validator);
        string packageDirectory = Directory.CreateTempSubdirectory("eventstore-valid-packages-").FullName;
        try
        {
            CreateSyntheticReleaseOutput(packageDirectory, "valid");

            (int exitCode, _, string error) = RunPackageValidator(validator, packageDirectory);

            exitCode.ShouldBe(0, error);
        }
        finally
        {
            Directory.Delete(packageDirectory, recursive: true);
        }
    }

    [Theory]
    [InlineData("tools/validate-release-packages.py", "missing", "Missing release packages")]
    [InlineData("scripts/validate-nuget-packages.py", "missing", "Missing release packages")]
    [InlineData("tools/validate-release-packages.py", "extra", "Unexpected release packages")]
    [InlineData("scripts/validate-nuget-packages.py", "extra", "Unexpected release packages")]
    [InlineData("tools/validate-release-packages.py", "foreign", "Unexpected release packages")]
    [InlineData("scripts/validate-nuget-packages.py", "foreign", "Unexpected release packages")]
    [InlineData("tools/validate-release-packages.py", "renamed", "Renamed package archive")]
    [InlineData("scripts/validate-nuget-packages.py", "renamed", "Renamed package archive")]
    [InlineData("tools/validate-release-packages.py", "duplicate", "Duplicate package output")]
    [InlineData("scripts/validate-nuget-packages.py", "duplicate", "Duplicate package output")]
    [InlineData("tools/validate-release-packages.py", "mixed", "embedded version")]
    [InlineData("scripts/validate-nuget-packages.py", "mixed", "must share one version")]
    [InlineData("tools/validate-release-packages.py", "case", "canonical manifest casing")]
    [InlineData("scripts/validate-nuget-packages.py", "case", "canonical manifest casing")]
    [InlineData("tools/validate-release-packages.py", "archive-extension-case", "Renamed package archive")]
    [InlineData("scripts/validate-nuget-packages.py", "archive-extension-case", "Renamed package archive")]
    public void ArchiveValidatorsRejectInventoryAndIdentityMutations(
        string validator,
        string mutation,
        string expectedError)
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(mutation);
        ArgumentNullException.ThrowIfNull(expectedError);
        string packageDirectory = Directory.CreateTempSubdirectory("eventstore-invalid-packages-").FullName;
        try
        {
            CreateSyntheticReleaseOutput(packageDirectory, mutation);

            (int exitCode, _, string error) = RunPackageValidator(validator, packageDirectory);

            exitCode.ShouldNotBe(0);
            error.ShouldContain(expectedError);
        }
        finally
        {
            Directory.Delete(packageDirectory, recursive: true);
        }
    }

    [Theory]
    [InlineData("tools/validate-release-packages.py", "metadata-leak", "local project or source-path metadata")]
    [InlineData("scripts/validate-nuget-packages.py", "metadata-leak", "local project or source-path metadata")]
    [InlineData("tools/validate-release-packages.py", "element-metadata-leak", "local project or source-path metadata")]
    [InlineData("scripts/validate-nuget-packages.py", "element-metadata-leak", "local project or source-path metadata")]
    [InlineData("tools/validate-release-packages.py", "archive-project-entry", "local or unsafe archive path")]
    [InlineData("scripts/validate-nuget-packages.py", "archive-project-entry", "local or unsafe archive path")]
    [InlineData("tools/validate-release-packages.py", "archive-traversal-entry", "local or unsafe archive path")]
    [InlineData("scripts/validate-nuget-packages.py", "archive-traversal-entry", "local or unsafe archive path")]
    [InlineData("tools/validate-release-packages.py", "archive-drive-relative-entry", "local or unsafe archive path")]
    [InlineData("scripts/validate-nuget-packages.py", "archive-drive-relative-entry", "local or unsafe archive path")]
    [InlineData("tools/validate-release-packages.py", "output-path-metadata-leak", "local project or source-path metadata")]
    [InlineData("scripts/validate-nuget-packages.py", "output-path-metadata-leak", "local project or source-path metadata")]
    [InlineData("tools/validate-release-packages.py", "sibling-metadata-leak", "local project or source-path metadata")]
    [InlineData("scripts/validate-nuget-packages.py", "sibling-metadata-leak", "local project or source-path metadata")]
    [InlineData("tools/validate-release-packages.py", "tool-type-forgery", "declares the DotnetTool package type")]
    [InlineData("scripts/validate-nuget-packages.py", "tool-type-forgery", "declares the DotnetTool package type")]
    [InlineData("tools/validate-release-packages.py", "tool-type-missing", "does not declare the DotnetTool package type")]
    [InlineData("scripts/validate-nuget-packages.py", "tool-type-missing", "does not declare the DotnetTool package type")]
    [InlineData("tools/validate-release-packages.py", "gateway-dependency-loss", "internal dependency contract failed")]
    [InlineData("scripts/validate-nuget-packages.py", "gateway-dependency-loss", "internal dependency contract failed")]
    [InlineData("tools/validate-release-packages.py", "client-dependency-loss", "internal dependency contract failed")]
    [InlineData("scripts/validate-nuget-packages.py", "client-dependency-loss", "internal dependency contract failed")]
    [InlineData("tools/validate-release-packages.py", "external-dependency-loss", "external Hexalith dependency contract failed")]
    [InlineData("scripts/validate-nuget-packages.py", "external-dependency-loss", "external Hexalith dependency contract failed")]
    [InlineData("tools/validate-release-packages.py", "gateway-dependency-version-drift", "expected version")]
    [InlineData("scripts/validate-nuget-packages.py", "gateway-dependency-version-drift", "expected version")]
    [InlineData("tools/validate-release-packages.py", "duplicate-dependency", "duplicate dependency")]
    [InlineData("scripts/validate-nuget-packages.py", "duplicate-dependency", "duplicate dependency")]
    [InlineData("tools/validate-release-packages.py", "ungrouped-duplicate-dependency", "duplicate dependency")]
    [InlineData("scripts/validate-nuget-packages.py", "ungrouped-duplicate-dependency", "duplicate dependency")]
    public void ArchiveValidatorsRejectSourceMetadataAndGatewayDependencyLoss(
        string validator,
        string mutation,
        string expectedError)
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(mutation);
        ArgumentNullException.ThrowIfNull(expectedError);
        string packageDirectory = Directory.CreateTempSubdirectory("eventstore-metadata-packages-").FullName;
        try
        {
            CreateSyntheticReleaseOutput(packageDirectory, mutation);

            (int exitCode, _, string error) = RunPackageValidator(validator, packageDirectory);

            exitCode.ShouldNotBe(0);
            error.ShouldContain(expectedError);
        }
        finally
        {
            Directory.Delete(packageDirectory, recursive: true);
        }
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

        AssertLiveSidecarWorkflowTargetsLiveProjectOutsideReleaseGate(integration, release);
    }

    [Theory]
    [InlineData("full-server-tests")]
    [InlineData("category-filter")]
    [InlineData("missing-cli-tag")]
    [InlineData("release-coupling-workflow")]
    [InlineData("release-coupling-project")]
    public void Live_sidecar_workflow_guardrail_rejects_forbidden_mutations(string mutation)
    {
        string root = FindRepositoryRoot();
        string integration = File.ReadAllText(Path.Combine(root, ".github", "workflows", "integration.yml"));
        string release = File.ReadAllText(Path.Combine(root, ".github", "workflows", "release.yml"));

        string mutatedIntegration = mutation switch
        {
            "full-server-tests" => integration + "\n          dotnet test tests/Hexalith.EventStore.Server.Tests/\n",
            "category-filter" => integration + "\n          --filter \"Category=LiveSidecar\"\n",
            "missing-cli-tag" => integration.Replace(
                "DAPR_VERSION: '1.18.0'",
                "DAPR_VERSION: '1.18.1'",
                StringComparison.Ordinal),
            "release-coupling-workflow" => integration,
            "release-coupling-project" => integration,
            _ => throw new InvalidOperationException($"Unknown mutation: {mutation}"),
        };
        string mutatedRelease = mutation switch
        {
            "release-coupling-workflow" => release + "\n# uses integration.yml\n",
            "release-coupling-project" => release + "\n# Hexalith.EventStore.Server.LiveSidecar.Tests\n",
            _ => release,
        };

        if (mutation is "missing-cli-tag")
        {
            mutatedIntegration.ShouldNotBe(
                integration,
                "Replace mutation must change the integration workflow text before the guardrail is evaluated.");
        }

        _ = Should.Throw<Shouldly.ShouldAssertException>(
            () => AssertLiveSidecarWorkflowTargetsLiveProjectOutsideReleaseGate(mutatedIntegration, mutatedRelease));
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
            if (script.Contains("validate", StringComparison.Ordinal))
            {
                // Manifest backing is inherited from the shared contract, which is asserted
                // below; requiring the literal filename here would only reward a dead constant.
                text.ShouldContain("release_package_contract");
            }
            else
            {
                text.ShouldContain("release-packages.json");
            }

            text.ShouldNotContain("references/Hexalith.");
            text.Contains("NU1605", StringComparison.Ordinal).ShouldBeFalse(
                "Package-consumer validation must not suppress package downgrade conflicts.");
        }

        string sharedContract = File.ReadAllText(Path.Combine(root, "tools", "release_package_contract.py"));
        sharedContract.ShouldContain("release-packages.json");
        sharedContract.ShouldContain("package_id == \"Hexalith.EventStore\"");
        sharedContract.ShouldContain("package_id.startswith(EVENTSTORE_PACKAGE_PREFIX)");
        sharedContract.ShouldNotContain("references/Hexalith.");
        sharedContract.Contains("NU1605", StringComparison.Ordinal).ShouldBeFalse(
            "The shared release contract must not suppress package downgrade conflicts.");
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

    private static void CreateSyntheticReleaseOutput(string packageDirectory, string mutation)
    {
        ReleasePackage[] packages = LoadReleasePackages();
        for (int index = 0; index < packages.Length; index++)
        {
            ReleasePackage package = packages[index];
            if (mutation == "missing" && index == packages.Length - 1)
            {
                continue;
            }

            string version = mutation == "mixed" && index == 0
                ? "999.3.7-fixture"
                : PackageFixtureVersion;
            string embeddedId = mutation == "case" && index == 0
                ? package.Id.ToLowerInvariant()
                : package.Id;
            string archiveName = mutation == "renamed" && index == 0
                ? "renamed-package.nupkg"
                : $"{embeddedId}.{version}.{(mutation == "archive-extension-case" && index == 0 ? "NUPKG" : "nupkg")}";
            string[] dependencies =
            [
                .. ExpectedInternalDependencies(package.Id),
                .. ExpectedExternalHexalithDependencies(package.Id),
            ];
            if (mutation == "gateway-dependency-loss" && package.Id == GatewayPackageId)
            {
                dependencies = GatewayRequiredDependencies[..^1];
            }
            else if (mutation == "client-dependency-loss" && package.Id == "Hexalith.EventStore.Client")
            {
                dependencies = [];
            }
            else if (mutation == "external-dependency-loss" && package.Id == "Hexalith.EventStore.Contracts")
            {
                dependencies = ExpectedInternalDependencies(package.Id);
            }
            else if (mutation == "tool-type-forgery" && package.Id == GatewayPackageId)
            {
                // A library that declares itself a tool must not thereby waive its
                // dependency proof, so the forged archive also drops every edge.
                dependencies = [];
            }

            string dependencyVersion = mutation == "gateway-dependency-version-drift" && package.Id == GatewayPackageId
                ? "999.3.5-fixture"
                : version;

            WriteSyntheticPackage(
                Path.Combine(packageDirectory, archiveName),
                embeddedId,
                version,
                dependencies,
                dependencyVersion,
                new SyntheticPackageOptions
                {
                    IncludeAttributeMetadataLeak = mutation == "metadata-leak" && index == 0,
                    IncludeElementMetadataLeak = mutation == "element-metadata-leak" && index == 0,
                    IncludeOutputPathMetadataLeak = mutation == "output-path-metadata-leak" && index == 0,
                    IncludeSiblingMetadataLeak = mutation == "sibling-metadata-leak" && index == 0,
                    ForgeToolPackageType = mutation == "tool-type-forgery" && package.Id == GatewayPackageId,
                    OmitToolPackageType = mutation == "tool-type-missing" && package.Id == ToolPackageId,
                    DuplicateDependencies =
                        (mutation is "duplicate-dependency" or "ungrouped-duplicate-dependency")
                        && package.Id == GatewayPackageId,
                    UngroupedDependencies =
                        mutation == "ungrouped-duplicate-dependency" && package.Id == GatewayPackageId,
                    ExtraArchiveEntries = index != 0
                        ? []
                        : mutation switch
                        {
                            "archive-project-entry" => ["src/Leaked/Leaked.csproj"],
                            "archive-traversal-entry" => ["../escaped-from-package.txt"],
                            "archive-drive-relative-entry" => ["C:leaked-from-package.txt"],
                            _ => [],
                        },
                });
        }

        if (mutation == "extra")
        {
            WriteSyntheticPackage(
                Path.Combine(packageDirectory, $"Hexalith.EventStore.Unlisted.{PackageFixtureVersion}.nupkg"),
                "Hexalith.EventStore.Unlisted",
                PackageFixtureVersion,
                [],
                PackageFixtureVersion);
        }
        else if (mutation == "foreign")
        {
            WriteSyntheticPackage(
                Path.Combine(packageDirectory, $"Hexalith.Commons.Foreign.{PackageFixtureVersion}.nupkg"),
                "Hexalith.Commons.Foreign",
                PackageFixtureVersion,
                [],
                PackageFixtureVersion);
        }
        else if (mutation == "duplicate")
        {
            WriteSyntheticPackage(
                Path.Combine(packageDirectory, "ZZZ-duplicate.nupkg"),
                packages[0].Id,
                PackageFixtureVersion,
                [],
                PackageFixtureVersion);
        }
    }

    private static void WriteSyntheticPackage(
        string archivePath,
        string packageId,
        string version,
        IEnumerable<string> dependencies,
        string dependencyVersion,
        SyntheticPackageOptions? options = null)
    {
        SyntheticPackageOptions fixture = options ?? new SyntheticPackageOptions();
        string[] declaredDependencies = fixture.DuplicateDependencies
            ? [.. dependencies, .. dependencies]
            : [.. dependencies];
        IEnumerable<XElement> dependencyElements = declaredDependencies.Select(dependency => new XElement(
            NuspecNamespace + "dependency",
            new XAttribute("id", dependency),
            new XAttribute("version", dependencyVersion)));

        // Real `dotnet pack` output always groups dependencies by target framework;
        // the ungrouped shape is a nuspec-legal alternative the parser must also police.
        XElement dependenciesElement = fixture.UngroupedDependencies
            ? new XElement(NuspecNamespace + "dependencies", dependencyElements)
            : new XElement(
                NuspecNamespace + "dependencies",
                new XElement(
                    NuspecNamespace + "group",
                    new XAttribute("targetFramework", "net10.0"),
                    dependencyElements));

        XElement metadata = new(
            NuspecNamespace + "metadata",
            new XElement(NuspecNamespace + "id", packageId),
            new XElement(NuspecNamespace + "version", version),
            new XElement(NuspecNamespace + "authors", "Hexalith Tests"),
            new XElement(NuspecNamespace + "description", "Synthetic release contract fixture"),
            new XElement(NuspecNamespace + "projectUrl", "https://github.com/Hexalith/Hexalith.EventStore"),
            dependenciesElement);
        bool declaresToolPackageType = fixture.ForgeToolPackageType
            || (packageId == ToolPackageId && !fixture.OmitToolPackageType);
        if (declaresToolPackageType)
        {
            metadata.Add(
                new XElement(
                    NuspecNamespace + "packageTypes",
                    new XElement(
                        NuspecNamespace + "packageType",
                        new XAttribute("name", "DotnetTool"),
                        new XAttribute("version", "1.0.0"))));
        }

        if (fixture.IncludeAttributeMetadataLeak)
        {
            metadata.Add(new XElement(
                NuspecNamespace + "repository",
                new XAttribute("url", "src/LocalProject.csproj")));
        }

        if (fixture.IncludeElementMetadataLeak)
        {
            metadata.Add(new XElement(
                NuspecNamespace + "icon",
                "../../artifacts/checkout/icon.png"));
        }

        if (fixture.IncludeOutputPathMetadataLeak)
        {
            // Unrooted build-output paths are the shape a real pack leak carries;
            // the rooted and traversal alternatives never see them.
            metadata.Add(new XElement(
                NuspecNamespace + "icon",
                "bin/Release/net10.0/icon.png"));
        }

        XElement packageElement = new(NuspecNamespace + "package", metadata);
        if (fixture.IncludeSiblingMetadataLeak)
        {
            // A leak outside <metadata> is still shipped nuspec content.
            packageElement.Add(new XElement(
                NuspecNamespace + "files",
                new XElement(
                    NuspecNamespace + "file",
                    new XAttribute("src", "src/LocalProject.csproj"))));
        }

        XDocument nuspec = new(packageElement);
        using ZipArchive archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
        foreach (string extraEntry in fixture.ExtraArchiveEntries)
        {
            archive.CreateEntry(extraEntry);
        }

        ZipArchiveEntry entry = archive.CreateEntry($"{packageId}.nuspec");
        using Stream stream = entry.Open();
        nuspec.Save(stream);
    }

    private sealed record SyntheticPackageOptions
    {
        public bool IncludeAttributeMetadataLeak { get; init; }

        public bool IncludeElementMetadataLeak { get; init; }

        public bool IncludeOutputPathMetadataLeak { get; init; }

        public bool IncludeSiblingMetadataLeak { get; init; }

        public bool ForgeToolPackageType { get; init; }

        public bool OmitToolPackageType { get; init; }

        public bool DuplicateDependencies { get; init; }

        public bool UngroupedDependencies { get; init; }

        public string[] ExtraArchiveEntries { get; init; } = [];
    }

    private static string MutatedManifestJson(string mutation)
    {
        const string first = "Hexalith.EventStore.First";
        const string second = "Hexalith.EventStore.Second";
        const string firstProject = "src/First/First.csproj";
        const string secondProject = "src/Second/Second.csproj";

        string entries = mutation switch
        {
            "empty-packages" => string.Empty,
            "non-object-entry" => "\"Hexalith.EventStore.First\"",
            "missing-fields" => $$"""{"id": "{{first}}"}""",
            "foreign-scope" => $$"""{"id": "Hexalith.Commons.Foreign", "project": "{{firstProject}}"}""",
            "unnormalized-project" => $$"""{"id": "{{first}}", "project": "./{{firstProject}}"}""",
            "traversal-project" => $$"""{"id": "{{first}}", "project": "src/../outside/Other.csproj"}""",
            "outside-src" => $$"""{"id": "{{first}}", "project": "outside/Other.csproj"}""",
            "missing-project" => $$"""{"id": "{{first}}", "project": "src/Absent/Absent.csproj"}""",
            "duplicate-id" =>
                $$"""{"id": "{{first}}", "project": "{{firstProject}}"}, {"id": "{{first}}", "project": "{{secondProject}}"}""",
            "duplicate-project" =>
                $$"""{"id": "{{first}}", "project": "{{firstProject}}"}, {"id": "{{second}}", "project": "{{firstProject}}"}""",
            _ =>
                $$"""{"id": "{{first}}", "project": "{{firstProject}}"}, {"id": "{{second}}", "project": "{{secondProject}}"}""",
        };

        return $$"""{"packages": [{{entries}}]}""";
    }

    private static string[] ExpectedInternalDependencies(string packageId)
        => packageId switch
        {
            "Hexalith.EventStore.Admin.Abstractions" => ["Hexalith.EventStore.Contracts"],
            "Hexalith.EventStore.Admin.Server" =>
            [
                "Hexalith.EventStore.Admin.Abstractions",
                "Hexalith.EventStore.Contracts",
            ],
            "Hexalith.EventStore.Client" => ["Hexalith.EventStore.Contracts"],
            "Hexalith.EventStore.DomainService" =>
            [
                "Hexalith.EventStore.Client",
                "Hexalith.EventStore.ServiceDefaults",
            ],
            GatewayPackageId => GatewayRequiredDependencies,
            "Hexalith.EventStore.Server" =>
            [
                "Hexalith.EventStore.Client",
                "Hexalith.EventStore.Contracts",
            ],
            "Hexalith.EventStore.SignalR" => ["Hexalith.EventStore.Contracts"],
            "Hexalith.EventStore.Testing" =>
            [
                "Hexalith.EventStore.Client",
                "Hexalith.EventStore.Contracts",
                "Hexalith.EventStore.Server",
            ],
            "Hexalith.EventStore.Testing.Integration" =>
            [
                "Hexalith.EventStore.Client",
                "Hexalith.EventStore.DomainService",
                "Hexalith.EventStore.Server",
                "Hexalith.EventStore.Testing",
            ],
            _ => [],
        };

    private static string[] ExpectedExternalHexalithDependencies(string packageId)
        => packageId switch
        {
            "Hexalith.EventStore.Admin.Server" => ["Hexalith.Tenants.Contracts"],
            "Hexalith.EventStore.Contracts" => ["Hexalith.Commons.UniqueIds"],
            _ => [],
        };

    private static (int ExitCode, string Output, string Error) RunPackageValidator(
        string validator,
        string packageDirectory)
        => validator.StartsWith("tools/", StringComparison.Ordinal)
            ? RunPythonScript(validator, packageDirectory, PackageFixtureVersion)
            : RunPythonScript(validator, packageDirectory);

    private static (int ExitCode, string Output, string Error) RunPythonScript(
        string script,
        params string[] arguments)
        => RunPythonScript(script, MsBuildPropertyTimeout, arguments);

    private static (int ExitCode, string Output, string Error) RunPythonScript(
        string script,
        TimeSpan timeout,
        params string[] arguments)
    {
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "python3",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                WorkingDirectory = FindRepositoryRoot(),
            },
        };
        process.StartInfo.ArgumentList.Add(script);
        foreach (string argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start().ShouldBeTrue($"Could not start {script}.");
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit((int)timeout.TotalMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"{script} timed out after {timeout}.");
        }

        return (
            process.ExitCode,
            outputTask.GetAwaiter().GetResult(),
            errorTask.GetAwaiter().GetResult());
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

    private static void AssertLiveSidecarWorkflowTargetsLiveProjectOutsideReleaseGate(
        string integration,
        string release)
    {
        integration.ShouldContain("dotnet test tests/Hexalith.EventStore.Server.LiveSidecar.Tests/");

        // Story 4.14 OQ8 support capture builds Server.Tests and invokes pinned -method
        // support oracles for --support-ctrf. Forbid any `dotnet test` of Server.Tests as
        // the live suite (build/-method paths remain allowed).
        integration.ShouldContain("dotnet build tests/Hexalith.EventStore.Server.Tests/");
        integration.ShouldContain("-method Hexalith.EventStore.Server.Tests.");
        integration.ShouldContain("--support-ctrf");
        integration.ShouldNotContain("dotnet test tests/Hexalith.EventStore.Server.Tests/");
        integration.ShouldNotContain("--filter \"Category=LiveSidecar\"");
        integration.ShouldNotContain("--filter \"Category!=LiveSidecar\"");

        MatchCollection daprVersions = Regex.Matches(
            integration,
            @"(?m)^[ \t]*DAPR_VERSION:[ \t]*'(?<version>[^']+)'[ \t]*(?:#.*)?$");
        daprVersions.Count.ShouldBeGreaterThan(
            0,
            "integration.yml must pin a shared DAPR_VERSION for Builds dapr-init.");
        foreach (Match daprVersion in daprVersions)
        {
            daprVersion.Groups["version"].Value.ShouldBe(
                "1.18.0",
                "Every shared DAPR_VERSION pin must be an installable Dapr CLI tag because Builds dapr-init uses one shared value for CLI install and runtime init.");
        }

        integration.ShouldContain("version: ${{ env.DAPR_VERSION }}");

        release.ShouldNotContain("integration.yml");
        release.ShouldNotContain("Hexalith.EventStore.Server.LiveSidecar.Tests");
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
        lines.ShouldContain("          persist-credentials: false");
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
                "        with:",
                "          persist-credentials: false",
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
