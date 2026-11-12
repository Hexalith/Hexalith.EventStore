using System.Security;
using System.Security.Cryptography;
using System.Text.Json;
using System.Diagnostics;
using System.Runtime.Loader;

using global::Aspire.Hosting;
using global::Aspire.Hosting.ApplicationModel;
using Hexalith.EventStore.Aspire;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Routing;

namespace Hexalith.EventStore.AppHost.Tests.Configuration;

/// <summary>Uses real SDK builds of selected optional children; no topology or foreign package feed is used.</summary>
[Collection(AspireEnvironmentMutationCollection.Name)]
public sealed class DevelopmentRouteCatalogOwnerPreparationTests
{
    /// <summary>Standalone fixture overrides retain their selected inputs without mutating the caller's mapping or package cache.</summary>
    [Fact]
    public void StandaloneFixtureBuildEnvironmentDoesNotMutateItsCaller()
    {
        string[] inherited = ["AfterMicrosoftNETSdkTargets", "CustomAfterDirectoryBuildTargets", "CustomAfterMicrosoftCommonProps",
            "CustomBeforeDirectoryBuildProps", "CustomBeforeDirectoryBuildTargets", "HEXALITH_SOURCE_MAPPING_HASH", "HEXALITH_SOURCE_MAPPING_PATH"];
        var before = inherited.Append("NUGET_PACKAGES").ToDictionary(name => name, Environment.GetEnvironmentVariable, StringComparer.Ordinal);
        var metadata = new SelectedOwnerProjectMetadata("selected.csproj", "selected-directory", "selected-output");

        var environment = metadata.BuildEnvironment;

        foreach (string name in inherited) environment[name].ShouldBeEmpty();
        environment["SelectedOwnerArtifact"].ShouldBe("owned");
        environment["SelectedOwnerOutput"].ShouldBe("selected-output");
        environment.ShouldNotContainKey("NUGET_PACKAGES");
        metadata.ProjectPath.ShouldBe("selected.csproj");
        metadata.BuildWorkingDirectory.ShouldBe("selected-directory");
        foreach (var variable in before) Environment.GetEnvironmentVariable(variable.Key).ShouldBe(variable.Value);
    }

