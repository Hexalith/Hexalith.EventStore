using System.IO.Compression;
using System.Diagnostics;
using System.Security.Cryptography;

using Hexalith.EventStore.ServiceDefaults.Routing;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

public sealed class PublishedTenantQuerySelectorAbiTests
{
    [Fact]
    public async Task AuthenticPublicTenantSevenConstructsOriginalDeclarationsAgainstExtendedContracts()
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "published-tenants-7.0.0");
        string archive = Path.Combine(directory, "Hexalith.Tenants.Contracts.7.0.0.nupkg");
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(archive))).ShouldBe("e21f5ac7b5f332f1921f70a249ab2032b9978d56ffdd6641a0a4b94d04850bdc");
        using var package = ZipFile.OpenRead(archive);
        package.GetEntry(".signature.p7s").ShouldNotBeNull().Length.ShouldBeGreaterThan(0);
        using var entry = package.GetEntry("lib/net10.0/Hexalith.Tenants.Contracts.dll")!.Open();
        using var bytes = new MemoryStream(); await entry.CopyToAsync(bytes, TestContext.Current.CancellationToken);
        string path = Path.Combine(directory, "Hexalith.Tenants.Contracts.dll");
        File.ReadAllBytes(path).ShouldBe(bytes.ToArray());
        var context = new PublishedTenantQuerySelectorLoadContext();
        try
        {
            var assembly = context.LoadFromAssemblyPath(path);
            System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(assembly).ShouldBeSameAs(context);
            var declarations = CompiledRouteCatalogDeclarations.Read([assembly]);
            declarations.ShouldNotBeEmpty();
            foreach (var declaration in declarations) declaration.Execution!.CompatibilityProjectionActorTypes.ShouldBeNull();
            CompiledRouteCatalogDeclarations.ReadOperations([assembly]).Count.ShouldBe(declarations.Count);
        }
        finally { context.Unload(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublishedFixtureMaterializationUsesRestoreResolvedCustomPathInEitherMode(bool sourceMode)
    {
        string root = FindRoot();
        string cache = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "published-tenants-7.0.0", "resolved-package-root.txt")).Trim();
        cache.ShouldNotBeNullOrWhiteSpace();
        string output = Directory.CreateTempSubdirectory("selector-public-fixture-").FullName;
        try
        {
            var start = new ProcessStartInfo("dotnet") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true };
            SourceMappingFixtureEnvironment.RemoveInheritedMapping(start);
            foreach (string argument in new[] { "msbuild", "tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj",
                "-t:CopyPublishedTenantQuerySelectorFixture", "-m:1", "-nodeReuse:false", "-v:q", "-p:RestorePackagesPath=" + cache,
                "-p:NuGetPackageRoot=" + cache,
                "-p:UseHexalithProjectReferences=" + sourceMode.ToString().ToLowerInvariant(), "-p:UseNuGetDeps=" + (!sourceMode).ToString().ToLowerInvariant(),
                "-p:OutDir=" + output + Path.DirectorySeparatorChar }) start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            Task<string> stdout = process.StandardOutput.ReadToEndAsync(), stderr = process.StandardError.ReadToEndAsync();
            try { await process.WaitForExitAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken); }
            finally { if (!process.HasExited) { process.Kill(true); await process.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)); } }
            process.ExitCode.ShouldBe(0, await stdout + await stderr);
            string selection = File.ReadAllText(Path.Combine(output, "published-tenants-7.0.0", "selection.txt")).Trim();
            selection.Split('|')[0].ShouldBe(sourceMode.ToString().ToLowerInvariant());
            if (sourceMode) selection.Split('|')[1].ShouldBe("true");
            foreach (string file in new[] { "Hexalith.Tenants.Contracts.7.0.0.nupkg", "Hexalith.Tenants.Contracts.dll" })
                File.ReadAllBytes(Path.Combine(output, "published-tenants-7.0.0", file)).AsSpan()
                    .SequenceEqual(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "published-tenants-7.0.0", file))).ShouldBeTrue(file);
        }
        finally { Directory.Delete(output, true); }
    }

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Hexalith.EventStore.slnx"))) return directory.FullName;
        throw new InvalidOperationException("The fixture materialization control requires its owning project.");
    }
}
