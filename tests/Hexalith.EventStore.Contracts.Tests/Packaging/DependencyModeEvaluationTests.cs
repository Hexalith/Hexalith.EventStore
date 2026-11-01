using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;

namespace Hexalith.EventStore.Contracts.Tests.Packaging;

public sealed class DependencyModeEvaluationTests
{
    private static readonly TimeSpan _msBuildEvaluationTimeout = TimeSpan.FromSeconds(30);

    [Theory]
    [InlineData("Debug", null, null, true, false)]
    [InlineData("Debug", "false", null, true, false)]
    [InlineData("Release", null, null, true, false)]
    [InlineData("Release", "true", null, true, true)]
    [InlineData("", null, null, true, false)]
    [InlineData("Staging", null, null, true, false)]
    [InlineData("Debug", "true", null, false, false)]
    [InlineData("Debug", "true", "true", true, true)]
    [InlineData("Debug", "false", "false", true, false)]
    [InlineData("Debug", null, "false", true, true)]
    [InlineData("Debug", null, "true", true, false)]
    public void ExternalCommonsDependencySelectsExactlyOneEdge(
        string configuration,
        string? explicitProjectReferences,
        string? legacyNuGetDependencies,
        bool sourceExists,
        bool expectProjectReference)
    {
        AssertExternalDependencySelectsExactlyOneEdge(
            "src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj",
            "HexalithCommonsRoot",
            "HexalithCommonsFromSource",
            "src/libraries/Hexalith.Commons.UniqueIds/Hexalith.Commons.UniqueIds.csproj",
            "Hexalith.Commons.UniqueIds.csproj",
            "Hexalith.Commons.UniqueIds",
            configuration,
            explicitProjectReferences,
            legacyNuGetDependencies,
            sourceExists,
            expectProjectReference);
    }

    [Theory]
    [InlineData("Debug", null, null, true, false)]
    [InlineData("Debug", "false", null, true, false)]
    [InlineData("Release", null, null, true, false)]
    [InlineData("Release", "true", null, true, true)]
    [InlineData("", null, null, true, false)]
    [InlineData("Staging", null, null, true, false)]
    [InlineData("Debug", "true", null, false, false)]
    [InlineData("Debug", "true", "true", true, true)]
    [InlineData("Debug", "false", "false", true, false)]
    [InlineData("Debug", null, "false", true, true)]
    [InlineData("Debug", null, "true", true, false)]
    public void ExternalTenantsDependencySelectsExactlyOneEdge(
        string configuration,
        string? explicitProjectReferences,
        string? legacyNuGetDependencies,
        bool sourceExists,
        bool expectProjectReference)
    {
        AssertExternalDependencySelectsExactlyOneEdge(
            "src/Hexalith.EventStore.Admin.Server/Hexalith.EventStore.Admin.Server.csproj",
            "HexalithTenantsBasePath",
            "HexalithTenantsFromSource",
            "Hexalith.Tenants.Contracts/Hexalith.Tenants.Contracts.csproj",
            "Hexalith.Tenants.Contracts.csproj",
            "Hexalith.Tenants.Contracts",
            configuration,
            explicitProjectReferences,
            legacyNuGetDependencies,
            sourceExists,
            expectProjectReference);
    }

