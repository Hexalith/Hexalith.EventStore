using System.Diagnostics;
using System.Xml.Linq;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Executes a real direct child so mapper metadata remains authoritative at child evaluation.</summary>
public sealed class DirectTenantSourceVersionTests
{
    [Theory]
    [InlineData("unmapped", "3.130.1")]
    [InlineData("disabled", "mapper-version")]
    [InlineData("HexalithSourceMappingHash", "mapper-version")]
    [InlineData("HexalithSourceMappingMode", "mapper-version")]
    [InlineData("HEXALITH_SOURCE_MAPPING_HASH", "mapper-version")]
    [InlineData("HEXALITH_SOURCE_MAPPING_PATH", "mapper-version")]
    public async Task DirectChildUsesExistingMappedVersionOrOrdinaryUnmappedProducer(string selector, string expected)
    {
        string root = Directory.CreateTempSubdirectory("direct-tenant-version-").FullName;
        try
        {
            string selected = Directory.CreateDirectory(Path.Combine(root, "tenants", "src")).FullName;
            string child = Path.Combine(Directory.CreateDirectory(Path.Combine(selected, "Hexalith.Tenants.Server")).FullName, "Hexalith.Tenants.Server.csproj");
            await File.WriteAllTextAsync(child, "<Project><Target Name=\"Report\" Returns=\"$(HexalithEventStoreVersion)|$(Witness)\" /></Project>", TestContext.Current.CancellationToken);
            var properties = new XElement("PropertyGroup", new XElement("HexalithTenantsBasePath", selected), new XElement("HexalithTenantsFromSource", selector == "disabled" ? "false" : "true"), new XElement("HexalithEventStoreVersion", "3.130.1"));
            if (selector is not ("unmapped" or "disabled")) properties.Add(new XElement(selector, selector == "HexalithSourceMappingMode" ? "source" : "mapper-owned"));
            string targets = FindTargets();
            var project = new XElement("Project", properties,
                new XElement("ItemGroup", new XElement("ProjectReference", new XAttribute("Include", child), new XElement("AdditionalProperties", "Witness=retained;HexalithEventStoreVersion=mapper-version"))),
                new XElement("Import", new XAttribute("Project", targets)),
                new XElement("Target", new XAttribute("Name", "Probe"), new XElement("MSBuild", new XAttribute("Projects", "@(ProjectReference)"), new XAttribute("Targets", "Report"), new XAttribute("Properties", "%(ProjectReference.AdditionalProperties)"),
                    new XElement("Output", new XAttribute("TaskParameter", "TargetOutputs"), new XAttribute("ItemName", "Actual"))),
                    new XElement("WriteLinesToFile", new XAttribute("File", Path.Combine(root, "actual.txt")), new XAttribute("Lines", "@(Actual)"))));
            string path = Path.Combine(root, "direct.proj"); await File.WriteAllTextAsync(path, project.ToString(), TestContext.Current.CancellationToken);
            var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = root };
            foreach (string argument in new[] { "msbuild", path, "-t:Probe", "-nologo", "-nodeReuse:false" }) start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            Task<string> stdout = process.StandardOutput.ReadToEndAsync(), stderr = process.StandardError.ReadToEndAsync();
            try { await process.WaitForExitAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken); }
            finally { if (!process.HasExited) { process.Kill(true); await process.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)); } }
            process.ExitCode.ShouldBe(0, await stdout + await stderr);
            (await File.ReadAllTextAsync(Path.Combine(root, "actual.txt"), TestContext.Current.CancellationToken)).Trim().ShouldBe(expected + "|retained");
        }
        finally { Directory.Delete(root, true); }
    }

    private static string FindTargets()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Hexalith.EventStore.slnx"))) return Path.Combine(directory.FullName, "Directory.Build.targets");
        throw new InvalidOperationException("The direct source control requires its actual owning targets.");
    }
}
