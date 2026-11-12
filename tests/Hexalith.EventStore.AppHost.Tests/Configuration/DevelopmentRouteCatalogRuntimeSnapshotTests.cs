using System.Security;
using System.Reflection;
using System.Text.Json;
using global::Aspire.Hosting;
using global::Aspire.Hosting.ApplicationModel;
using Hexalith.EventStore.Aspire;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.DomainService;

namespace Hexalith.EventStore.AppHost.Tests.Configuration;

/// <summary>Exercises actual NET11 host resolution without executing a selected application's entry point.</summary>
[Collection(AspireEnvironmentMutationCollection.Name)]
public sealed class DevelopmentRouteCatalogRuntimeSnapshotTests
{
    /// <summary>Framework-pruned contracts are snapshotted from the selected runtime, with all start-time identities retained.</summary>
    [Theory(Timeout = 120000)]
    [InlineData("valid")]
    [InlineData("framework-installation-drift")]
    [InlineData("unselected-framework-installation")]
    [InlineData("native-runtime-body-drift")]
    [InlineData("nonexecutable-inherited-candidate")]
    [InlineData("owner-source-drift")]
    [InlineData("owner-snapshot-drift")]
    [InlineData("dependency-source-drift")]
    [InlineData("dependency-snapshot-drift")]
    [InlineData("native-environment-drift")]
    [InlineData("environment-drift")]
    [InlineData("local-body-drift")]
    [InlineData("runtimeconfig-drift")]
    [InlineData("deps-drift")]
    [InlineData("probe-drift")]
    [InlineData("probe-bcl-shadow")]
    [InlineData("insert-original-json")]
    [InlineData("insert-original-encodings")]
    [InlineData("insert-original-pipelines")]
    [InlineData("launch-path-drift")]
    [InlineData("launch-host-drift")]
    [InlineData("launch-executable-drift")]
    [InlineData("launch-relative-inherited-path")]
    [InlineData("launch-empty-inherited-path")]
    [InlineData("runtimeconfig-dev-insertion")]
    public async Task ActualPrunedFrameworkSelectionAndEveryBoundStartInputRemainClosedAsync(string scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        using var mapping = IsolatedSourceMappingEnvironment.Enter();
        string root = Directory.CreateTempSubdirectory("catalog-runtime-owner-").FullName;
        string? previousPath = Environment.GetEnvironmentVariable("PATH");
        string? previousHost = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        try
        {
            string? isolatedRuntime = null;
            string? addedRuntime = null;
            if (scenario is "framework-installation-drift" or "native-runtime-body-drift" or "unselected-framework-installation")
            {
                isolatedRuntime = Path.Combine(root, "runtime");
                CopyRuntimeInstallation(isolatedRuntime);
                Environment.SetEnvironmentVariable("PATH", isolatedRuntime + Path.PathSeparator + previousPath);
                Environment.SetEnvironmentVariable("DOTNET_HOST_PATH", Path.Combine(isolatedRuntime, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"));
            }
            else if (scenario == "nonexecutable-inherited-candidate")
            {
                string inert = Path.Combine(root, "dotnet");
                await File.WriteAllTextAsync(inert, "This file is not an executable host.", TestContext.Current.CancellationToken);
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(inert, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                Environment.SetEnvironmentVariable("PATH", root + Path.PathSeparator + previousPath);
            }
            string output = Path.Combine(root, "selected-output") + Path.DirectorySeparatorChar;
            string path = Path.Combine(root, "Selected.csproj");
            string Reference(Assembly assembly) => $"<Reference Include=\"{SecurityElement.Escape(assembly.GetName().Name)}\"><HintPath>{SecurityElement.Escape(assembly.Location)}</HintPath></Reference>";
            string references = Reference(typeof(RouteCatalogEnvelope).Assembly) + Reference(typeof(RouteCatalogAdapterSourceAttribute).Assembly)
                + Reference(typeof(DevelopmentCatalogProbeDeclarations).Assembly);
            string shadow = scenario == "probe-bcl-shadow"
                ? "<Target Name=\"ShadowRuntimeProbeBcl\" AfterTargets=\"Build\"><Copy SourceFiles=\"$(TargetPath)\" DestinationFiles=\"$(TargetDir)/System.Console.dll\" /></Target>"
                : string.Empty;
            await File.WriteAllTextAsync(path, $"""
                <Project Sdk="Microsoft.NET.Sdk.Web"><PropertyGroup><TargetFramework>net11.0</TargetFramework>
                  <ImplicitUsings>enable</ImplicitUsings><AssemblyName>RuntimeOwner$(SelectedOwnerArtifact)</AssemblyName>
                  <BaseOutputPath>$(SelectedOwnerOutput)</BaseOutputPath><TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                </PropertyGroup><ItemGroup>{references}</ItemGroup>{shadow}</Project>
                """, TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(root, "Program.cs"), """
                [assembly: Hexalith.EventStore.Contracts.Routing.RouteCatalogDeclarationSource(typeof(Hexalith.EventStore.AppHost.Tests.Configuration.DevelopmentCatalogProbeDeclarations))]
                [assembly: Hexalith.EventStore.DomainService.RouteCatalogAdapterSource(typeof(Hexalith.EventStore.AppHost.Tests.Configuration.DevelopmentCatalogProbeDeclarations))]
                System.IO.File.WriteAllText("application-entry-executed", "unexpected");
                throw new System.InvalidOperationException("Application startup must never run during metadata preparation.");
                """, TestContext.Current.CancellationToken);
            var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { DisableDashboard = true });
            builder.Environment.EnvironmentName = "Development";
#pragma warning disable ASPIREPROJECTS001
            var selected = builder.AddResource(new ProjectResource("runtime-owner"))
                .WithAnnotation(new SelectedOwnerProjectMetadata(path, root, output))
                .WithProjectDefaults(new ProjectResourceOptions { ExcludeLaunchProfile = true });
#pragma warning restore ASPIREPROJECTS001
            if (scenario == "probe-bcl-shadow")
            {
                var refusal = await Should.ThrowAsync<InvalidOperationException>(() => selected.PrepareEventStoreDevelopmentOwnerAsync(
                    TimeSpan.FromSeconds(90), TestContext.Current.CancellationToken));
                refusal.ToString().ShouldContain("runtime probe framework dependency 'System.Console'");
                string diagnostics = refusal.Message.Split("private diagnostics: ", StringSplitOptions.None)[1];
                try { File.Exists(Path.Combine(diagnostics, "runtime.stdout.log")).ShouldBeFalse(); }
                finally { Directory.Delete(diagnostics, recursive: true); }
                return;
            }
            await using var prepared = await selected.PrepareEventStoreDevelopmentOwnerAsync(TimeSpan.FromSeconds(90), TestContext.Current.CancellationToken);
            using var receipt = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(prepared.Directory, "preparation.json"), TestContext.Current.CancellationToken));
            var runtime = receipt.RootElement.GetProperty("managedRuntime");
            runtime.GetProperty("protocol").GetString().ShouldBe("EventStoreSelectedOwnerManagedRuntimeV1");
            runtime.GetProperty("modules").GetArrayLength().ShouldBe(4);
            runtime.GetProperty("runtimeInstallationInputsSha256").GetString()!.Length.ShouldBe(64);
            var originals = runtime.GetProperty("originalContractInputs").EnumerateArray().ToArray();
            originals.Length.ShouldBe(3);
            originals.ShouldAllBe(input => !input.GetProperty("present").GetBoolean() && input.GetProperty("sha256").ValueKind == JsonValueKind.Null);
            string target = receipt.RootElement.GetProperty("targetPath").GetString()!;
            foreach (string name in new[] { "System.Text.Json", "System.Text.Encodings.Web", "System.IO.Pipelines" })
            {
                File.Exists(Path.Combine(Path.GetDirectoryName(target)!, name + ".dll")).ShouldBeFalse();
                File.Exists(Path.Combine(prepared.Directory, "metadata", name + ".dll")).ShouldBeTrue();
                runtime.GetProperty("modules").EnumerateArray().Single(module => module.GetProperty("name").GetString() == name)
                    .GetProperty("path").GetString()!.ShouldNotStartWith(Path.Combine(prepared.Directory, "metadata"));
            }
            File.Exists(Path.Combine(root, "application-entry-executed")).ShouldBeFalse();
            File.Exists(Path.Combine(prepared.Directory, "metadata", "application-entry-executed")).ShouldBeFalse();
            using var application = builder.Build();
            await builder.Eventing.PublishAsync(new BeforeResourceStartedEvent(selected.Resource, application.Services), TestContext.Current.CancellationToken);
            if (scenario is "valid" or "nonexecutable-inherited-candidate") return;
            if (scenario == "unselected-framework-installation")
            {
                string shared = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(typeof(object).Assembly.Location)))!;
                string historical = Directory.EnumerateDirectories(Path.Combine(shared, "Microsoft.NETCore.App"))
                    .First(path => Path.GetFileName(path).StartsWith("10.", StringComparison.Ordinal));
                CopyDirectory(historical, Path.Combine(isolatedRuntime!, "shared", "Microsoft.NETCore.App", Path.GetFileName(historical)));
                await builder.Eventing.PublishAsync(new BeforeResourceStartedEvent(selected.Resource, application.Services), TestContext.Current.CancellationToken);
                return;
            }
            if (scenario == "framework-installation-drift")
            {
                string core = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
                string current = Path.GetFileName(core);
                Version version = Version.Parse(current);
                string next = new Version(version.Major, version.Minor, version.Build + 1).ToString();
                addedRuntime = Path.Combine(isolatedRuntime!, "shared", "Microsoft.NETCore.App", next);
                CopyDirectory(core, addedRuntime);
            }
            else if (scenario == "native-runtime-body-drift")
            {
                string core = Path.Combine(isolatedRuntime!, "shared", "Microsoft.NETCore.App", Path.GetFileName(Path.GetDirectoryName(typeof(object).Assembly.Location)!));
                string native = Path.Combine(core, OperatingSystem.IsWindows() ? "coreclr.dll" : OperatingSystem.IsMacOS() ? "libcoreclr.dylib" : "libcoreclr.so");
                await File.AppendAllTextAsync(native, "changed native runtime", TestContext.Current.CancellationToken);
            }
            else if (scenario == "native-environment-drift") selected.WithEnvironment("LD_PRELOAD", "unsupported");
            else if (scenario is "owner-source-drift" or "owner-snapshot-drift" or "dependency-source-drift" or "dependency-snapshot-drift")
            {
                string name = scenario.StartsWith("owner-", StringComparison.Ordinal) ? Path.GetFileName(target) : Path.GetFileName(typeof(RouteCatalogEnvelope).Assembly.Location);
                string changed = Path.Combine(scenario.Contains("snapshot", StringComparison.Ordinal)
                    ? Path.Combine(prepared.Directory, "metadata") : Path.GetDirectoryName(target)!, name);
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(changed, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                await File.AppendAllTextAsync(changed, "changed owner dependency", TestContext.Current.CancellationToken);
            }
            else if (scenario == "environment-drift") selected.WithEnvironment("DOTNET_ROLL_FORWARD", "LatestMajor");
            else if (scenario.StartsWith("launch-", StringComparison.Ordinal))
            {
                string alternate = Path.Combine(root, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
                File.Copy(Environment.ProcessPath!, alternate);
                if (scenario == "launch-path-drift") selected.WithEnvironment("PATH", root + Path.PathSeparator + previousPath);
                else if (scenario == "launch-relative-inherited-path") Environment.SetEnvironmentVariable("PATH", "." + Path.PathSeparator + previousPath);
                else if (scenario == "launch-empty-inherited-path") Environment.SetEnvironmentVariable("PATH", Path.PathSeparator + previousPath);
                else if (scenario == "launch-host-drift") selected.WithEnvironment("DOTNET_HOST_PATH", alternate);
                else selected.WithAnnotation(new ExecutableAnnotation { Command = alternate, WorkingDirectory = root });
            }
            else if (scenario == "runtimeconfig-dev-insertion")
            {
                string dev = Path.ChangeExtension(target, ".runtimeconfig.dev.json");
                File.Exists(dev).ShouldBeFalse();
                await File.WriteAllTextAsync(dev, """{"runtimeOptions":{"additionalProbingPaths":["unmodeled"]}}""", TestContext.Current.CancellationToken);
            }
            else if (scenario.StartsWith("insert-original-", StringComparison.Ordinal))
            {
                string name = scenario switch { "insert-original-json" => "System.Text.Json", "insert-original-encodings" => "System.Text.Encodings.Web", _ => "System.IO.Pipelines" };
                string inserted = Path.Combine(Path.GetDirectoryName(target)!, name + ".dll");
                File.Copy(target, inserted);
                File.Exists(inserted).ShouldBeTrue();
            }
            else
            {
                string changed = scenario switch
                {
                    "local-body-drift" => Path.Combine(prepared.Directory, "metadata", "System.Text.Json.dll"),
                    "runtimeconfig-drift" => Path.ChangeExtension(target, ".runtimeconfig.json"),
                    "deps-drift" => Path.ChangeExtension(target, ".deps.json"),
                    _ => Path.Combine(prepared.Directory, "metadata", "Hexalith.EventStore.RuntimeIdentityProbe.dll"),
                };
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(changed, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                await File.AppendAllTextAsync(changed, " ", TestContext.Current.CancellationToken);
            }
            (await Should.ThrowAsync<InvalidOperationException>(() => builder.Eventing.PublishAsync(
                new BeforeResourceStartedEvent(selected.Resource, application.Services), TestContext.Current.CancellationToken)))
                .ToString().ShouldContain(scenario.StartsWith("launch-", StringComparison.Ordinal) || scenario == "native-environment-drift" ? "unsupported"
                    : scenario == "framework-installation-drift" ? "managed runtime selection changed" : "managed runtime inputs changed");
            if (scenario == "framework-installation-drift")
            {
                string[] starts = Directory.GetFiles(prepared.Directory, "runtime-start-*.stdout.log");
                starts.Length.ShouldBe(2);
                string[] resolved = starts.Select(file =>
                {
                    using var report = JsonDocument.Parse(File.ReadAllBytes(file));
                    return report.RootElement.GetProperty("modules").EnumerateArray().Single(module => module.GetProperty("name").GetString() == "System.Private.CoreLib")
                        .GetProperty("path").GetString()!;
                }).ToArray();
                resolved.Count(path => path.StartsWith(addedRuntime! + Path.DirectorySeparatorChar, StringComparison.Ordinal)).ShouldBe(1);
                TestContext.Current.TestOutputHelper!.WriteLine(JsonSerializer.Serialize(new { actualRuntimeSelections = resolved }));
            }
            if (scenario == "native-runtime-body-drift")
                Directory.GetFiles(prepared.Directory, "runtime-start-*.stdout.log").Length.ShouldBe(1);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", previousPath);
            Environment.SetEnvironmentVariable("DOTNET_HOST_PATH", previousHost);
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Unmodeled startup mechanisms refuse before invoking the selected build or application.</summary>
    [Theory]
    [InlineData("LD_AUDIT")]
    [InlineData("inherited:LD_AUDIT")]
    [InlineData("LD_PRELOAD")]
    [InlineData("LD_LIBRARY_PATH")]
    [InlineData("DYLD_INSERT_LIBRARIES")]
    [InlineData("DYLD_LIBRARY_PATH")]
    [InlineData("DYLD_FRAMEWORK_PATH")]
    [InlineData("inherited:LD_PRELOAD")]
    [InlineData("inherited:LD_LIBRARY_PATH")]
    [InlineData("inherited:DYLD_INSERT_LIBRARIES")]
    [InlineData("inherited:DYLD_LIBRARY_PATH")]
    [InlineData("inherited:DYLD_FRAMEWORK_PATH")]
    [InlineData("DOTNET_STARTUP_HOOKS")]
    [InlineData("DOTNET_ADDITIONAL_DEPS")]
    [InlineData("DOTNET_SHARED_STORE")]
    [InlineData("CORECLR_ENABLE_PROFILING")]
    [InlineData("PATH")]
    [InlineData("DOTNET_HOST_PATH")]
    [InlineData("custom-executable")]
    [InlineData("relative-inherited-path")]
    [InlineData("empty-inherited-path")]
    public async Task UnsupportedRuntimeInjectionRefusesBeforeBuildingAsync(string variable)
    {
        ArgumentNullException.ThrowIfNull(variable);
        string root = Directory.CreateTempSubdirectory("catalog-runtime-refusal-").FullName;
        string? previousPath = Environment.GetEnvironmentVariable("PATH");
        bool inherited = variable.StartsWith("inherited:", StringComparison.Ordinal);
        string name = inherited ? variable["inherited:".Length..] : variable;
        string? previousValue = Environment.GetEnvironmentVariable(name);
        try
        {
            string path = Path.Combine(root, "Selected.csproj");
            await File.WriteAllTextAsync(path, """
                <Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net11.0</TargetFramework></PropertyGroup>
                  <Target Name="BuildMarker" BeforeTargets="PrepareForBuild"><WriteLinesToFile File="built" Lines="unexpected" /></Target>
                </Project>
                """, TestContext.Current.CancellationToken);
            var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { DisableDashboard = true });
            builder.Environment.EnvironmentName = "Development";
            var selected = builder.AddProject("runtime-refusal", path);
            if (variable == "custom-executable") selected.WithAnnotation(new ExecutableAnnotation { Command = Environment.ProcessPath!, WorkingDirectory = root });
            else if (variable == "relative-inherited-path") Environment.SetEnvironmentVariable("PATH", "." + Path.PathSeparator + previousPath);
            else if (variable == "empty-inherited-path") Environment.SetEnvironmentVariable("PATH", Path.PathSeparator + previousPath);
            else if (inherited) Environment.SetEnvironmentVariable(name, "unsupported");
            else selected.WithEnvironment(variable, "unsupported");
            (await Should.ThrowAsync<InvalidOperationException>(() => selected.PrepareEventStoreDevelopmentOwnerAsync(
                TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken))).ToString().ShouldContain("unsupported");
            File.Exists(Path.Combine(root, "built")).ShouldBeFalse();
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", previousPath);
            if (inherited) Environment.SetEnvironmentVariable(name, previousValue);
            Directory.Delete(root, recursive: true);
        }
    }

    private static void CopyRuntimeInstallation(string destination)
    {
        string shared = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(typeof(object).Assembly.Location)))!;
        string installation = Path.GetDirectoryName(shared)!;
        Directory.CreateDirectory(destination);
        string hostName = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
        string host = Path.Combine(destination, hostName);
        File.Copy(Path.Combine(installation, hostName), host);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(host, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        CopyDirectory(Path.Combine(installation, "host"), Path.Combine(destination, "host"));
        foreach (string framework in new[] { "Microsoft.NETCore.App", "Microsoft.AspNetCore.App" })
        {
            string version = Path.GetFileName(Path.GetDirectoryName(typeof(object).Assembly.Location)!);
            CopyDirectory(Path.Combine(shared, framework, version), Path.Combine(destination, "shared", framework, version));
        }
        // SDK and targeting packs are ordinary build prerequisites. Only the runtime/host bodies
        // are copied and later changed; links do not modify any machine installation.
        foreach (string name in new[] { "sdk", "packs", "sdk-manifests", "metadata" })
        {
            string source = Path.Combine(installation, name);
            if (!Directory.Exists(source)) continue;
            if (OperatingSystem.IsWindows()) CopyDirectory(source, Path.Combine(destination, name));
            else Directory.CreateSymbolicLink(Path.Combine(destination, name), source);
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.EnumerateFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        foreach (string directory in Directory.EnumerateDirectories(source)) CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }
}