    /// <summary>
    /// Propagates the root source version only to promoted references in the selected Tenants checkout.
    /// </summary>
    /// <param name="useVerifiedMapping">Whether the workspace mapping owns source version propagation.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PromotedTenantsReferencesPreserveSelectedSourceVersion(bool useVerifiedMapping)
    {
        string fixture = Path.Combine(
            Path.GetTempPath(),
            "hexalith-eventstore-promoted-references",
            Guid.NewGuid().ToString("N"));
        string selectedCheckout = Path.Combine(fixture, "selected");
        string selectedProject = Path.Combine(selectedCheckout, "Hexalith.Tenants.Contracts", "Hexalith.Tenants.Contracts.csproj");
        string outsideProject = Path.Combine(fixture, "selected-other", "Hexalith.Tenants.Contracts", "Hexalith.Tenants.Contracts.csproj");
        string selectedOutput = Path.Combine(fixture, "selected-version.txt");
        string outsideOutput = Path.Combine(fixture, "outside-version.txt");

        try
        {
            WriteVersionRecordingProject(selectedProject, selectedOutput, "1.2.3");
            WriteVersionRecordingProject(outsideProject, outsideOutput, "4.5.6");
            string fixtureProject = Path.Combine(fixture, "PromotedReferences.proj");
            new XDocument(new XElement("Project",
                new XElement("PropertyGroup",
                    new XElement("HexalithTenantsFromSource", "true"),
                    new XElement("HexalithTenantsBasePath", selectedCheckout),
                    new XElement("HexalithEventStoreVersion", "3.130.2"),
                    new XElement("HexalithSourceMappingHash", useVerifiedMapping ? new string('a', 64) : string.Empty)),
                new XElement("Import", new XAttribute("Project", Path.Combine(FindRepositoryRoot(), "Directory.Build.targets"))),
                new XElement("Target", new XAttribute("Name", "IncludeTransitiveProjectReferences"),
                    new XElement("ItemGroup", new[] { selectedProject, outsideProject }.Select(project =>
                        new XElement("ProjectReference", new XAttribute("Include", Path.GetRelativePath(fixture, project)),
                            new XElement("AdditionalProperties", "FixtureEdgeMarker=retained"))))),
                new XElement("Target", new XAttribute("Name", "RecordPromotedVersions"),
                    new XAttribute("DependsOnTargets", "IncludeTransitiveProjectReferences"),
                    new XElement("MSBuild", new XAttribute("Projects", "@(ProjectReference)"),
                        new XAttribute("Targets", "RecordVersion"),
                        new XAttribute("Properties", "%(ProjectReference.AdditionalProperties)")))))
                .Save(fixtureProject);

            RunMsBuild(fixture, [fixtureProject, "-nologo", "-target:RecordPromotedVersions", "-verbosity:quiet"]);

            File.ReadAllLines(selectedOutput).ShouldBe(new[] { useVerifiedMapping ? "1.2.3" : "3.130.2", "retained" });
            File.ReadAllLines(outsideOutput).ShouldBe(new[] { "4.5.6", "retained" });
        }
        finally
        {
            if (Directory.Exists(fixture))
            {
                Directory.Delete(fixture, recursive: true);
            }
        }
    }

