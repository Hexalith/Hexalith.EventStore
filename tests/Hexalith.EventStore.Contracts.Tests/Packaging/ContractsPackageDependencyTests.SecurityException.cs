using System.Diagnostics;
using System.Xml.Linq;

namespace Hexalith.EventStore.Contracts.Tests.Packaging;

/// <summary>Rejects changes outside the owner's exact five-pin servicing exception.</summary>
public sealed partial class ContractsPackageDependencyTests
{
    /// <summary>Runs the original raw and effective checks after the exact exception overlay.</summary>
    /// <param name="mutation">The nonexception authority violation.</param>
    [Theory]
    [InlineData("none")]
    [InlineData("raw")]
    [InlineData("raw-version")]
    [InlineData("effective")]
    [InlineData("crlf-authority")]
    public async Task AuthorizedRootPackageAdapterPreservesAllOtherAuthorityChecksAsync(string mutation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mutation);
        string root = FindRepositoryRoot();
        string catalog = ResolveSharedPackageVersionsPath(root,
            XDocument.Load(Path.Combine(root, "Directory.Packages.props")));
        string directory = Path.Combine(Path.GetTempPath(), "eventstore-authority-checks-" + Guid.NewGuid().ToString("N"));
        string fixture = Path.Combine(directory, "consumer");
        Directory.CreateDirectory(fixture);
        try
        {
            if (mutation == "crlf-authority")
            {
                string originalCatalog = catalog;
                catalog = Path.Combine(directory, "shared", "Props", "Directory.Packages.props");
                Directory.CreateDirectory(Path.GetDirectoryName(catalog)!);
                File.Copy(originalCatalog, catalog);
                string originalTools = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(originalCatalog)!, "..", "Tools"));
                string tools = Path.Combine(directory, "shared", "Tools");
                Directory.CreateDirectory(tools);
                foreach (string name in new[]
                {
                    "validate-consumer-package-authority.ps1", "validate-central-package-versions.ps1",
                })
                {
                    string content = await File.ReadAllTextAsync(Path.Combine(originalTools, name)).ConfigureAwait(true);
                    await File.WriteAllTextAsync(Path.Combine(tools, name),
                        content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal))
                        .ConfigureAwait(true);
                }
            }
            XElement pins = new(XDocument.Load(Path.Combine(root, "Directory.Packages.props"))
                .Root!.Elements("ItemGroup").Single());
            new XDocument(new XElement("Project",
                new XElement("PropertyGroup", new XElement("ManagePackageVersionsCentrally", "true")),
                new XElement("Import", new XAttribute("Project", catalog)),
                pins)).Save(Path.Combine(fixture, "Directory.Packages.props"));
            XElement project = new("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"),
                new XElement("PropertyGroup", new XElement("TargetFramework", "net10.0")),
                new XElement("ItemGroup",
                    new XElement("PackageReference", new XAttribute("Include", "Hexalith.EventStore.Server"))));
            string metadata = mutation == "raw-version" ? "Version" : "VersionOverride";
            if (mutation.StartsWith("raw", StringComparison.Ordinal))
            {
                project.Descendants("PackageReference").Single().SetAttributeValue(metadata, "9.9.9");
            }
            else if (mutation == "effective")
            {
                string imported = Path.Combine(directory, "unapproved.props");
                new XDocument(new XElement("Project", new XElement("ItemGroup",
                    new XElement("PackageVersion", new XAttribute("Update", "Microsoft.Extensions.Options"),
                        new XAttribute("Version", "10.0.14"))))).Save(imported);
                project.Add(new XElement("Import", new XAttribute("Project", imported)));
            }
            new XDocument(project).Save(Path.Combine(fixture, "Consumer.csproj"));

            (int exitCode, string output, string error) = await RunEventStorePackageAuthorityAsync(
                fixture, catalog, validateWrapperOnly: false).ConfigureAwait(true);
            bool expectedSuccess = mutation is "none" or "crlf-authority";
            exitCode.ShouldBe(expectedSuccess ? 0 : 1, $"{output}{Environment.NewLine}{error}");
            if (expectedSuccess)
            {
                output.ShouldContain("Consumer package authority validation passed for 1 projects");
            }
            else if (mutation.StartsWith("raw", StringComparison.Ordinal))
            {
                error.ShouldContain($"contains PackageReference {metadata} metadata '9.9.9'");
            }
            else
            {
                error.ShouldContain("resolves 'Microsoft.Extensions.Options' to '10.0.14' instead of '10.0.13'");
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Requires both temporary validator sources to match the original shared authority.</summary>
    /// <param name="fileName">The shared source copy to corrupt.</param>
    [Theory]
    [InlineData("validate-consumer-package-authority.ps1")]
    [InlineData("validate-central-package-versions.ps1")]
    public async Task AuthorizedRootPackageAdapterRejectsChangedSharedAuthorityAsync(string fileName)
    {
        string root = FindRepositoryRoot();
        string catalog = ResolveSharedPackageVersionsPath(root,
            XDocument.Load(Path.Combine(root, "Directory.Packages.props")));
        string directory = Path.Combine(Path.GetTempPath(), "eventstore-authority-hash-" + Guid.NewGuid().ToString("N"));
        string fixture = Path.Combine(directory, "consumer");
        string copiedCatalog = Path.Combine(directory, "shared", "Props", "Directory.Packages.props");
        string tools = Path.Combine(directory, "shared", "Tools");
        Directory.CreateDirectory(fixture);
        Directory.CreateDirectory(Path.GetDirectoryName(copiedCatalog)!);
        Directory.CreateDirectory(tools);
        try
        {
            File.Copy(catalog, copiedCatalog);
            File.Copy(Path.Combine(root, "Directory.Packages.props"), Path.Combine(fixture, "Directory.Packages.props"));
            string originalTools = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(catalog)!, "..", "Tools"));
            foreach (string name in new[]
            {
                "validate-consumer-package-authority.ps1", "validate-central-package-versions.ps1",
            })
            {
                File.Copy(Path.Combine(originalTools, name), Path.Combine(tools, name));
            }
            await File.AppendAllTextAsync(Path.Combine(tools, fileName), "\n# changed authority\n").ConfigureAwait(true);
            (int exitCode, _, string error) = await RunEventStorePackageAuthorityAsync(
                fixture, copiedCatalog, validateWrapperOnly: false).ConfigureAwait(true);
            exitCode.ShouldBe(1);
            error.ShouldContain("EventStore exact package authority validation failed.");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Requires every authorized tuple and refuses extra or conditional forms.</summary>
    /// <param name="mutation">The unapproved root declaration change.</param>
    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("extra")]
    [InlineData("action")]
    [InlineData("version")]
    [InlineData("identity-case")]
    [InlineData("item-condition")]
    [InlineData("group-condition")]
    [InlineData("project-condition")]
    [InlineData("unexpected-attribute")]
    [InlineData("nested-version")]
    [InlineData("namespace")]
    public async Task AuthorizedRootPackageExceptionRejectsChangedDeclarationAsync(string mutation)
    {
        string root = FindRepositoryRoot();
        XDocument wrapper = XDocument.Load(Path.Combine(root, "Directory.Packages.props"));
        XElement first = wrapper.Descendants("PackageVersion").First();
        switch (mutation)
        {
            case "missing":
                first.Remove();
                break;
            case "duplicate":
                wrapper.Descendants("PackageVersion").Last().ReplaceWith(new XElement(first));
                break;
            case "extra":
                first.Parent!.Add(new XElement("PackageVersion",
                    new XAttribute("Include", "Unapproved.Package"), new XAttribute("Version", "10.0.13")));
                break;
            case "action":
                first.Attribute("Include")!.Remove();
                first.SetAttributeValue("Update", "Microsoft.AspNetCore.SignalR.Common");
                break;
            case "version":
                first.SetAttributeValue("Version", "10.0.14");
                break;
            case "identity-case":
                first.SetAttributeValue("Include", "microsoft.AspNetCore.SignalR.Common");
                break;
            case "item-condition":
                first.SetAttributeValue("Condition", "'$(Configuration)' == 'Release'");
                break;
            case "group-condition":
                first.Parent!.SetAttributeValue("Condition", "'$(Configuration)' == 'Release'");
                break;
            case "project-condition":
                wrapper.Root!.SetAttributeValue("Condition", "'$(Configuration)' == 'Release'");
                break;
            case "unexpected-attribute":
                first.SetAttributeValue("Label", "unapproved");
                break;
            case "nested-version":
                first.Attribute("Version")!.Remove();
                first.Add(new XElement("Version", "10.0.13"));
                break;
            case "namespace":
                first.Name = XName.Get("PackageVersion", "urn:unapproved");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation));
        }

        string fixture = Path.Combine(Path.GetTempPath(), "eventstore-root-pins-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        try
        {
            wrapper.Save(Path.Combine(fixture, "Directory.Packages.props"));
            string catalog = ResolveSharedPackageVersionsPath(root,
                XDocument.Load(Path.Combine(root, "Directory.Packages.props")));
            (int exitCode, _, _) = await RunEventStorePackageAuthorityAsync(
                fixture, catalog, validateWrapperOnly: true).ConfigureAwait(true);
            exitCode.ShouldBe(1);
        }
        finally
        {
            Directory.Delete(fixture, recursive: true);
        }
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunEventStorePackageAuthorityAsync(
        string root, string catalogPath, bool validateWrapperOnly)
    {
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo("pwsh")
            {
                WorkingDirectory = FindRepositoryRoot(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            },
        };
        PackagingRepositoryPaths.RemoveRepositorySelectors(process.StartInfo);
        foreach (string argument in new[]
        {
            "-NoProfile", "-File",
            Path.Combine(FindRepositoryRoot(), "scripts", "validate-eventstore-package-authority.ps1"),
            "-RepositoryRoot", root, "-CatalogPath", catalogPath,
        })
        {
            process.StartInfo.ArgumentList.Add(argument);
        }
        if (validateWrapperOnly)
        {
            process.StartInfo.ArgumentList.Add("-ValidateWrapperOnly");
        }
        process.Start().ShouldBeTrue();
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(_consumerAuthorityValidationTimeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }
        return (process.ExitCode, await output.ConfigureAwait(true), await error.ConfigureAwait(true));
    }
}