    /// <summary>A file at a loaded assembly's path cannot substitute a different module while retaining the loaded SDK claim.</summary>
    [Fact]
    public void LoadedSdkIdentityRequiresTheActualLoadedModuleBytes()
    {
        string root = Directory.CreateTempSubdirectory("catalog-loaded-sdk-").FullName;
        var context = new AssemblyLoadContext("selected-owner-sdk-identity", isCollectible: true);
        try
        {
            string path = Path.Combine(root, "Hexalith.EventStore.Contracts.dll");
            File.Copy(typeof(RouteCatalogEnvelope).Assembly.Location, path);
            var loaded = context.LoadFromAssemblyPath(path);
            _ = loaded.ManifestModule.ModuleVersionId;
            DevelopmentRouteCatalogOwnerLoadContext.LoadedSdkDigest(loaded)
                .ShouldBe(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))));
            string replacement = Path.Combine(root, "replacement.dll");
            File.Copy(typeof(DevelopmentCatalogProbeDeclarations).Assembly.Location, replacement);
            File.Move(replacement, path, overwrite: true);
            Should.Throw<InvalidOperationException>(() => DevelopmentRouteCatalogOwnerLoadContext.LoadedSdkDigest(loaded))
                .Message.ShouldContain("differs from its actual loaded module");
        }
        finally { context.Unload(); Directory.Delete(root, recursive: true); }
    }

    /// <summary>The optional native Sample is prepared from its selected project, preserving all seven actual owner operations.</summary>
    [Fact(Timeout = 120000)]
    public async Task ActualSelectedSampleLoadsItsCompleteExistingOwnerInventoryAsync()
    {
        DirectoryInfo? root = new(Path.GetDirectoryName(new EventStoreProjectMetadata().ProjectPath)!);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Hexalith.EventStore.slnx"))) root = root.Parent;
        string path = Path.Combine(root?.FullName ?? throw new DirectoryNotFoundException("The selected SDK Sample root is required."),
            "samples", "Hexalith.EventStore.Sample", "Hexalith.EventStore.Sample.csproj");
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { DisableDashboard = true });
        builder.Environment.EnvironmentName = "Development";
        var selected = builder.AddProject("optional-selected-sample", path);
        await using var prepared = await selected.PrepareEventStoreDevelopmentOwnerAsync(TimeSpan.FromSeconds(90), TestContext.Current.CancellationToken);
        var operations = CompiledRouteCatalogDeclarations.ReadOperations([prepared.Owner.Contracts]);
        operations.Count.ShouldBe(7);
        operations.Count(operation => operation.Ingress == "public" && operation.Eligibility == "UI-only").ShouldBe(6);
        operations.Count(operation => operation.Ingress == "internal").ShouldBe(1);
        prepared.Owner.Contracts.GetName().Name.ShouldBe("Hexalith.EventStore.Sample");
        prepared.Owner.Project.ShouldBeSameAs(selected);
    }

    /// <summary>Reads the exact selected output and inherited build mapping, refusing absent inventories and changed SDK bytes.</summary>
    [Theory(Timeout = 120000)]
    [InlineData("valid")]
    [InlineData("configuration")]
    [InlineData("missing-inventory")]
    [InlineData("sdk-drift")]
    [InlineData("json-drift")]
    [InlineData("json-missing")]
    [InlineData("encoder-drift")]
    [InlineData("encoder-missing")]
    [InlineData("pipeline-drift")]
    [InlineData("pipeline-missing")]
    [InlineData("diagnostic-overflow")]
    public async Task ActualSelectedBuildOwnsItsOutputMetadataAndSdkIdentityAsync(string scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        using var mapping = IsolatedSourceMappingEnvironment.Enter();
        string root = Directory.CreateTempSubdirectory("catalog-selected-project-").FullName;
        string path = Path.Combine(root, "Selected.csproj");
        string output = Path.Combine(root, "selected-output") + Path.DirectorySeparatorChar;
        string? retained = null;
        try
        {
            string contracts = typeof(RouteCatalogEnvelope).Assembly.Location;
            var jsonDependencies = new Dictionary<string, string>
            {
                ["System.Text.Json"] = typeof(JsonSerializerOptions).Assembly.Location,
                ["System.Text.Encodings.Web"] = typeof(System.Text.Encodings.Web.JavaScriptEncoder).Assembly.Location,
                ["System.IO.Pipelines"] = typeof(System.IO.Pipelines.PipeReader).Assembly.Location,
            };
            var originalJsonDependencies = new Dictionary<string, string>(jsonDependencies);
            string? changedJsonDependency = scenario.Split('-')[0] switch
            {
                "json" => "System.Text.Json",
                "encoder" => "System.Text.Encodings.Web",
                "pipeline" => "System.IO.Pipelines",
                _ => null,
            };
            if (scenario == "sdk-drift")
            {
                string copy = Path.Combine(root, "Hexalith.EventStore.Contracts.dll");
                File.Copy(contracts, copy);
                using (var bytes = new FileStream(copy, FileMode.Append)) bytes.WriteByte(0);
                contracts = copy;
            }
            if (changedJsonDependency is not null && scenario.EndsWith("-drift", StringComparison.Ordinal))
            {
                string copy = Path.Combine(root, changedJsonDependency + ".dll");
                File.Copy(jsonDependencies[changedJsonDependency], copy);
                using (var bytes = new FileStream(copy, FileMode.Append)) bytes.WriteByte(0);
                jsonDependencies[changedJsonDependency] = copy;
            }
            string Reference(string assemblyPath) => $"<Reference Include=\"{SecurityElement.Escape(Path.GetFileNameWithoutExtension(assemblyPath))}\"><HintPath>{SecurityElement.Escape(assemblyPath)}</HintPath></Reference>";
            string overflow = scenario == "diagnostic-overflow" ? $"<Target Name=\"Overflow\" BeforeTargets=\"PrepareForBuild\"><Message Importance=\"high\" Text=\"{new string('x', 9 * 1024 * 1024)}\" /></Target>" : string.Empty;
            string drift = scenario switch
            {
                "sdk-drift" => $"<Target Name=\"SelectedRuntimeDrift\" AfterTargets=\"Build\"><Copy SourceFiles=\"{SecurityElement.Escape(contracts)}\" DestinationFiles=\"$(TargetDir)/Hexalith.EventStore.Contracts.dll\" /></Target>",
                _ when changedJsonDependency is not null && scenario.EndsWith("-drift", StringComparison.Ordinal)
                    => $"<Target Name=\"SelectedJsonDrift\" AfterTargets=\"Build\"><Copy SourceFiles=\"{SecurityElement.Escape(jsonDependencies[changedJsonDependency])}\" DestinationFiles=\"$(TargetDir)/{changedJsonDependency}.dll\" /></Target>",
                _ when changedJsonDependency is not null
                    => $"<Target Name=\"SelectedJsonMissing\" AfterTargets=\"Build\"><Delete Files=\"$(TargetDir)/{changedJsonDependency}.dll\" /></Target>",
                _ => string.Empty,
            };
            await File.WriteAllTextAsync(path, $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup><TargetFramework>net10.0</TargetFramework><TreatWarningsAsErrors>true</TreatWarningsAsErrors><MSBuildTreatWarningsAsErrors>true</MSBuildTreatWarningsAsErrors><AssemblyName>Selected$(SelectedOwnerArtifact)</AssemblyName><BaseOutputPath>$(SelectedOwnerOutput)</BaseOutputPath></PropertyGroup>
                  <ItemGroup>{Reference(contracts)}{string.Concat(jsonDependencies.Values.Select(Reference))}{Reference(typeof(Activity).Assembly.Location)}{Reference(typeof(RouteCatalogAdapterSourceAttribute).Assembly.Location)}{Reference(typeof(DevelopmentCatalogProbeDeclarations).Assembly.Location)}</ItemGroup>
                  <Target Name="RequireActualMapping" BeforeTargets="PrepareForBuild"><Error Condition="'$(SelectedOwnerArtifact)' != 'owned'" Text="The actual selected build environment was lost." /></Target>
                  {overflow}
                  {drift}
                </Project>
                """, TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(root, "Selected.cs"), scenario == "missing-inventory" ? "public sealed class EmptyOwner;" : changedJsonDependency is not null ? """
                [assembly: Hexalith.EventStore.Contracts.Routing.RouteCatalogDeclarationSource(typeof(Hexalith.EventStore.AppHost.Tests.Configuration.DevelopmentCatalogProbeDeclarations))]
                [assembly: Hexalith.EventStore.DomainService.RouteCatalogAdapterSource(typeof(Hexalith.EventStore.AppHost.Tests.Configuration.DevelopmentCatalogProbeDeclarations))]
                public sealed class SelectedOwner;
                """ : """
                [assembly: Hexalith.EventStore.Contracts.Routing.RouteCatalogDeclarationSource(typeof(SelectedOwner))]
                [assembly: Hexalith.EventStore.DomainService.RouteCatalogAdapterSource(typeof(Hexalith.EventStore.AppHost.Tests.Configuration.DevelopmentCatalogProbeDeclarations))]
                public static class SelectedOwner
                {
                    public static System.Collections.Generic.IReadOnlyList<Hexalith.EventStore.Contracts.Routing.RouteContractDeclaration> Routes
                        => Hexalith.EventStore.AppHost.Tests.Configuration.DevelopmentCatalogProbeDeclarations.Routes;
                    public static System.Collections.Generic.IReadOnlyList<Hexalith.EventStore.Contracts.Routing.RouteOperationContract> Operations
                    {
                        get
                        {
                            var options = new System.Text.Json.JsonSerializerOptions
                            {
                                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                            };
                            using var stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes("\"owner\""));
                            var reader = System.IO.Pipelines.PipeReader.Create(stream);
                            string value = System.Text.Json.JsonSerializer.DeserializeAsync<string>(reader, options).AsTask().GetAwaiter().GetResult();
                            reader.Complete();
                            if (value != "owner") throw new System.InvalidOperationException("The selected owner's actual PipeReader JSON API failed.");
                            return Hexalith.EventStore.AppHost.Tests.Configuration.DevelopmentCatalogProbeDeclarations.Operations;
                        }
                    }
                }
                """, TestContext.Current.CancellationToken);
            var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { DisableDashboard = true });
            builder.Environment.EnvironmentName = "Development";
#pragma warning disable ASPIREPROJECTS001
            var selected = builder.AddResource(new ProjectResource("actual-selected-child"))
                .WithAnnotation(new SelectedOwnerProjectMetadata(path, root, output))
                .WithProjectDefaults(new ProjectResourceOptions { ExcludeLaunchProfile = true });
#pragma warning restore ASPIREPROJECTS001
            if (scenario == "configuration")
            {
#pragma warning disable ASPIREEXTENSION001
                var launch = (ProjectLaunchConfiguration)await selected.Resource.CreateLaunchConfigurationAsync(ExecutableLaunchMode.NoDebug, TestContext.Current.CancellationToken);
                launch.BuildConfiguration = "DebugSelected";
                selected.WithDebugSupport(_ => Task.FromResult(launch), "project");
#pragma warning restore ASPIREEXTENSION001
            }
            if (scenario is "missing-inventory" or "sdk-drift" or "diagnostic-overflow" || changedJsonDependency is not null)
            {
                var refusal = await Should.ThrowAsync<InvalidOperationException>(() => selected.PrepareEventStoreDevelopmentOwnerAsync(TimeSpan.FromSeconds(90), TestContext.Current.CancellationToken));
                retained = refusal.Message.Split("private diagnostics: ", StringSplitOptions.None)[1];
                File.Exists(Path.Combine(retained, "build.stdout.log")).ShouldBeTrue();
                File.Exists(Path.Combine(retained, "preparation.json")).ShouldBeFalse();
                refusal.ToString().ShouldContain(scenario switch
                {
                    "sdk-drift" => "differs from the actual AppHost SDK",
                    _ when changedJsonDependency is not null && scenario.EndsWith("-drift", StringComparison.Ordinal)
                        => $"Selected child SDK dependency '{changedJsonDependency}' differs from the actual AppHost SDK",
                    _ when changedJsonDependency is not null
                        => $"Selected child JSON contract dependency '{changedJsonDependency}' is absent from its immutable output snapshot",
                    "diagnostic-overflow" => "bounded stream limit",
                    _ => "No compiled owner declaration source",
                });
                if (changedJsonDependency is not null && scenario.EndsWith("-missing", StringComparison.Ordinal))
                    File.Exists(Path.Combine(retained, "metadata", changedJsonDependency + ".dll")).ShouldBeFalse();
                if (changedJsonDependency is not null && scenario.EndsWith("-drift", StringComparison.Ordinal))
                    SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(retained, "metadata", changedJsonDependency + ".dll"), TestContext.Current.CancellationToken))
                        .ShouldNotBe(SHA256.HashData(await File.ReadAllBytesAsync(originalJsonDependencies[changedJsonDependency], TestContext.Current.CancellationToken)));
                if (scenario == "diagnostic-overflow") new FileInfo(Path.Combine(retained, "build.stdout.log")).Length.ShouldBeLessThanOrEqualTo(8 * 1024 * 1024);
                return;
            }
            var preparation = await selected.PrepareEventStoreDevelopmentOwnerAsync(TimeSpan.FromSeconds(90), TestContext.Current.CancellationToken);
            retained = preparation.Directory;
            try
            {
                preparation.Owner.Project.ShouldBeSameAs(selected);
                preparation.Owner.Contracts.Location.ShouldStartWith(Path.Combine(retained, "metadata"));
                preparation.Owner.Contracts.GetName().Name.ShouldBe("Selectedowned");
                CompiledRouteCatalogDeclarations.ReadOperations([preparation.Owner.Contracts]).ShouldHaveSingleItem().Digest
                    .ShouldBe(DevelopmentCatalogProbeDeclarations.Operations[0].Digest);
                using var receipt = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(retained, "preparation.json"), TestContext.Current.CancellationToken));
                var identity = receipt.RootElement;
                identity.GetProperty("projectPath").GetString().ShouldBe(path);
                identity.GetProperty("workingDirectory").GetString().ShouldBe(root);
                identity.GetProperty("targetPath").GetString().ShouldStartWith(output);
                identity.GetProperty("metadataPath").GetString().ShouldBe(preparation.Owner.Contracts.Location);
                identity.GetProperty("targetSha256").GetString().ShouldBe(Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(preparation.Owner.Contracts.Location, TestContext.Current.CancellationToken))));
                identity.GetProperty("moduleVersionId").GetGuid().ShouldBe(preparation.Owner.Contracts.ManifestModule.ModuleVersionId);
                identity.GetProperty("configuration").GetString().ShouldNotBeNullOrWhiteSpace();
                if (scenario == "configuration") identity.GetProperty("configuration").GetString().ShouldBe("DebugSelected");
                identity.GetProperty("sdk").GetString().ShouldNotBeNullOrWhiteSpace();
                identity.GetProperty("dependencies").GetArrayLength().ShouldBeGreaterThan(0);
            }
            finally { await preparation.DisposeAsync(); }
            Directory.Exists(retained).ShouldBeFalse();
        }
        finally
        {
            if (retained is not null && Directory.Exists(retained)) Directory.Delete(retained, recursive: true);
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>A finite deadline terminates an already executing ordinary build and retains its bounded diagnostics.</summary>
    [Fact(Timeout = 60000)]
    public async Task RunningSelectedBuildIsTerminatedAtItsDeadlineAsync()
    {
        using var mapping = IsolatedSourceMappingEnvironment.Enter();
        string root = Directory.CreateTempSubdirectory("catalog-selected-deadline-").FullName;
        string path = Path.Combine(root, "Deadline.csproj");
        string marker = Path.Combine(root, "running.pid");
        string? retained = null;
        try
        {
            await File.WriteAllTextAsync(path, """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
                  <UsingTask TaskName="AwaitOwnerDeadline" TaskFactory="RoslynCodeTaskFactory" AssemblyFile="$(MSBuildToolsPath)/Microsoft.Build.Tasks.Core.dll">
                    <ParameterGroup><Marker ParameterType="System.String" Required="true" /></ParameterGroup>
                    <Task><Code Type="Fragment" Language="cs"><![CDATA[System.IO.File.WriteAllText(Marker, typeof(System.Environment).GetProperty("ProcessId").GetValue(null, null).ToString()); System.Threading.Thread.Sleep(30000);]]></Code></Task>
                  </UsingTask>
                  <Target Name="ObserveRunningBuild" BeforeTargets="PrepareForBuild">
                    <AwaitOwnerDeadline Marker="$(MSBuildProjectDirectory)/running.pid" />
                  </Target>
                </Project>
                """, TestContext.Current.CancellationToken);
            var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { DisableDashboard = true });
            builder.Environment.EnvironmentName = "Development";
            var selected = builder.AddProject("deadline-child", path);
            Task<DevelopmentRouteCatalogPreparedOwner> preparation = selected.PrepareEventStoreDevelopmentOwnerAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
            using var markerDeadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            markerDeadline.CancelAfter(TimeSpan.FromSeconds(12));
            while (!File.Exists(marker)) await Task.Delay(50, markerDeadline.Token);
            int pid = int.Parse(await File.ReadAllTextAsync(marker, TestContext.Current.CancellationToken), System.Globalization.CultureInfo.InvariantCulture);
            using (var running = Process.GetProcessById(pid)) running.HasExited.ShouldBeFalse();
            var refusal = await Should.ThrowAsync<InvalidOperationException>(() => preparation);
            retained = refusal.Message.Split("private diagnostics: ", StringSplitOptions.None)[1];
            refusal.ToString().ShouldContain("canceled");
            File.Exists(Path.Combine(retained, "preparation.json")).ShouldBeFalse();
            new FileInfo(Path.Combine(retained, "build.stdout.log")).Length.ShouldBeLessThanOrEqualTo(8 * 1024 * 1024);
            try { using var stopped = Process.GetProcessById(pid); stopped.HasExited.ShouldBeTrue(); }
            catch (ArgumentException) { }
        }
        finally
        {
            if (retained is not null) Directory.Delete(retained, recursive: true);
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Refuses non-Development selection and cancellation before starting a child build.</summary>
    [Theory(Timeout = 10000)]
    [InlineData("production")]
    [InlineData("cancelled")]
    [InlineData("unbounded")]
    [InlineData("suppressed")]
    public async Task InvalidPreparationCannotStartASelectedChildAsync(string scenario)
    {
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { DisableDashboard = true });
        builder.Environment.EnvironmentName = scenario == "production" ? "Production" : "Development";
        var selected = builder.AddProject<EventStoreProjectMetadata>("selected");
        if (scenario == "unbounded")
            await Should.ThrowAsync<ArgumentOutOfRangeException>(() => selected.PrepareEventStoreDevelopmentOwnerAsync(Timeout.InfiniteTimeSpan, TestContext.Current.CancellationToken));
        else if (scenario == "production")
            await Should.ThrowAsync<InvalidOperationException>(() => selected.PrepareEventStoreDevelopmentOwnerAsync(TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken));
        else if (scenario == "suppressed")
            (await Should.ThrowAsync<InvalidOperationException>(() => selected.PrepareEventStoreDevelopmentOwnerAsync(TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken)))
                .Message.ShouldContain("suppressed builds");
        else
        {
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            await Should.ThrowAsync<OperationCanceledException>(() => selected.PrepareEventStoreDevelopmentOwnerAsync(TimeSpan.FromMinutes(1), cancelled.Token));
        }
    }
}