    private static void WriteVersionRecordingProject(string project, string output, string version)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(project).ShouldNotBeNull());
        new XDocument(new XElement("Project",
            new XElement("PropertyGroup", new XElement("HexalithEventStoreVersion", version)),
            new XElement("Target", new XAttribute("Name", "RecordVersion"),
                new XElement("WriteLinesToFile", new XAttribute("File", output),
                    new XAttribute("Lines", "$(HexalithEventStoreVersion);$(FixtureEdgeMarker)"),
                    new XAttribute("Overwrite", "true")))))
            .Save(project);
    }

    private static void AssertExternalDependencySelectsExactlyOneEdge(
        string projectPath,
        string sourceRootProperty,
        string sourceSelectionProperty,
        string sourceProjectRelativePath,
        string sourceProjectFileName,
        string packageId,
        string configuration,
        string? explicitProjectReferences,
        string? legacyNuGetDependencies,
        bool sourceExists,
        bool expectProjectReference)
    {
        string sourceRoot = Path.Combine(
            Path.GetTempPath(),
            "hexalith-eventstore-dependency-mode",
            Guid.NewGuid().ToString("N"));

        try
        {
            if (sourceExists)
            {
                string sourceProject = Path.Combine(
                    sourceRoot,
                    sourceProjectRelativePath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(sourceProject).ShouldNotBeNull());
                File.WriteAllText(sourceProject, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
            }

            using JsonDocument evaluation = EvaluateProject(
                projectPath,
                sourceRootProperty,
                sourceRoot,
                sourceSelectionProperty,
                configuration,
                explicitProjectReferences,
                legacyNuGetDependencies);

            JsonElement properties = evaluation.RootElement.GetProperty("Properties");
            bool sourceIntent = string.Equals(
                properties.GetProperty("UseHexalithProjectReferences").GetString(),
                "true",
                StringComparison.OrdinalIgnoreCase);
            bool sourceSelected = string.Equals(
                properties.GetProperty(sourceSelectionProperty).GetString(),
                "true",
                StringComparison.OrdinalIgnoreCase);

            bool expectedSourceIntent = explicitProjectReferences is not null
                ? string.Equals(explicitProjectReferences, "true", StringComparison.OrdinalIgnoreCase)
                : string.Equals(legacyNuGetDependencies, "false", StringComparison.OrdinalIgnoreCase);

            sourceIntent.ShouldBe(expectedSourceIntent);
            sourceSelected.ShouldBe(expectedSourceIntent && sourceExists);

            JsonElement items = evaluation.RootElement.GetProperty("Items");
            int projectEdges = ItemIdentities(items, "ProjectReference")
                .Count(identity => identity.EndsWith(
                    sourceProjectFileName,
                    StringComparison.OrdinalIgnoreCase));
            int packageEdges = ItemIdentities(items, "PackageReference")
                .Count(identity => string.Equals(
                    identity,
                    packageId,
                    StringComparison.OrdinalIgnoreCase));

            projectEdges.ShouldBe(expectProjectReference ? 1 : 0);
            packageEdges.ShouldBe(expectProjectReference ? 0 : 1);
            (projectEdges + packageEdges).ShouldBe(1);
        }
        finally
        {
            if (Directory.Exists(sourceRoot))
            {
                Directory.Delete(sourceRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ServiceDefaultsDoesNotReferenceUnusedCommonsServiceDefaults()
    {
        string root = FindRepositoryRoot();
        string projectPath = Path.Combine(
            root,
            "src",
            "Hexalith.EventStore.ServiceDefaults",
            "Hexalith.EventStore.ServiceDefaults.csproj");
        XDocument project = XDocument.Load(projectPath);

        project
            .Descendants()
            .Where(element => element.Name.LocalName is "ProjectReference" or "PackageReference")
            .Select(element => element.Attributes()
                .FirstOrDefault(attribute => attribute.Name.LocalName is "Include" or "Update")?.Value)
            .OfType<string>()
            .ShouldNotContain(identity => identity.Contains(
                "Hexalith.Commons.ServiceDefaults",
                StringComparison.OrdinalIgnoreCase));
    }

    private static JsonDocument EvaluateProject(
        string projectPath,
        string sourceRootProperty,
        string sourceRoot,
        string sourceSelectionProperty,
        string configuration,
        string? explicitProjectReferences,
        string? legacyNuGetDependencies)
    {
        string root = FindRepositoryRoot();
        List<string> arguments =
        [
            projectPath,
            "-nologo",
            $"-getProperty:UseHexalithProjectReferences,UseNuGetDeps,{sourceSelectionProperty}",
            "-getItem:ProjectReference,PackageReference",
            $"-p:Configuration={configuration}",
            $"-p:{sourceRootProperty}={sourceRoot}",
        ];

        if (explicitProjectReferences is not null)
        {
            arguments.Add($"-p:UseHexalithProjectReferences={explicitProjectReferences}");
        }

        if (legacyNuGetDependencies is not null)
        {
            arguments.Add($"-p:UseNuGetDeps={legacyNuGetDependencies}");
        }

        return JsonDocument.Parse(RunMsBuild(root, arguments));
    }

    private static string RunMsBuild(string workingDirectory, IEnumerable<string> arguments)
    {
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                WorkingDirectory = workingDirectory,
            },
        };

        process.StartInfo.ArgumentList.Add("msbuild");
        foreach (string argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start().ShouldBeTrue("Could not start dotnet msbuild for dependency-mode evaluation.");
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit((int)_msBuildEvaluationTimeout.TotalMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException(
                $"Dependency-mode evaluation timed out after {_msBuildEvaluationTimeout}.");
        }

        string output = outputTask.GetAwaiter().GetResult();
        string error = errorTask.GetAwaiter().GetResult();
        process.ExitCode.ShouldBe(0, $"Dependency-mode MSBuild failed: {output}{Environment.NewLine}{error}");

        return output;
    }

    private static IEnumerable<string> ItemIdentities(JsonElement items, string itemName)
    {
        if (!items.TryGetProperty(itemName, out JsonElement itemCollection))
        {
            return [];
        }

        return itemCollection
            .EnumerateArray()
            .Select(item => item.GetProperty("Identity").GetString())
            .OfType<string>();
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

        throw new DirectoryNotFoundException(
            "Could not locate repository root from the test working directory.");
    }
}
