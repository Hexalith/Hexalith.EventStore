using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Runs the owning targets after SDK-style relative node promotion, preserving foreign roots and active mapper metadata.</summary>
public sealed class PromotedTenantSourceVersionTests
{
    [Theory]
    [InlineData("unmapped", true)]
    [InlineData("disabled", false)]
    [InlineData("hash", false)]
    [InlineData("mode", false)]
    [InlineData("environment-hash", false)]
    [InlineData("environment-path", false)]
    public async Task PromotedSelectedNodeKeepsOneProducerWhileUnrelatedAndMappedMetadataRemainOwned(string scenario, bool forwards)
    {
        string directory = Directory.CreateTempSubdirectory("promoted-tenant-source-").FullName;
        try
        {
            string work = Directory.CreateDirectory(Path.Combine(directory, "workspace")).FullName;
            string selected = Directory.CreateDirectory(Path.Combine(directory, "tenants", "src")).FullName;
            var properties = new XElement("PropertyGroup", new XElement("HexalithTenantsBasePath", selected),
                new XElement("HexalithTenantsFromSource", scenario == "disabled" ? "false" : "true"),
                new XElement("HexalithEventStoreVersion", "3.130.1"));
            if (scenario == "hash") properties.Add(new XElement("HexalithSourceMappingHash", "mapper-owned"));
            if (scenario == "mode") properties.Add(new XElement("HexalithSourceMappingMode", "source"));
            if (scenario == "environment-hash") properties.Add(new XElement("HEXALITH_SOURCE_MAPPING_HASH", "mapper-owned"));
            if (scenario == "environment-path") properties.Add(new XElement("HEXALITH_SOURCE_MAPPING_PATH", "mapper-owned.props"));
            XElement Reference(string relative, string id, string label) => new("ProjectReference", new XAttribute("Include", relative),
                new XElement("NuGetPackageId", id), new XElement("ProbeCase", label),
                new XElement("AdditionalProperties", "Witness=" + label), new XElement("Aliases", "retained-alias"));
            var project = new XDocument(new XElement("Project", properties,
                new XElement("Import", new XAttribute("Project", Path.Combine(RepositoryRoot(), "Directory.Build.targets"))),
                new XElement("Target", new XAttribute("Name", "IncludeTransitiveProjectReferences"), new XElement("ItemGroup",
                    Reference("../tenants/src/Hexalith.Tenants.Server/Hexalith.Tenants.Server.csproj", "Hexalith.Tenants.Server", "selected"),
                    Reference("../tenants/src-foreign/Hexalith.Tenants.Server/Hexalith.Tenants.Server.csproj", "Hexalith.Tenants.Server", "foreign-root"),
                    Reference("../tenants/src/Other/Other.csproj", "Other", "foreign-package")))));
            string path = Path.Combine(work, "promotion.proj");
            await File.WriteAllTextAsync(path, project.ToString(), TestContext.Current.CancellationToken);
            var start = new ProcessStartInfo("dotnet") { WorkingDirectory = work, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (string argument in new[] { "msbuild", path, "-nologo", "-t:IncludeTransitiveProjectReferences", "-getItem:ProjectReference" }) start.ArgumentList.Add(argument);
            start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
            start.Environment["DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER"] = "1";
            using var process = Process.Start(start)!;
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(15));
            Task<string> output = process.StandardOutput.ReadToEndAsync(deadline.Token), error = process.StandardError.ReadToEndAsync(deadline.Token);
            try { await process.WaitForExitAsync(deadline.Token); }
            finally
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
                }
            }
            string stdout = await output, stderr = await error;
            process.ExitCode.ShouldBe(0, stdout + stderr);
            using var result = JsonDocument.Parse(stdout);
            var references = result.RootElement.GetProperty("Items").GetProperty("ProjectReference").EnumerateArray().ToArray();
            references.Length.ShouldBe(3);
            foreach (var reference in references)
            {
                string label = reference.GetProperty("ProbeCase").GetString()!;
                reference.GetProperty("AdditionalProperties").GetString().ShouldBe(label == "selected" && forwards
                    ? "Witness=selected;HexalithEventStoreVersion=3.130.1" : "Witness=" + label);
                reference.GetProperty("Aliases").GetString().ShouldBe("retained-alias");
            }
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static string RepositoryRoot()
    {
        for (DirectoryInfo? parent = new(AppContext.BaseDirectory); parent is not null; parent = parent.Parent)
            if (File.Exists(Path.Combine(parent.FullName, "Hexalith.EventStore.slnx"))) return parent.FullName;
        throw new InvalidOperationException("The source-version control must run below its owning repository.");
    }
}
