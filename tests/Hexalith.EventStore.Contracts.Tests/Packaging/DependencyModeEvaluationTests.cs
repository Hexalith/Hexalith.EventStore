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
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                WorkingDirectory = root,
            },
        };

        process.StartInfo.ArgumentList.Add("msbuild");
        process.StartInfo.ArgumentList.Add(projectPath);
        process.StartInfo.ArgumentList.Add("-nologo");
        process.StartInfo.ArgumentList.Add(
            $"-getProperty:UseHexalithProjectReferences,UseNuGetDeps,{sourceSelectionProperty}");
        process.StartInfo.ArgumentList.Add("-getItem:ProjectReference,PackageReference");
        process.StartInfo.ArgumentList.Add($"-p:Configuration={configuration}");
        process.StartInfo.ArgumentList.Add($"-p:{sourceRootProperty}={sourceRoot}");

        if (explicitProjectReferences is not null)
        {
            process.StartInfo.ArgumentList.Add(
                $"-p:UseHexalithProjectReferences={explicitProjectReferences}");
        }

        if (legacyNuGetDependencies is not null)
        {
            process.StartInfo.ArgumentList.Add($"-p:UseNuGetDeps={legacyNuGetDependencies}");
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
        process.ExitCode.ShouldBe(0, $"Dependency-mode evaluation failed: {error}");

        return JsonDocument.Parse(output);
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
