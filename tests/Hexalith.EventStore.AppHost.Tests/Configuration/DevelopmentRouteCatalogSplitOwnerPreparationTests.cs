using System.Reflection;
using System.Security;
using System.Security.Cryptography;
using System.Text.Json;

using global::Aspire.Hosting;
using global::Aspire.Hosting.ApplicationModel;
using Hexalith.EventStore.Aspire;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Routing;

namespace Hexalith.EventStore.AppHost.Tests.Configuration;

/// <summary>Builds genuine split owner libraries beside their selected executable; no topology or source stand-in is used.</summary>
[Collection(AspireEnvironmentMutationCollection.Name)]
public sealed class DevelopmentRouteCatalogSplitOwnerPreparationTests
{
    /// <summary>The split API refuses path-bearing names before any selected build can run.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("../Split.Contracts.dll")]
    [InlineData("..\\Split.Contracts.dll")]
    [InlineData("/tmp/Split.Contracts.dll")]
    [InlineData("C:\\Split.Contracts.dll")]
    [InlineData("Split.Contracts.dll/child")]
    [InlineData("Split.Contracts.exe")]
    [InlineData("Split.Contracts.dll\n")]
    public async Task ExplicitArtifactNamesCannotEscapeTheSelectedOutputAsync(string invalid)
    {
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { DisableDashboard = true });
        builder.Environment.EnvironmentName = "Development";
        var selected = builder.AddProject<EventStoreProjectMetadata>("invalid-split");
        (await Should.ThrowAsync<ArgumentException>(() => selected.PrepareEventStoreDevelopmentOwnerAsync(
            invalid, "Split.Adapters.dll", TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken))).ParamName.ShouldBe("declarationAssemblyFileName");
        (await Should.ThrowAsync<ArgumentException>(() => selected.PrepareEventStoreDevelopmentOwnerAsync(
            "Split.Contracts.dll", invalid, TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken))).ParamName.ShouldBe("adapterAssemblyFileName");
    }

    /// <summary>Exact split artifacts retain their distinct PE identities; missing, foreign and mismatched output refuses without a receipt.</summary>
    [Theory(Timeout = 120000)]
    [InlineData("valid")]
    [InlineData("configuration")]
    [InlineData("root-source-drift")]
    [InlineData("root-snapshot-drift")]
    [InlineData("declaration-source-drift")]
    [InlineData("declaration-snapshot-drift")]
    [InlineData("adapter-source-drift")]
    [InlineData("adapter-snapshot-drift")]
    [InlineData("missing-declaration")]
    [InlineData("missing-adapter-root-has-inventory")]
    [InlineData("reversed-roles")]
    [InlineData("missing-adapter-marker")]
    [InlineData("mismatched-adapters")]
    [InlineData("foreign-declaration")]
    [InlineData("mismatched-file-name")]
    [InlineData("linked-declaration")]
    public async Task RealSelectedSplitOutputSuppliesExactOwnersOrRefusesAsync(string scenario)
    {
        using var mapping = IsolatedSourceMappingEnvironment.Enter();
        string root = Directory.CreateTempSubdirectory("catalog-split-owner-").FullName;
        string output = Path.Combine(root, "selected-output") + Path.DirectorySeparatorChar;
        string? retained = null;
        try
        {
            await CreateProjectsAsync(root, scenario, TestContext.Current.CancellationToken);
            var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { DisableDashboard = true });
            builder.Environment.EnvironmentName = "Development";
#pragma warning disable ASPIREPROJECTS001
            var selected = builder.AddResource(new ProjectResource("actual-split-child"))
                .WithAnnotation(new SelectedOwnerProjectMetadata(Path.Combine(root, "Root.csproj"), root, output))
                .WithProjectDefaults(new ProjectResourceOptions { ExcludeLaunchProfile = true });
#pragma warning restore ASPIREPROJECTS001
            if (scenario == "configuration")
            {
#pragma warning disable ASPIREEXTENSION001
                var launch = (ProjectLaunchConfiguration)await selected.Resource.CreateLaunchConfigurationAsync(ExecutableLaunchMode.NoDebug, TestContext.Current.CancellationToken);
                launch.BuildConfiguration = "DebugSplit";
                selected.WithDebugSupport(_ => Task.FromResult(launch), "project");
#pragma warning restore ASPIREEXTENSION001
            }
            string declarations = scenario switch
            {
                "missing-declaration" => "Missing.Contracts.dll", "reversed-roles" => "Split.Adapters.dll",
                "foreign-declaration" => "Foreign.Contracts.dll", "mismatched-file-name" => "Substitute.dll", _ => "Split.Contracts.dll",
            };
            string adapters = scenario switch { "missing-adapter-root-has-inventory" => "Missing.Adapters.dll", "reversed-roles" => "Split.Contracts.dll", _ => "Split.Adapters.dll" };
            if (scenario is not ("valid" or "configuration") && !scenario.EndsWith("-drift", StringComparison.Ordinal))
            {
                var refusal = await Should.ThrowAsync<InvalidOperationException>(() => selected.PrepareEventStoreDevelopmentOwnerAsync(
                    declarations, adapters, TimeSpan.FromSeconds(90), TestContext.Current.CancellationToken));
                retained = refusal.Message.Split("private diagnostics: ", StringSplitOptions.None)[1];
                File.Exists(Path.Combine(retained, "preparation.json")).ShouldBeFalse();
                refusal.ToString().ShouldContain(scenario switch
                {
                    "missing-declaration" or "missing-adapter-root-has-inventory" or "linked-declaration" => "not an actual regular selected output file",
                    "reversed-roles" => "No compiled owner declaration source", "missing-adapter-marker" => "compiled command adapter factory",
                    "mismatched-adapters" => "adapter inventory differs", "foreign-declaration" => "outside the selected runtime dependency closure",
                    _ => "does not match its actual assembly name",
                });
                return;
            }
            var prepared = await selected.PrepareEventStoreDevelopmentOwnerAsync(declarations, adapters, TimeSpan.FromSeconds(90), TestContext.Current.CancellationToken);
            retained = prepared.Directory;
            try
            {
                prepared.Owner.Project.ShouldBeSameAs(selected);
                prepared.Owner.Contracts.ShouldNotBeSameAs(prepared.Owner.Adapters);
                prepared.Owner.Contracts.GetName().Name.ShouldBe("Split.Contracts"); prepared.Owner.Adapters.GetName().Name.ShouldBe("Split.Adapters");
                prepared.Owner.Contracts.GetCustomAttribute<RouteCatalogDeclarationSourceAttribute>()!.SourceType.Assembly.ShouldBeSameAs(prepared.Owner.Contracts);
                CompiledRouteCatalogDeclarations.ReadOperations([prepared.Owner.Contracts]).ShouldHaveSingleItem().Digest.ShouldBe(DevelopmentCatalogProbeDeclarations.Operations[0].Digest);
                using var receipt = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(retained, "preparation.json"), TestContext.Current.CancellationToken));
                var identity = receipt.RootElement;
                identity.GetProperty("assembly").GetString().ShouldStartWith("Split.Rootowned,");
                identity.GetProperty("targetPath").GetString().ShouldStartWith(output);
                identity.GetProperty("workingDirectory").GetString().ShouldBe(root);
                if (scenario == "configuration") identity.GetProperty("configuration").GetString().ShouldBe("DebugSplit");
                foreach (var (role, assembly) in new[] { ("declarations", prepared.Owner.Contracts), ("adapters", prepared.Owner.Adapters) })
                {
                    var artifact = identity.GetProperty("ownerArtifacts").GetProperty(role);
                    artifact.GetProperty("fileName").GetString().ShouldBe(assembly.GetName().Name + ".dll");
                    artifact.GetProperty("assembly").GetString().ShouldBe(assembly.FullName);
                    artifact.GetProperty("moduleVersionId").GetGuid().ShouldBe(assembly.ManifestModule.ModuleVersionId);
                    artifact.GetProperty("metadataPath").GetString().ShouldBe(assembly.Location);
                    string digest = Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(assembly.Location, TestContext.Current.CancellationToken)));
                    artifact.GetProperty("sha256").GetString().ShouldBe(digest);
                    string source = artifact.GetProperty("sourcePath").GetString()!;
                    Path.GetDirectoryName(source).ShouldBe(Path.GetDirectoryName(identity.GetProperty("targetPath").GetString()));
                    Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(source, TestContext.Current.CancellationToken))).ShouldBe(digest);
                    identity.GetProperty("dependencies").EnumerateArray().Single(file => file.GetProperty("name").GetString() == Path.GetFileName(source))
                        .GetProperty("sha256").GetString().ShouldBe(digest);
                }
                using var application = builder.Build();
                await builder.Eventing.PublishAsync(new BeforeResourceStartedEvent(selected.Resource, application.Services), TestContext.Current.CancellationToken);
                if (scenario.EndsWith("-drift", StringComparison.Ordinal))
                {
                    string name = scenario.StartsWith("declaration-", StringComparison.Ordinal) ? declarations
                        : scenario.StartsWith("adapter-", StringComparison.Ordinal) ? adapters : Path.GetFileName(identity.GetProperty("targetPath").GetString()!);
                    string directory = scenario.Contains("snapshot", StringComparison.Ordinal) ? Path.Combine(retained, "metadata")
                        : Path.GetDirectoryName(identity.GetProperty("targetPath").GetString()!)!;
                    string changed = Path.Combine(directory, name);
                    if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(changed, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                    await File.AppendAllTextAsync(changed, "changed split owner", TestContext.Current.CancellationToken);
                    (await Should.ThrowAsync<InvalidOperationException>(() => builder.Eventing.PublishAsync(
                        new BeforeResourceStartedEvent(selected.Resource, application.Services), TestContext.Current.CancellationToken)))
                        .ToString().ShouldContain("managed runtime inputs changed");
                }
            }
            finally { await prepared.DisposeAsync(); }
            Directory.Exists(retained).ShouldBeFalse();
        }
        finally
        {
            if (retained is not null && Directory.Exists(retained)) Directory.Delete(retained, recursive: true);
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task CreateProjectsAsync(string root, string scenario, CancellationToken token)
    {
        string Reference(string path) => $"<Reference Include=\"{SecurityElement.Escape(Path.GetFileNameWithoutExtension(path))}\"><HintPath>{SecurityElement.Escape(path)}</HintPath></Reference>";
        string references = Reference(typeof(RouteCatalogEnvelope).Assembly.Location) + Reference(typeof(RouteCatalogAdapterSourceAttribute).Assembly.Location)
            + Reference(typeof(DevelopmentCatalogProbeDeclarations).Assembly.Location)
            + Reference(typeof(JsonSerializerOptions).Assembly.Location)
            + Reference(typeof(System.Text.Encodings.Web.JavaScriptEncoder).Assembly.Location)
            + Reference(typeof(System.IO.Pipelines.PipeReader).Assembly.Location)
            + Reference(typeof(System.Diagnostics.Activity).Assembly.Location);
        string Properties(string name) => $"<TargetFramework>net11.0</TargetFramework><GenerateRuntimeConfigurationFiles>true</GenerateRuntimeConfigurationFiles><TreatWarningsAsErrors>true</TreatWarningsAsErrors><MSBuildTreatWarningsAsErrors>true</MSBuildTreatWarningsAsErrors><ImplicitUsings>enable</ImplicitUsings><EnableDefaultCompileItems>false</EnableDefaultCompileItems><AssemblyName>{name}</AssemblyName><BaseOutputPath>$(SelectedOwnerOutput)</BaseOutputPath>";
        foreach (string name in new[] { "Split.Contracts", "Split.Adapters", "Foreign.Contracts" })
        {
            string directory = Path.Combine(root, name); Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, name + ".csproj"), $"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>{Properties(name)}</PropertyGroup><ItemGroup><Compile Include=\"Owner.cs\" />{references}</ItemGroup></Project>", token);
            string source = name.EndsWith("Contracts", StringComparison.Ordinal) ? """
                using Hexalith.EventStore.Contracts.Routing;
                using Hexalith.EventStore.AppHost.Tests.Configuration;
                [assembly: RouteCatalogDeclarationSource(typeof(SplitOwners.Declarations))]
                namespace SplitOwners;
                public static class Declarations
                {
                    public static IReadOnlyList<RouteContractDeclaration> Routes => DevelopmentCatalogProbeDeclarations.Routes;
                    public static IReadOnlyList<RouteOperationContract> Operations => DevelopmentCatalogProbeDeclarations.Operations;
                    public static IReadOnlyList<CommandIntentDeclaration> Commands => DevelopmentCatalogProbeDeclarations.Commands;
                    public static IReadOnlyDictionary<string,string> ReceiverMethods => DevelopmentCatalogProbeDeclarations.ReceiverMethods;
                }
                """ : $$"""
                using Hexalith.EventStore.DomainService;
                using Hexalith.EventStore.AppHost.Tests.Configuration;
                {{(scenario == "missing-adapter-marker" ? "" : "[assembly: RouteCatalogAdapterSource(typeof(SplitOwners.Adapters))]")}}
                namespace SplitOwners;
                public static class Adapters
                {
                    public static IReadOnlyList<IDeclaredIdempotencyIntentAdapter> Create() => {{(scenario == "mismatched-adapters" ? "[]" : "DevelopmentCatalogProbeDeclarations.Create()")}};
                }
                """;
            await File.WriteAllTextAsync(Path.Combine(directory, "Owner.cs"), source, token);
        }
        string after = scenario == "mismatched-file-name" ? "<Target Name=\"MismatchedArtifact\" AfterTargets=\"Build\"><Copy SourceFiles=\"$(TargetDir)Split.Contracts.dll\" DestinationFiles=\"$(TargetDir)Substitute.dll\" /></Target>" : string.Empty;
        if (scenario == "linked-declaration") after = """
            <UsingTask TaskName="LinkSplitArtifact" TaskFactory="RoslynCodeTaskFactory" AssemblyFile="$(MSBuildToolsPath)/Microsoft.Build.Tasks.Core.dll">
              <ParameterGroup><Artifact ParameterType="System.String" Required="true" /></ParameterGroup>
              <Task><Code Type="Fragment" Language="cs"><![CDATA[
                string outside = Artifact + ".outside"; System.IO.File.Move(Artifact, outside);
                typeof(System.IO.File).GetMethod("CreateSymbolicLink").Invoke(null, new object[] { Artifact, outside });
              ]]></Code></Task>
            </UsingTask>
            <Target Name="LinkedArtifact" AfterTargets="Build"><LinkSplitArtifact Artifact="$(TargetDir)Split.Contracts.dll" /></Target>
            """;
        await File.WriteAllTextAsync(Path.Combine(root, "Root.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>{Properties("Split.Root$(SelectedOwnerArtifact)")}<OutputType>Exe</OutputType></PropertyGroup>
              <ItemGroup><Compile Include="Root.cs" />{references}
                <ProjectReference Include="Split.Contracts/Split.Contracts.csproj" />
                <ProjectReference Include="Split.Adapters/Split.Adapters.csproj" />
                <ProjectReference Include="Foreign.Contracts/Foreign.Contracts.csproj" ReferenceOutputAssembly="false" />
              </ItemGroup>
              <Target Name="RequireSelectedEnvironment" BeforeTargets="PrepareForBuild"><Error Condition="'$(SelectedOwnerArtifact)' != 'owned'" Text="Selected split output mapping was lost." /></Target>
              {after}
            </Project>
            """, token);
        string rootAttributes = scenario == "missing-adapter-root-has-inventory" ? """
            [assembly: Hexalith.EventStore.Contracts.Routing.RouteCatalogDeclarationSource(typeof(Hexalith.EventStore.AppHost.Tests.Configuration.DevelopmentCatalogProbeDeclarations))]
            [assembly: Hexalith.EventStore.DomainService.RouteCatalogAdapterSource(typeof(Hexalith.EventStore.AppHost.Tests.Configuration.DevelopmentCatalogProbeDeclarations))]
            """ : string.Empty;
        await File.WriteAllTextAsync(Path.Combine(root, "Root.cs"), rootAttributes + "public static class Entry { public static void Main() { } }", token);
    }
}
