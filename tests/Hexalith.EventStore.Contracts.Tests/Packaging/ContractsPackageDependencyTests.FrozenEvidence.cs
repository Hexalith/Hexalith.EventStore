using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Hexalith.EventStore.Contracts.Tests.Packaging;

/// <summary>Distinguishes immutable MSBuild captures from the current consumer build graph.</summary>
public sealed partial class ContractsPackageDependencyTests
{
    // New exact enrollment of unchanged captures at f4d29263bd9d22dd432a6a464ee145e57eff3acd.
    // This does not replace any prior seal.
    // Membership AND raw bytes are bound; additions, removals and edits require investigation.
    private const string FrozenMsBuildInventorySha256 =
        "eee21473fe57b4aa1c71670d4d6454889d68d13290d3a0fa3350fe0a710db29a";
    private static readonly string[] FrozenMsBuildRoots =
    [
        "_bmad-output/implementation-artifacts/evidence/",
    ];

    /// <summary>Changed bytes or membership cannot inherit an immutable evidence exemption.</summary>
    /// <param name="mutation">The alteration to the exact original inventory.</param>
    [Theory]
    [InlineData("changed")]
    [InlineData("added")]
    [InlineData("removed")]
    [InlineData("renamed")]
    [InlineData("symlink")]
    public void FrozenMsBuildInventory_RejectsByteOrMembershipChanges(string mutation)
    {
        string root = Path.Combine(Path.GetTempPath(), "frozen-msbuild-inventory-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string path = FrozenMsBuildRoots[0] + "6-1-p1r-31251-published-run/initial-attempt/observation-a87fc7cc50e3447aae60a23491db9773/consumers/candidate/Directory.Packages.props";
            byte[] original = File.ReadAllBytes(Path.Combine(FindRepositoryRoot(), path));
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(root, path))!);
            File.WriteAllBytes(Path.Combine(root, path), original);
            string expectedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                path + " " + Convert.ToHexString(SHA256.HashData(original)).ToLowerInvariant() + "\n"))).ToLowerInvariant();
            VerifyFrozenMsBuildInventory(root, [path], 1, expectedHash);
            string[] paths = [path];
            switch (mutation)
            {
                case "changed":
                    File.AppendAllText(Path.Combine(root, path), "\n<!-- changed -->");
                    break;
                case "added":
                    File.WriteAllBytes(Path.Combine(root, path + ".props"), original);
                    paths = [path, path + ".props"];
                    break;
                case "removed":
                    paths = [];
                    break;
                case "symlink":
                    string external = Path.Combine(root, "original.props");
                    File.WriteAllBytes(external, original);
                    File.Delete(Path.Combine(root, path));
                    File.CreateSymbolicLink(Path.Combine(root, path), external);
                    break;
                case "renamed":
                    File.Move(Path.Combine(root, path), Path.Combine(root, path + ".props"));
                    paths = [path + ".props"];
                    break;
            }
            Should.Throw<Shouldly.ShouldAssertException>(() => VerifyFrozenMsBuildInventory(root, paths, 1, expectedHash));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Even unchanged archived MSBuild bytes cannot become active build inputs.</summary>
    /// <param name="activation">The evaluated import or reference mechanism.</param>
    [Theory]
    [InlineData("import")]
    [InlineData("property")]
    [InlineData("wildcard")]
    [InlineData("implicit")]
    [InlineData("reference")]
    [InlineData("file-alias")]
    [InlineData("directory-alias")]
    [InlineData("inactive")]
    [InlineData("release")]
    [InlineData("packages")]
    [InlineData("framework")]
    [InlineData("transitive")]
    [InlineData("reference-properties")]
    [InlineData("task")]
    [InlineData("task-property")]
    [InlineData("task-composed")]
    [InlineData("task-item")]
    [InlineData("task-bridge")]
    [InlineData("task-alias")]
    [InlineData("task-properties")]
    [InlineData("task-remove-properties")]
    [InlineData("task-wildcard")]
    [InlineData("task-transform")]
    [InlineData("task-transform-lowercase")]
    [InlineData("task-transform-unsupported")]
    [InlineData("task-import-directory")]
    [InlineData("target-reference")]
    [InlineData("comment")]
    [InlineData("cycle")]
    [InlineData("reverse-alias")]
    public async Task FrozenMsBuildEvidence_RejectsActivationAsync(string activation)
    {
        ArgumentNullException.ThrowIfNull(activation);
        string root = Path.Combine(Path.GetTempPath(), "frozen-msbuild-activation-" + Guid.NewGuid().ToString("N"));
        string archive = Path.Combine(root, FrozenMsBuildRoots[0], "fixture");
        Directory.CreateDirectory(archive);
        try
        {
            string frozen = Path.Combine(archive, "Captured.props");
            File.WriteAllText(frozen, "<Project />");
            XElement project = new("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"),
                new XElement("PropertyGroup", new XElement("TargetFramework", "net10.0")));
            string input = frozen;
            if (activation == "file-alias")
            {
                input = Path.Combine(root, "alias.props");
                File.CreateSymbolicLink(input, frozen);
            }
            else if (activation == "directory-alias")
            {
                Directory.CreateSymbolicLink(Path.Combine(root, "alias"), archive);
                input = Path.Combine(root, "alias", "Captured.props");
            }
            if (activation == "reverse-alias")
            {
                string external = Path.Combine(root, "outside.props");
                File.WriteAllText(external, "<Project />");
                File.Delete(frozen);
                File.CreateSymbolicLink(frozen, external);
            }
            if (activation is "transitive" or "reference-properties" or "cycle")
            {
                string bridge = Path.Combine(root, "external", "Bridge.csproj");
                Directory.CreateDirectory(Path.GetDirectoryName(bridge)!);
                XElement child = new("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"),
                    new XElement("PropertyGroup", new XElement("TargetFramework", "net10.0")));
                if (activation == "cycle")
                {
                    child.Add(new XElement("ItemGroup", new XElement("ProjectReference", new XAttribute("Include", Path.Combine(root, "Active.csproj")))));
                }
                else
                {
                    XElement import = new("Import", new XAttribute("Project", frozen));
                    if (activation == "reference-properties") import.SetAttributeValue("Condition", "'$(SelectFrozen)' == 'true'");
                    child.Add(import);
                }
                new XDocument(child).Save(bridge);
                XElement reference = new("ProjectReference", new XAttribute("Include", bridge));
                if (activation == "reference-properties") reference.SetAttributeValue("AdditionalProperties", "SelectFrozen=true");
                project.Add(new XElement("ItemGroup", reference));
            }
            else if (activation.StartsWith("task", StringComparison.Ordinal) || activation == "target-reference")
            {
                string targetProject = Path.Combine(archive, "Captured.csproj");
                File.WriteAllText(targetProject, "<Project />");
                string expression = targetProject;
                if (activation == "task-property")
                {
                    project.Element("PropertyGroup")!.Add(new XElement("CapturedProject", targetProject));
                    expression = "$(CapturedProject)";
                }
                if (activation == "task-composed")
                {
                    project.Element("PropertyGroup")!.Add(new XElement("EvidenceBase", Path.Combine(root, "_bmad-output", "implementation-artifacts")),
                        new XElement("CapturedProject", "$(EvidenceBase)/evidence/fixture/Captured.csproj"));
                    expression = "$(CapturedProject)";
                }
                if (activation == "task-item")
                {
                    project.Add(new XElement("ItemGroup", new XElement("CapturedProjects", new XAttribute("Include", targetProject))));
                    expression = "@(CapturedProjects)";
                }
                if (activation == "task-bridge")
                {
                    expression = Path.Combine(root, "Bridge.csproj");
                    new XDocument(new XElement("Project", new XElement("Import", new XAttribute("Project", frozen))))
                        .Save(expression);
                }
                if (activation == "task-alias")
                {
                    expression = Path.Combine(root, "task-alias.csproj");
                    File.CreateSymbolicLink(expression, targetProject);
                }
                if (activation is "task-properties" or "task-remove-properties" or "task-wildcard" or "task-transform" or "task-transform-lowercase" or "task-transform-unsupported")
                {
                    string bridge = Path.Combine(root, "Bridge.csproj");
                    XElement import = new("Import", new XAttribute("Project", frozen));
                    if (activation is "task-properties" or "task-remove-properties")
                    {
                        import.SetAttributeValue("Condition", "'$(SelectFrozen)' == 'true'"
                            + (activation == "task-remove-properties" ? " and '$(UseHexalithProjectReferences)' == ''" : string.Empty));
                    }
                    new XDocument(new XElement("Project", import)).Save(bridge);
                    expression = activation == "task-wildcard" ? Path.Combine(root, "Bridge*.csproj") : bridge;
                    if (activation is "task-transform" or "task-transform-lowercase" or "task-transform-unsupported")
                    {
                        project.Add(new XElement("ItemGroup", new XElement("CapturedProjects", new XAttribute("Include", bridge))));
                        expression = activation switch
                        {
                            "task-transform" => "@(CapturedProjects->'%(FullPath)')",
                            "task-transform-lowercase" => "@(CapturedProjects->'%(fullpath)')",
                            _ => "@(CapturedProjects->'%(FullPath)', '|')",
                        };
                    }
                }
                XElement action = activation == "target-reference"
                    ? new XElement("ItemGroup", new XElement("ProjectReference", new XAttribute("Include", expression)))
                    : new XElement("MSBuild", new XAttribute("Projects", expression), new XAttribute("Targets", "Build"));
                if (activation is "task-properties" or "task-remove-properties") action.SetAttributeValue("Properties", "SelectFrozen=true");
                if (activation == "task-remove-properties") action.SetAttributeValue("RemoveProperties", "UseHexalithProjectReferences");
                XElement target = new("Target", new XAttribute("Name", "AddFrozen"),
                    new XAttribute("BeforeTargets", "PrepareProjectReferences"), action);
                if (activation == "task-import-directory")
                {
                    string imported = Path.Combine(root, "imported", "Guard.targets");
                    Directory.CreateDirectory(Path.GetDirectoryName(imported)!);
                    File.CreateSymbolicLink(Path.Combine(root, "imported", "alias.csproj"), targetProject);
                    action.SetAttributeValue("Projects", "$(MSBuildThisFileDirectory)alias.csproj");
                    new XDocument(new XElement("Project", target)).Save(imported);
                    project.Add(new XElement("Import", new XAttribute("Project", imported)));
                }
                else project.Add(target);
            }
            else if (activation == "comment")
            {
                project.Add(new XComment("Harmless source comment\n" + frozen + "\n"));
            }
            else if (activation == "reference")
            {
                string reference = Path.Combine(archive, "Captured.csproj");
                File.WriteAllText(reference, "<Project />");
                project.Add(new XElement("ItemGroup", new XElement("ProjectReference", new XAttribute("Include", reference))));
            }
            else if (activation == "implicit")
            {
                project.Element("PropertyGroup")!.Add(new XElement("DirectoryBuildPropsPath", frozen));
                // DirectoryBuildPropsPath must be set before Sdk.props evaluates.
                project.Attribute("Sdk")!.Remove();
                project.Add(new XElement("Import", new XAttribute("Project", "Sdk.props"), new XAttribute("Sdk", "Microsoft.NET.Sdk")));
                project.Add(new XElement("Import", new XAttribute("Project", "Sdk.targets"), new XAttribute("Sdk", "Microsoft.NET.Sdk")));
            }
            else
            {
                if (activation == "property")
                {
                    project.Element("PropertyGroup")!.Add(new XElement("CapturedPath", frozen));
                    input = "$(CapturedPath)";
                }
                if (activation == "wildcard") input = Path.Combine(archive, "*.props");
                XElement import = new("Import", new XAttribute("Project", input));
                if (activation == "inactive") import.SetAttributeValue("Condition", "'1' == '2'");
                if (activation == "release") import.SetAttributeValue("Condition", "'$(Configuration)' == 'Release'");
                if (activation == "packages") import.SetAttributeValue("Condition", "'$(UseHexalithProjectReferences)' != 'true'");
                if (activation == "framework")
                {
                    project.Element("PropertyGroup")!.Element("TargetFramework")!.Remove();
                    project.Element("PropertyGroup")!.Add(new XElement("TargetFrameworks", "net10.0"));
                    import.SetAttributeValue("Condition", "'$(TargetFramework)' == 'net10.0'");
                }
                project.Add(import);
            }
            new XDocument(project).Save(Path.Combine(root, "Active.csproj"));
            if (activation is "inactive" or "comment" or "cycle")
            {
                await VerifyFrozenEvidenceNotBuildInputAsync(root, "Active.csproj").ConfigureAwait(true);
            }
            else
            {
                Exception? error = await Record.ExceptionAsync(() =>
                    VerifyFrozenEvidenceNotBuildInputAsync(root, "Active.csproj")).ConfigureAwait(true);
                error.ShouldBeOfType<Shouldly.ShouldAssertException>().Message
                    .ShouldContain("consumes frozen evidence as a build input");
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string[] GetFrozenMsBuildEvidenceExclusions(string root)
    {
        string[] paths = GetTrackedMsBuildFiles(root)
            .Where(path => FrozenMsBuildRoots.Any(prefix => path.StartsWith(prefix, StringComparison.Ordinal)))
            .Order(StringComparer.Ordinal).ToArray();
        VerifyFrozenMsBuildInventory(root, paths, 984, FrozenMsBuildInventorySha256);
        return paths;
    }

    /// <summary>Metadata filters retain every underlying project for conservative input inspection.</summary>
    /// <param name="frozenBridge">Whether the filtered bridge imports immutable evidence.</param>
    /// <param name="defaultMetadata">Whether task properties come from an item definition default.</param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task FrozenMsBuildEvidence_WithMetadataValueInspectsUnderlyingReferencesAsync(bool frozenBridge, bool defaultMetadata)
    {
        string root = Path.Combine(Path.GetTempPath(), "frozen-msbuild-filter-" + Guid.NewGuid().ToString("N"));
        string archive = Path.Combine(root, FrozenMsBuildRoots[0], "fixture");
        Directory.CreateDirectory(archive);
        try
        {
            string frozen = Path.Combine(archive, "Captured.props");
            File.WriteAllText(frozen, "<Project />");
            string bridge = Path.Combine(root, "Bridge.csproj");
            new XDocument(new XElement("Project", frozenBridge
                ? new XElement("Import", new XAttribute("Project", frozen), new XAttribute("Condition", "'$(SelectFrozen)' == 'true'")) : null)).Save(bridge);
            string collection = defaultMetadata ? "CapturedProjects" : "ProjectReference";
            string expression = "@(" + collection + "->WithMetadataValue('IsAspireProjectResource', 'true'))";
            var declarations = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase) { ["@" + collection] = [bridge] };
            ExpandFrozenBuildExpressions(expression, declarations, new HashSet<string>(StringComparer.OrdinalIgnoreCase))
                .ShouldBe([bridge]);
            new XDocument(new XElement("Project",
                defaultMetadata ? new XElement("ItemDefinitionGroup", new XElement(collection,
                    new XElement("SetConfiguration", "SelectFrozen=true"), new XElement("GlobalPropertiesToRemove"))) : null,
                new XElement("ItemGroup", new XElement(collection, new XAttribute("Include", bridge),
                    // Even an excluded metadata value remains in this deliberate over-approximation.
                    new XElement("IsAspireProjectResource", frozenBridge ? "false" : "true"),
                    frozenBridge && !defaultMetadata ? new XElement("SetConfiguration", "SelectFrozen=true") : null),
                    new XElement("FilteredProjects", new XAttribute("Include", expression))),
                new XElement("Target", new XAttribute("Name", "Build"),
                    new XElement("MSBuild", new XAttribute("Projects", "@(FilteredProjects)"), new XAttribute("Targets", "Build"),
                        new XAttribute("Properties", "%(FilteredProjects.SetConfiguration)"),
                        new XAttribute("RemoveProperties", "%(FilteredProjects.GlobalPropertiesToRemove)")))))
                .Save(Path.Combine(root, "Active.csproj"));
            if (frozenBridge)
            {
                Exception? error = await Record.ExceptionAsync(() => VerifyFrozenEvidenceNotBuildInputAsync(root, "Active.csproj"))
                    .ConfigureAwait(true);
                error.ShouldBeOfType<Shouldly.ShouldAssertException>().Message.ShouldContain(frozen);
            }
            else await VerifyFrozenEvidenceNotBuildInputAsync(root, "Active.csproj").ConfigureAwait(true);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Unknown batching metadata cannot silently discard custom task properties.</summary>
    [Fact]
    public void FrozenMsBuildEvidence_RejectsUnknownTaskBatchMetadata()
    {
        string definingFile = Path.Combine(Path.GetTempPath(), "Custom.targets");
        var declarations = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["?$MSBuildToolsPath"] = [Path.Combine(Path.GetTempPath(), "installed-sdk")],
            ["@FilteredProjects"] = ["@(ProjectReference->WithMetadataValue('Selected', 'true'))"],
        };
        XElement task = new("MSBuild", new XAttribute("Properties", "%(FilteredProjects.UnknownCustomMetadata)"));
        Should.Throw<Shouldly.ShouldAssertException>(() => FrozenTaskProperties(task, definingFile, declarations,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)).ToArray());
    }

    /// <summary>Path metadata cannot erase the directory containing a frozen-input bridge.</summary>
    /// <param name="originalItemSpecOverride">Whether source metadata explicitly overrides the original item path.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FrozenMsBuildEvidence_UnresolvedTransformCannotEraseNestedBridgeAsync(bool originalItemSpecOverride)
    {
        string root = Path.Combine(Path.GetTempPath(), "frozen-msbuild-nested-transform-" + Guid.NewGuid().ToString("N"));
        string archive = Path.Combine(root, FrozenMsBuildRoots[0], "fixture");
        string nested = Path.Combine(root, "nested");
        Directory.CreateDirectory(archive);
        Directory.CreateDirectory(nested);
        try
        {
            string frozen = Path.Combine(archive, "Captured.props");
            File.WriteAllText(frozen, "<Project />");
            new XDocument(new XElement("Project", new XElement("Import", new XAttribute("Project", frozen))))
                .Save(Path.Combine(nested, "Bridge.csproj"));
            File.Exists(Path.Combine(root, "Bridge.csproj")).ShouldBeFalse();
            string expression = originalItemSpecOverride ? "@(CapturedProjects->'%(OriginalItemSpec)')"
                : "@(CapturedProjects->'%(RelativeDir)Bridge.csproj')";
            XElement seed = new("CapturedProjects", new XAttribute("Include", originalItemSpecOverride ? "Seed.csproj" : "nested/Seed.csproj"));
            if (originalItemSpecOverride) seed.SetAttributeValue("OriginalItemSpec", "nested/Bridge.csproj");
            new XDocument(new XElement("Project",
                new XElement("ItemGroup", seed),
                new XElement("Target", new XAttribute("Name", "Build"),
                    new XElement("MSBuild", new XAttribute("Projects", expression), new XAttribute("Targets", "Build")))))
                .Save(Path.Combine(root, "Active.csproj"));
            Exception? error = await Record.ExceptionAsync(() => VerifyFrozenEvidenceNotBuildInputAsync(root, "Active.csproj"))
                .ConfigureAwait(true);
            error.ShouldBeOfType<Shouldly.ShouldAssertException>().Message.ShouldContain(originalItemSpecOverride
                ? frozen : "Unresolved executable item transform metadata");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Custom executable destinations retain effective metadata and MSBuild escaping.</summary>
    /// <param name="route">The direct task or command route into a nested bridge.</param>
    [Theory]
    [InlineData("function")]
    [InlineData("item-properties")]
    [InlineData("item-attribute")]
    [InlineData("combined-properties")]
    [InlineData("escaped-properties")]
    [InlineData("unsupported-escape")]
    [InlineData("exec")]
    [InlineData("exec-bridge")]
    [InlineData("exec-overflow")]
    [InlineData("benign")]
    public async Task FrozenMsBuildEvidence_CustomExecutableEdgesRemainFailClosedAsync(string route)
    {
        string root = Path.Combine(Path.GetTempPath(), "frozen-msbuild-custom-edge-" + Guid.NewGuid().ToString("N"));
        string archive = Path.Combine(root, FrozenMsBuildRoots[0], "fixture");
        Directory.CreateDirectory(archive);
        Directory.CreateDirectory(Path.Combine(root, "nested"));
        try
        {
            string frozen = Path.Combine(archive, "Captured.props");
            File.WriteAllText(frozen, "<Project />");
            string condition = "'$(SelectFrozen)' == 'true'" + (route == "combined-properties" ? " and '$(OtherFlag)' == 'true'" : string.Empty);
            new XDocument(new XElement("Project", new XElement("Import", new XAttribute("Project", frozen), new XAttribute("Condition", condition))))
                .Save(Path.Combine(root, "nested", "Bridge.csproj"));
            string destination = route == "function" ? "$([System.IO.Path]::Combine('nested', 'Bridge.csproj'))" : "nested/Bridge.csproj";
            XElement project = new("Project");
            XElement task = new("MSBuild", new XAttribute("Projects", destination), new XAttribute("Targets", "Build"));
            if (route is "item-properties" or "item-attribute" or "combined-properties" or "benign")
            {
                XElement item = new("CapturedProjects", new XAttribute("Include", destination));
                string value = "SelectFrozen=" + (route == "benign" ? "false" : "true");
                if (route == "item-attribute") item.SetAttributeValue("AdditionalProperties", value);
                else item.Add(new XElement("AdditionalProperties", value));
                project.Add(new XElement("ItemGroup", item));
                task.SetAttributeValue("Projects", "@(CapturedProjects)");
                if (route == "combined-properties") task.SetAttributeValue("Properties", "OtherFlag=true");
            }
            if (route is "escaped-properties" or "unsupported-escape")
                task.SetAttributeValue("Properties", "SelectFrozen=" + (route == "escaped-properties" ? "tr%75e" : "tr%ZZe"));
            if (route is "exec" or "exec-bridge")
            {
                string path = route == "exec" ? frozen : Path.Combine(root, "nested", "Bridge.csproj");
                task = new XElement("Exec", new XAttribute("Command", "dotnet msbuild \"" + path + "\" -p:SelectFrozen=true -t:Build -nologo"));
            }
            if (route == "exec-overflow")
            {
                XElement group = new("PropertyGroup");
                for (int index = 0; index < 257; index++)
                    group.Add(new XElement("BuildCommand", "dotnet msbuild nested/Bridge.csproj -t:Control" + index));
                group.Add(new XElement("BuildCommand", "dotnet msbuild \"" + frozen + "\""));
                project.Add(group);
                task = new XElement("Exec", new XAttribute("Command", "$(BuildCommand)"));
            }
            project.Add(new XElement("Target", new XAttribute("Name", "Build"), task));
            new XDocument(project).Save(Path.Combine(root, "Active.csproj"));
            if (route == "benign") await VerifyFrozenEvidenceNotBuildInputAsync(root, "Active.csproj").ConfigureAwait(true);
            else
            {
                Exception? error = await Record.ExceptionAsync(() => VerifyFrozenEvidenceNotBuildInputAsync(root, "Active.csproj")).ConfigureAwait(true);
                string expected = route switch
                {
                    "function" => "Unresolved executable project destination",
                    "unsupported-escape" => "Unsupported executable MSBuild escaping",
                    "exec-overflow" => "Executable command expansion exceeded",
                    _ => frozen,
                };
                error.ShouldBeOfType<Shouldly.ShouldAssertException>().Message.ShouldContain(expected);
            }
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    /// <summary>Ordinary Aspire RID tools invoke dotnet exec, which does not select an MSBuild project.</summary>
    [Fact]
    public void FrozenMsBuildEvidence_DirectExecDistinguishesTheAspireToolInvocation()
    {
        FrozenDirectExecTask("\"$([MSBuild]::NormalizePath('/sdk/../../'))\\dotnet\" exec \"/tools/Aspire.RuntimeIdentifier.Tool.dll\""
            + " --runtimeGraphPath \"/sdk/RuntimeIdentifierGraph.json\" --netcoreSdkRuntimeIdentifier \"linux-x64\"").ShouldBeNull();
    }

    /// <summary>SDK alias filtering preserves custom target references and evaluated reference metadata.</summary>
    /// <param name="targetTime">Whether a custom target introduces the frozen reference.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FrozenMsBuildEvidence_SdkAliasesCannotEraseSourceOrEvaluatedReferencesAsync(bool targetTime)
    {
        string root = Path.Combine(Path.GetTempPath(), "frozen-msbuild-sdk-alias-" + Guid.NewGuid().ToString("N"));
        string archive = Path.Combine(root, FrozenMsBuildRoots[0], "fixture");
        Directory.CreateDirectory(archive);
        try
        {
            string frozen = Path.Combine(archive, "Captured.props");
            File.WriteAllText(frozen, "<Project />");
            string bridge = Path.Combine(root, "Bridge.csproj");
            new XDocument(new XElement("Project", new XElement("Import", new XAttribute("Project", frozen),
                new XAttribute("Condition", "'$(SelectFrozen)' == 'true'")))).Save(bridge);
            // Child metadata is read by the evaluated ProjectReference traversal.
            // Target-time references remain explicitly inspected custom edges.
            XElement references = new("ItemGroup", new XElement("ProjectReference",
                new XAttribute("Include", targetTime ? frozen : bridge),
                new XElement("AdditionalProperties", "SelectFrozen=true")));
            XElement project = new("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"),
                new XElement("PropertyGroup", new XElement("TargetFramework", "net10.0")),
                targetTime ? new XElement("Target", new XAttribute("Name", "Build"), references) : references);
            new XDocument(project).Save(Path.Combine(root, "Active.csproj"));
            Exception? error = await Record.ExceptionAsync(() => VerifyFrozenEvidenceNotBuildInputAsync(root, "Active.csproj")).ConfigureAwait(true);
            error.ShouldBeOfType<Shouldly.ShouldAssertException>().Message.ShouldContain(frozen);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    /// <summary>The demonstrated SDK project protocol preserves custom additions and every real framework.</summary>
    /// <param name="route">The custom addition or framework-specific bridge.</param>
    [Theory]
    [InlineData("benign")]
    [InlineData("custom-addition")]
    [InlineData("framework")]
    [InlineData("empty-framework")]
    public async Task FrozenMsBuildEvidence_SdkAnnotatedReferencesRemainConservativeAsync(string route)
    {
        string root = Path.Combine(Path.GetTempPath(), "frozen-msbuild-annotated-" + Guid.NewGuid().ToString("N"));
        string archive = Path.Combine(root, FrozenMsBuildRoots[0], "fixture");
        Directory.CreateDirectory(archive);
        try
        {
            string frozen = Path.Combine(archive, "Captured.props");
            File.WriteAllText(frozen, "<Project />");
            string bridge = Path.Combine(root, "Bridge.csproj");
            string selected = route == "empty-framework" ? string.Empty : "net9.0";
            new XDocument(new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"),
                new XElement("PropertyGroup", new XElement("TargetFrameworks", "net10.0;net9.0")),
                route is "framework" or "empty-framework" ? new XElement("Import", new XAttribute("Project", frozen),
                    new XAttribute("Condition", "'$(SelectFrozen)' == 'true' and '$(TargetFramework)' == '" + selected + "'")) : null)).Save(bridge);
            XElement project = new("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"),
                new XElement("PropertyGroup", new XElement("TargetFramework", "net10.0")),
                new XElement("ItemGroup", new XElement("ProjectReference", new XAttribute("Include", bridge)),
                    route == "custom-addition" ? new XElement("AnnotatedProjects", new XAttribute("Include", frozen)) : null),
                new XElement("Target", new XAttribute("Name", "InspectReferences"), new XAttribute("BeforeTargets", "Build"),
                    new XElement("MSBuild", new XAttribute("Projects", "@(AnnotatedProjects->'%(FullPath)')"),
                        new XAttribute("Properties", "TargetFramework=%(AnnotatedProjects.NearestTargetFramework);SelectFrozen=true"))));
            new XDocument(project).Save(Path.Combine(root, "Active.csproj"));
            if (route == "benign") await VerifyFrozenEvidenceNotBuildInputAsync(root, "Active.csproj").ConfigureAwait(true);
            else
            {
                Exception? error = await Record.ExceptionAsync(() => VerifyFrozenEvidenceNotBuildInputAsync(root, "Active.csproj")).ConfigureAwait(true);
                error.ShouldBeOfType<Shouldly.ShouldAssertException>().Message.ShouldContain(frozen);
            }
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    /// <summary>Testing-platform framework batching preserves each identity and refuses custom metadata.</summary>
    /// <param name="route">The declared framework list and metadata control.</param>
    [Theory]
    [InlineData("single")]
    [InlineData("multiple")]
    [InlineData("empty")]
    [InlineData("benign")]
    [InlineData("unknown")]
    public async Task FrozenMsBuildEvidence_QualifiedItemIdentityPreservesFrameworkBatchingAsync(string route)
    {
        string root = Path.Combine(Path.GetTempPath(), "frozen-msbuild-framework-identity-" + Guid.NewGuid().ToString("N"));
        string archive = Path.Combine(root, FrozenMsBuildRoots[0], "fixture");
        Directory.CreateDirectory(archive);
        try
        {
            string frozen = Path.Combine(archive, "Captured.props");
            File.WriteAllText(frozen, "<Project />");
            string bridge = Path.Combine(root, "Bridge.csproj");
            new XDocument(new XElement("Project", route == "benign" ? null : new XElement("Import",
                new XAttribute("Project", frozen), new XAttribute("Condition", "'$(TargetFramework)' == 'net9.0'")))).Save(bridge);
            string frameworks = route == "empty" ? string.Empty : route == "single" ? "net9.0" : "net10.0;net9.0";
            string metadata = route == "unknown" ? "UnknownCustomMetadata" : "Identity";
            new XDocument(new XElement("Project",
                new XElement("PropertyGroup", new XElement("TargetFrameworks", frameworks)),
                new XElement("ItemGroup", new XElement("_TargetFramework", new XAttribute("Include", "$(TargetFrameworks)")),
                    new XElement("_ProjectToTestWithTFM", new XAttribute("Include", bridge),
                        new XAttribute("Properties", "TargetFramework=%(_TargetFramework." + metadata + ")"))),
                new XElement("Target", new XAttribute("Name", "Build"),
                    new XElement("MSBuild", new XAttribute("Projects", "@(_ProjectToTestWithTFM)"),
                        new XAttribute("Condition", "'$(TargetFrameworks)' != ''")))))
                .Save(Path.Combine(root, "Active.csproj"));
            if (route is "empty" or "benign") await VerifyFrozenEvidenceNotBuildInputAsync(root, "Active.csproj").ConfigureAwait(true);
            else
            {
                Exception? error = await Record.ExceptionAsync(() => VerifyFrozenEvidenceNotBuildInputAsync(root, "Active.csproj")).ConfigureAwait(true);
                error.ShouldBeOfType<Shouldly.ShouldAssertException>().Message.ShouldContain(route == "unknown"
                    ? "Unrecognized executable task batching metadata" : frozen);
            }
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    /// <summary>Identity cycles and unproven wildcard identities cannot become task property values.</summary>
    /// <param name="wildcard">Whether the unproven identity is a wildcard rather than a metadata cycle.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FrozenMsBuildEvidence_QualifiedIdentityUnprovenItemsRemainFailClosed(bool wildcard)
    {
        var declarations = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["?$MSBuildToolsPath"] = [Path.Combine(Path.GetTempPath(), "installed-sdk")],
            ["@_TargetFramework"] = ["@(_FrameworkAlias)"],
            ["@_FrameworkAlias"] = [wildcard ? "Bridge*.csproj" : "%(_TargetFramework.Identity)"],
        };
        XElement task = new("MSBuild", new XAttribute("Properties", "TargetFramework=%(_TargetFramework.Identity)"));
        Should.Throw<Shouldly.ShouldAssertException>(() => FrozenTaskProperties(task, Path.Combine(Path.GetTempPath(), "Custom.targets"),
            declarations, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)).ToArray());
    }

    /// <summary>Preserves executable contexts reproduced by the independent closing review.</summary>
    /// <param name="route">The executable edge or provenance boundary being inspected.</param>
    [Theory]
    [InlineData("workingdirectory")]
    [InlineData("comma-properties")]
    [InlineData("task-property-output")]
    [InlineData("boundary-comment-spoof")]
    [InlineData("targettime-reference-configuration")]
    [InlineData("import-fullpath")]
    [InlineData("exec-environment")]
    [InlineData("msbuild-dll")]
    [InlineData("transform-semicolon-items")]
    [InlineData("exec-directory")]
    [InlineData("environment-command-precedence")]
    public async Task FrozenMsBuildEvidence_ClosingExecutableContextsAsync(string route)
    {
        string root = Path.Combine(Path.GetTempPath(), "frozen-closing-context-" + Guid.NewGuid().ToString("N"));
        string archive = Path.Combine(root, FrozenMsBuildRoots[0], "fixture");
        Directory.CreateDirectory(archive);
        try
        {
            string frozen = Path.Combine(archive, "Captured.props");
            File.WriteAllText(frozen, "<Project />");
            string active = Path.Combine(root, "Active.csproj");
            File.WriteAllText(active, "<Project />");
            XElement project = new("Project");
            XElement target = new("Target", new XAttribute("Name", "Build"));
            string bridge = Path.Combine(root, "Bridge.csproj");
            XElement import = new("Import", new XAttribute("Project", frozen));
            XElement exec = new("Exec", new XAttribute("Command", "dotnet msbuild Bridge.csproj -t:Build -nologo"));
            if (route is "comma-properties" or "exec-environment" or "environment-command-precedence")
                import.SetAttributeValue("Condition", "'$(SelectFrozen)' == 'true'");
            if (route is "workingdirectory" or "exec-directory")
            {
                Directory.CreateDirectory(Path.Combine(root, "bridge"));
                bridge = Path.Combine(root, "bridge", "Bridge.csproj");
                if (route == "workingdirectory") exec.SetAttributeValue("WorkingDirectory", Path.GetDirectoryName(bridge));
                else exec.SetAttributeValue("Command", "dotnet build bridge --no-restore -nologo");
            }
            if (route == "comma-properties") exec.SetAttributeValue("Command", exec.Attribute("Command")!.Value + " -p:SelectFrozen=true,Other=false");
            if (route is "exec-environment" or "environment-command-precedence") exec.SetAttributeValue("EnvironmentVariables", "SelectFrozen=true");
            if (route == "environment-command-precedence") exec.SetAttributeValue("Command", exec.Attribute("Command")!.Value + " -p:SelectFrozen=false");
            if (route is "boundary-comment-spoof" or "msbuild-dll")
            {
                string sdk = (await EvaluateFrozenBuildProjectAsync(active, new(StringComparer.OrdinalIgnoreCase), "-getProperty:MSBuildToolsPath").ConfigureAwait(true)).Trim();
                if (route == "boundary-comment-spoof") project.Add(new XComment("\n======================\n" + Path.Combine(sdk, "Microsoft.Common.CurrentVersion.targets") + "\n======================\n"));
                else exec.SetAttributeValue("Command", "dotnet exec \"" + Path.Combine(sdk, "MSBuild.dll") + "\" Bridge.csproj -t:Build -nologo");
            }
            if (route == "task-property-output")
            {
                target.Add(new XElement("CreateProperty", new XAttribute("Value", "Bridge.csproj"),
                    new XElement("Output", new XAttribute("TaskParameter", "Value"), new XAttribute("PropertyName", "ChildProject"))),
                    new XElement("MSBuild", new XAttribute("Projects", "$(ChildProject)"), new XAttribute("Targets", "Build")));
            }
            else if (route == "targettime-reference-configuration")
            {
                project.SetAttributeValue("Sdk", "Microsoft.NET.Sdk");
                project.Add(new XElement("PropertyGroup", new XElement("TargetFramework", "net10.0")));
                import.SetAttributeValue("Condition", "'$(Configuration)' == 'Selected'");
                target.SetAttributeValue("BeforeTargets", "AssignProjectConfiguration");
                target.Add(new XElement("ItemGroup", new XElement("ProjectReference", new XAttribute("Include", "Bridge.csproj"),
                    new XElement("SetConfiguration", "Configuration=Selected"), new XElement("SkipGetTargetFrameworkProperties", "true"))));
            }
            else if (route == "import-fullpath")
            {
                Directory.CreateDirectory(Path.Combine(root, "imported"));
                string imported = Path.Combine(root, "imported", "Guard.targets");
                new XDocument(new XElement("Project", new XElement("Target", new XAttribute("Name", "Build"),
                    new XElement("MSBuild", new XAttribute("Projects", "$(MSBuildThisFileFullPath).csproj"), new XAttribute("Targets", "Build"))))).Save(imported);
                bridge = imported + ".csproj";
                project.Add(new XElement("Import", new XAttribute("Project", imported)));
            }
            else if (route == "transform-semicolon-items")
            {
                File.WriteAllText(Path.Combine(root, "Safe.csproj"), "<Project />");
                project.Add(new XElement("ItemGroup", new XElement("Aliases", new XAttribute("Include", "dummy/Bridge.txt;dummy/Safe.txt"))));
                target.Add(new XElement("MSBuild", new XAttribute("Projects", "@(Aliases->'%(Filename).csproj')"), new XAttribute("Targets", "Build")));
            }
            else target.Add(exec);
            new XDocument(new XElement("Project", import)).Save(bridge);
            if (route != "import-fullpath") project.Add(target);
            new XDocument(project).Save(active);
            if (route == "environment-command-precedence")
                await VerifyFrozenEvidenceNotBuildInputAsync(root, active).ConfigureAwait(true);
            else
                (await Record.ExceptionAsync(() => VerifyFrozenEvidenceNotBuildInputAsync(root, active)).ConfigureAwait(true))
                    .ShouldBeOfType<Shouldly.ShouldAssertException>();
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static void VerifyFrozenMsBuildInventory(string root, string[] paths, int count, string expectedHash)
    {
        paths.Length.ShouldBe(count, "The exact frozen MSBuild evidence membership changed.");
        foreach (string path in paths)
        {
            File.GetAttributes(Path.Combine(root, path)).HasFlag(FileAttributes.ReparsePoint)
                .ShouldBeFalse("Frozen evidence must retain regular files, not substituted links.");
            Path.GetFullPath(Path.Combine(root, path)).ShouldBe(ResolvePhysicalBuildPath(Path.Combine(root, path)),
                "Frozen evidence parent directories cannot be substituted with links.");
        }
        string inventory = string.Concat(paths.Order(StringComparer.Ordinal).Select(path =>
            path + " " + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root, path))))
                .ToLowerInvariant() + "\n"));
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(inventory))).ToLowerInvariant()
            .ShouldBe(expectedHash, "Frozen MSBuild evidence paths or original raw bytes changed.");
    }

    private static string[] GetTrackedMsBuildFiles(string root)
    {
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo("git")
            {
                WorkingDirectory = root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            },
        };
        PackagingRepositoryPaths.RemoveRepositorySelectors(process.StartInfo);
        foreach (string argument in new[] { "ls-files", "-z", "--", "*.csproj", "*.props", "*.targets" })
        {
            process.StartInfo.ArgumentList.Add(argument);
        }
        process.Start().ShouldBeTrue();
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit(30_000).ShouldBeTrue("MSBuild input discovery timed out.");
        process.ExitCode.ShouldBe(0, error);
        return output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }

    private static async Task VerifyFrozenEvidenceNotBuildInputAsync(
        string root, string project, HashSet<string>? visited = null)
    {
        visited ??= new HashSet<string>(StringComparer.Ordinal);
        // Evaluate the supported local and CI modes; this performs no Release build or restore.
        foreach ((string configuration, string source) in new[] { ("Debug", "true"), ("Release", "false") })
        {
            var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Configuration"] = configuration,
                ["UseHexalithProjectReferences"] = source,
            };
            await InspectFrozenBuildGraphAsync(root, Path.GetFullPath(Path.Combine(root, project)), properties, visited)
                .ConfigureAwait(true);
        }
    }

    private static async Task InspectFrozenBuildGraphAsync(
        string root, string project, Dictionary<string, string> properties, HashSet<string> visited)
    {
        AssertNotFrozenBuildInput(root, project, project);
        string key = ResolvePhysicalBuildPath(project) + "|" + string.Join(";",
            properties.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).Select(pair => pair.Key + "=" + pair.Value));
        if (!visited.Add(key)) return;
        string preprocessed = Path.Combine(Path.GetTempPath(), "eventstore-msbuild-inputs-" + Guid.NewGuid().ToString("N") + ".xml");
        try
        {
            await EvaluateFrozenBuildProjectAsync(project, properties, "-preprocess:" + preprocessed).ConfigureAwait(true);
            XDocument document = XDocument.Load(preprocessed);
            var owners = new Dictionary<XElement, string>();
            var sourceComments = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            string owner = project;
            foreach (XNode node in document.DescendantNodes())
            {
                if (node is XElement owned) owners[owned] = owner;
                if (node is not XComment comment) continue;
                // MSBuild boundary records start with a separator and put an absolute source
                // path immediately before their closing separator. Ordinary comments are data.
                string[] lines = comment.Value.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToArray();
                static bool Separator(string line) => line.Length >= 10 && line.All(character => character == '=');
                if (lines.Length >= 3 && Separator(lines[0]) && Separator(lines[^1]) && Path.IsPathFullyQualified(lines[^2]))
                {
                    // A literal source comment is not a preprocessor-generated boundary,
                    // even when it names a real file under the installed SDK.
                    if (!sourceComments.TryGetValue(owner, out HashSet<string>? comments))
                    {
                        comments = XDocument.Load(owner).DescendantNodes().OfType<XComment>()
                            .Select(value => value.Value.Replace("\r\n", "\n", StringComparison.Ordinal).Trim())
                            .ToHashSet(StringComparer.Ordinal);
                        sourceComments.Add(owner, comments);
                    }
                    if (comments.Contains(comment.Value.Replace("\r\n", "\n", StringComparison.Ordinal).Trim())) continue;
                    AssertNotFrozenBuildInput(root, project, lines[^2]);
                    owner = lines[^2];
                }
            }

            // Also inspect executable target bodies: evaluation-time items omit references
            // added by targets and MSBuild task invocations. No historical paths are permitted
            // on these executable edges, even when execution would occur after evaluation.
            var declarations = document.Descendants().Where(element => element.Parent?.Name.LocalName == "PropertyGroup")
                .Select(element => (Name: "$" + element.Name.LocalName, Value: BindDefiningDirectory(element.Value, owners[element])))
                .Concat(document.Descendants().Where(element => element.Parent?.Name.LocalName == "ItemGroup")
                    .SelectMany(element => element.Attributes().Where(attribute => attribute.Name.LocalName is "Include" or "Update")
                        .Select(attribute => (Name: "@" + element.Name.LocalName, Value: BindDefiningDirectory(attribute.Value, owners[element])))))
                .GroupBy(declaration => declaration.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Select(declaration => declaration.Value).Distinct().ToArray(), StringComparer.OrdinalIgnoreCase);
            declarations["$MSBuildProjectFullPath"] = [project];
            declarations["$MSBuildProjectDirectory"] = [Path.GetDirectoryName(project)!];
            declarations["$MSBuildThisFileDirectory"] = [Path.GetDirectoryName(project)! + Path.DirectorySeparatorChar];
            string[] unresolvedProperties = declarations.Values.SelectMany(values => values)
                .Concat(document.Descendants().Where(element => element.Name.LocalName is "ProjectReference" or "MSBuild" or "Exec")
                    .SelectMany(element => element.Attributes().Select(attribute => attribute.Value)))
                .SelectMany(value => Regex.Matches(value, @"\$\((?<name>[A-Za-z_][A-Za-z0-9_.-]*)\)", RegexOptions.CultureInvariant)
                    .Select(match => match.Groups["name"].Value))
                .Concat(declarations.Keys.Where(name => name.StartsWith('$')).Select(name => name[1..]))
                .Append("MSBuildToolsPath")
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (unresolvedProperties.Length > 0)
            {
                unresolvedProperties.Length.ShouldBeLessThanOrEqualTo(4096, "Executable property discovery exceeded its bounded inspection budget.");
                string result = await EvaluateFrozenBuildProjectAsync(project, properties,
                    "-getProperty:" + string.Join(',', unresolvedProperties.Append("MSBuildProjectFullPath"))).ConfigureAwait(true);
                using JsonDocument evaluated = JsonDocument.Parse(result);
                foreach (JsonProperty property in evaluated.RootElement.GetProperty("Properties").EnumerateObject())
                {
                    declarations["?$" + property.Name] = [property.Value.GetString()!];
                    declarations.TryAdd("$" + property.Name, [property.Value.GetString()!]);
                }
            }
            foreach ((string name, string value) in properties) declarations["$" + name] = [value];
            foreach (XElement produced in document.Descendants().Where(element => element.Name.LocalName == "Output"
                && element.Attribute("PropertyName") is not null && !IsFrozenSdkOwner(owners[element], declarations)))
            {
                // Evaluation cannot prove a value produced by a task that has not run.
                // Preserve the expression instead of treating an evaluated empty value as absence.
                string name = produced.Attribute("PropertyName")!.Value;
                declarations["$" + name] = declarations["?$" + name] = ["$(" + name + ")"];
            }
            // Query real evaluated references once; this executes no targets.
            string output = await EvaluateFrozenBuildProjectAsync(project, properties,
                "-getItem:ProjectReference", "-getProperty:TargetFrameworks,TargetFramework").ConfigureAwait(true);
            using JsonDocument evaluation = JsonDocument.Parse(output);
            JsonElement evaluatedReferences = evaluation.RootElement.GetProperty("Items").GetProperty("ProjectReference");
            evaluatedReferences.GetArrayLength().ShouldBeLessThanOrEqualTo(4096, "Evaluated reference inventory exceeded its bounded inspection budget.");
            // SDK targets synthesize ProjectReference aliases from task outputs.
            // Those hypothetical outputs are not source declarations. Their real
            // evaluated references are inspected below; keep every custom/source
            // declaration here, including references added inside custom targets.
            declarations["@ProjectReference"] = document.Descendants()
                .Where(element => element.Name.LocalName == "ProjectReference" && element.Parent?.Name.LocalName == "ItemGroup"
                    && !IsFrozenSdkOwner(owners[element], declarations))
                .SelectMany(element => element.Attributes().Where(attribute => attribute.Name.LocalName is "Include" or "Update")
                    .Where(attribute => attribute.Name.LocalName != "Update" || !attribute.Value.Trim().Equals("@(ProjectReference)", StringComparison.OrdinalIgnoreCase))
                    .Select(attribute => BindDefiningDirectory(attribute.Value, owners[element])))
                .Concat(evaluatedReferences.EnumerateArray().Select(reference => reference.GetProperty("FullPath").GetString()!))
                .Distinct().ToArray();
            // This demonstrated SDK output decorates ProjectReferences; do not
            // invent intermediate target outputs. Its conservative destination
            // set is every actual/source reference plus every custom addition.
            if (document.Descendants().Any(element => element.Name.LocalName == "Output"
                && element.Attribute("ItemName")?.Value == "AnnotatedProjects"
                && element.Attribute("TaskParameter")?.Value == "AssignedProjects"
                && element.Parent?.Name.LocalName == "GetReferenceNearestTargetFrameworkTask"
                && IsFrozenSdkOwner(owners[element], declarations)))
            {
                declarations["!SDKAnnotatedProjects"] = ["true"];
                declarations["@AnnotatedProjects"] = declarations["@ProjectReference"]
                    .Concat(document.Descendants().Where(element => element.Name.LocalName == "AnnotatedProjects"
                        && element.Parent?.Name.LocalName == "ItemGroup" && !IsFrozenSdkOwner(owners[element], declarations))
                        .SelectMany(element => element.Attributes().Where(attribute => attribute.Name.LocalName is "Include" or "Update")
                            .Where(attribute => attribute.Name.LocalName != "Update" || !attribute.Value.Trim().Equals("@(AnnotatedProjects)", StringComparison.OrdinalIgnoreCase))
                            .Select(attribute => BindDefiningDirectory(attribute.Value, owners[element]))))
                    .Distinct().ToArray();
                declarations["?@AnnotatedProjects"] = declarations["@ProjectReference"];
            }
            foreach (XElement item in document.Descendants().Where(element => element.Parent?.Name.LocalName is "ItemGroup" or "ItemDefinitionGroup"))
            {
                foreach (XElement metadata in item.Elements())
                {
                    string name = "%" + item.Name.LocalName + "." + metadata.Name.LocalName;
                    string value = BindDefiningDirectory(metadata.Value, owners[metadata]);
                    declarations[name] = declarations.TryGetValue(name, out string[]? prior) ? [.. prior, value] : [value];
                }
                foreach (XAttribute original in item.Attributes().Where(attribute => new[] { "OriginalItemSpec", "Properties", "AdditionalProperties", "GlobalPropertiesToRemove", "UndefineProperties", "SetConfiguration", "SetPlatform", "SetTargetFramework" }
                    .Contains(attribute.Name.LocalName, StringComparer.OrdinalIgnoreCase)))
                {
                    string name = "%" + item.Name.LocalName + "." + original.Name.LocalName;
                    string value = BindDefiningDirectory(original.Value, owners[item]);
                    declarations[name] = declarations.TryGetValue(name, out string[]? prior) ? [.. prior, value] : [value];
                }
            }
            var edges = document.Descendants().Where(element => element.Name.LocalName is "ProjectReference" or "MSBuild")
                .Select(element => (Element: element, Owner: owners[element])).ToList();
            foreach (XElement exec in document.Descendants().Where(element => element.Name.LocalName == "Exec"))
            {
                if (IsFrozenSdkOwner(owners[exec], declarations) || exec.Attribute("Command") is not { } command) continue;
                string[] commands = ExpandFrozenBuildExpressions(BindDefiningDirectory(command.Value, owners[exec]),
                    declarations, new HashSet<string>(StringComparer.OrdinalIgnoreCase)).Distinct().Take(257).ToArray();
                commands.Length.ShouldBeLessThanOrEqualTo(256, "Executable command expansion exceeded its bounded inspection budget.");
                foreach (string expanded in commands)
                {
                    if (FrozenDirectExecTask(expanded) is { } task)
                    {
                        foreach (string name in new[] { "WorkingDirectory", "EnvironmentVariables" })
                            if (exec.Attribute(name) is { } control) task.SetAttributeValue(name, control.Value);
                        edges.Add((task, owners[exec]));
                    }
                    edges.Count.ShouldBeLessThanOrEqualTo(16384, "Executable project edge discovery exceeded its bounded inspection budget.");
                }
            }
            foreach ((XElement element, string definingFile) in edges)
            {
                foreach (XAttribute attribute in element.Attributes().Where(attribute => attribute.Name.LocalName is "Include" or "Update" or "Projects"))
                {
                    string[] expressions = ExpandFrozenBuildExpressions(BindDefiningDirectory(attribute.Value, definingFile), declarations, new HashSet<string>(StringComparer.OrdinalIgnoreCase))
                        .Distinct().Take(257).ToArray();
                    expressions.Length.ShouldBeLessThanOrEqualTo(256, "Executable project input expansion exceeded its bounded inspection budget.");
                    foreach (string raw in expressions)
                    {
                        string expression = DecodeFrozenBuildValue(raw);
                        string normalized = expression.Replace('\\', '/');
                        const string marker = "_bmad-output/implementation-artifacts/evidence";
                        int index = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                        bool archived = index >= 0 && (index + marker.Length == normalized.Length || normalized[index + marker.Length] == '/');
                        archived.ShouldBeFalse($"Active project {project} consumes frozen evidence as a build input: {expression}");
                        if (expression.Contains("->", StringComparison.Ordinal))
                            false.ShouldBeTrue("Active project " + project + " potentially consumes frozen evidence as a build input: unresolved executable item transform " + expression);
                        if (expression.Contains("$(", StringComparison.Ordinal) || expression.Contains("@(", StringComparison.Ordinal)
                            || expression.Contains("%(", StringComparison.Ordinal))
                        {
                            IsFrozenSdkOwner(definingFile, declarations).ShouldBeTrue(
                                "Unresolved executable project destination in custom input: " + expression
                                    + " (owner " + definingFile + ", declaration " + attribute.Value + ")");
                            continue;
                        }
                        foreach (string path in expression.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        {
                            string[] bases = element.Attribute("WorkingDirectory") is { } workingDirectory
                                ? ExpandFrozenBuildExpressions(BindDefiningDirectory(workingDirectory.Value, definingFile), declarations,
                                    new HashSet<string>(StringComparer.OrdinalIgnoreCase)).Distinct().Take(257).ToArray()
                                : [Path.GetDirectoryName(project)!];
                            bases.Length.ShouldBeLessThanOrEqualTo(256, "Executable working-directory expansion exceeded its bounded inspection budget.");
                            foreach (string candidateBase in bases)
                            {
                                (candidateBase.Contains("$(", StringComparison.Ordinal) || candidateBase.Contains("@(", StringComparison.Ordinal)
                                    || candidateBase.Contains("%(", StringComparison.Ordinal)).ShouldBeFalse("Unproven executable working directory.");
                                string directory = Path.GetFullPath(DecodeFrozenBuildValue(candidateBase.Length == 0 ? "." : candidateBase), Path.GetDirectoryName(project)!);
                                Directory.Exists(directory).ShouldBeTrue("Unproven executable working directory.");
                                foreach (string candidate in ExpandFrozenBuildPaths(path, directory))
                                {
                                    string referenced = candidate;
                                    AssertNotFrozenBuildInput(root, project, referenced);
                                    if (Directory.Exists(referenced))
                                    {
                                        string[] selected = Directory.EnumerateFiles(referenced).Where(value => new[] { ".csproj", ".vbproj", ".fsproj", ".proj", ".slnx", ".sln" }
                                            .Contains(Path.GetExtension(value), StringComparer.OrdinalIgnoreCase)).Take(2).ToArray();
                                        (selected.Length == 1 && !Path.GetExtension(selected[0]).StartsWith(".sln", StringComparison.OrdinalIgnoreCase))
                                            .ShouldBeTrue("Unproven executable directory project selection.");
                                        referenced = selected[0];
                                        AssertNotFrozenBuildInput(root, project, referenced);
                                    }
                                    if (File.Exists(referenced))
                                    {
                                        foreach (Dictionary<string, string> child in FrozenReferencedTaskProperties(element, definingFile, declarations, properties))
                                        {
                                            await InspectFrozenBuildGraphAsync(root, referenced, child, visited).ConfigureAwait(true);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            string frameworks = evaluation.RootElement.GetProperty("Properties").GetProperty("TargetFrameworks").GetString()!;
            if (!properties.ContainsKey("TargetFramework"))
            {
                foreach (string framework in frameworks.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var inner = new Dictionary<string, string>(properties, StringComparer.OrdinalIgnoreCase) { ["TargetFramework"] = framework };
                    await InspectFrozenBuildGraphAsync(root, project, inner, visited).ConfigureAwait(true);
                }
            }
            foreach (JsonElement reference in evaluatedReferences.EnumerateArray())
            {
                string referenced = reference.GetProperty("FullPath").GetString()!;
                AssertNotFrozenBuildInput(root, project, referenced);
                var child = new Dictionary<string, string>(properties, StringComparer.OrdinalIgnoreCase);
                // These are the MSBuild ProjectReference protocol's global-property controls.
                foreach (string metadata in new[] { "GlobalPropertiesToRemove", "UndefineProperties" })
                {
                    if (reference.TryGetProperty(metadata, out JsonElement removed))
                    {
                        foreach (string name in removed.GetString()!.Split(';', StringSplitOptions.RemoveEmptyEntries)) child.Remove(name);
                    }
                }
                foreach (string metadata in new[] { "Properties", "AdditionalProperties", "SetConfiguration", "SetPlatform", "SetTargetFramework" })
                {
                    if (!reference.TryGetProperty(metadata, out JsonElement extra)) continue;
                    foreach (string assignment in extra.GetString()!.Split(';', StringSplitOptions.RemoveEmptyEntries))
                    {
                        int separator = assignment.IndexOf('=');
                        (separator > 0).ShouldBeTrue("Unrecognized ProjectReference global-property metadata.");
                        child[assignment[..separator]] = assignment[(separator + 1)..];
                    }
                }
                await InspectFrozenBuildGraphAsync(root, referenced, child, visited).ConfigureAwait(true);
            }
        }
        finally
        {
            File.Delete(preprocessed);
        }
    }

    private static string BindDefiningDirectory(string value, string definingFile)
        => value.Replace("$(MSBuildThisFileFullPath)", definingFile, StringComparison.OrdinalIgnoreCase)
            .Replace("$(MSBuildThisFileDirectory)", Path.GetDirectoryName(definingFile)! + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);

    private static bool IsFrozenSdkOwner(string definingFile, Dictionary<string, string[]> declarations)
        => Path.GetFullPath(definingFile).StartsWith(
            Path.TrimEndingDirectorySeparator(declarations["?$MSBuildToolsPath"][0]) + Path.DirectorySeparatorChar,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static string DecodeFrozenBuildValue(string value)
    {
        Regex.IsMatch(value, @"%(?![0-9a-fA-F]{2}|\()", RegexOptions.CultureInvariant)
            .ShouldBeFalse("Unsupported executable MSBuild escaping.");
        string decoded = Regex.Replace(value, @"%(?<hex>[0-9a-fA-F]{2})", match =>
            ((char)Convert.ToInt32(match.Groups["hex"].Value, 16)).ToString(), RegexOptions.CultureInvariant);
        decoded.Any(char.IsControl).ShouldBeFalse("Unsupported executable MSBuild control character.");
        return decoded;
    }

    private static XElement? FrozenDirectExecTask(string command)
    {
        // Recognize only an explicit direct .NET build invocation. Never invoke a
        // shell, expand shell expressions, or infer a destination from shell code.
        if (!Regex.IsMatch(command, @"\A\s*(?:""(?:[^""]*[/\\])?(?:dotnet|msbuild)(?:\.exe)?""|(?:[^\s""']*[/\\])?(?:dotnet|msbuild)(?:\.exe)?)(?=\s|\z)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) return null;
        command.Length.ShouldBeLessThanOrEqualTo(65536, "Direct executable command exceeds its bounded inspection budget.");
        var arguments = new List<string>();
        int position = 0;
        foreach (Match token in Regex.Matches(command, @"\G\s*(?:""(?<arg>[^""]*)""|'(?<arg>[^']*)'|(?<arg>[^\s""';&|<>`]+))(?=\s|\z)", RegexOptions.CultureInvariant))
        {
            arguments.Add(token.Groups["arg"].Value);
            position = token.Index + token.Length;
        }
        (position > 0 && string.IsNullOrWhiteSpace(command[position..])).ShouldBeTrue("Unproven direct executable command syntax.");
        arguments.Count.ShouldBeLessThanOrEqualTo(256, "Direct executable arguments exceed their bounded inspection budget.");
        int first = 1;
        if (Path.GetFileNameWithoutExtension(arguments[0].Replace('\\', '/')).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            if (arguments.Count >= 3 && arguments[1] == "exec"
                && Path.GetFileName(arguments[2].Replace('\\', '/')).Equals("MSBuild.dll", StringComparison.OrdinalIgnoreCase)) first = 3;
            else
            {
                if (arguments.Count < 2 || arguments[1] is not ("msbuild" or "build" or "restore" or "publish" or "pack" or "test" or "run")) return null;
                first = 2;
            }
        }
        string? project = null;
        var properties = new List<string>();
        for (int index = first; index < arguments.Count; index++)
        {
            string argument = arguments[index];
            Match property = Regex.Match(argument, @"\A(?:--property:|[-/](?:p|property):)(?<value>.+)\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (property.Success) properties.Add(property.Groups["value"].Value.Replace(',', ';'));
            else if (argument == "--project" && index + 1 < arguments.Count) project = arguments[++index];
            else if (!argument.StartsWith('-') && (!argument.StartsWith('/') || Path.IsPathFullyQualified(argument) && (File.Exists(argument) || Directory.Exists(argument))))
            {
                (project is null).ShouldBeTrue("Unproven multiple direct executable destinations.");
                project = argument;
            }
        }
        (!string.IsNullOrWhiteSpace(project)).ShouldBeTrue("Unproven direct executable project destination.");
        return new XElement("MSBuild", new XAttribute("Projects", project!), new XAttribute("Properties", string.Join(';', properties)));
    }

    private static IEnumerable<Dictionary<string, string>> FrozenReferencedTaskProperties(
        XElement element, string definingFile, Dictionary<string, string[]> declarations, Dictionary<string, string> inherited)
    {
        const string nearest = "TargetFramework=%(AnnotatedProjects.NearestTargetFramework)";
        string[] assignments = (element.Attribute("Properties")?.Value ?? string.Empty).Split(';', StringSplitOptions.TrimEntries);
        bool protocol = declarations.ContainsKey("!SDKAnnotatedProjects")
            && element.Attribute("Projects")?.Value == "@(AnnotatedProjects->'%(FullPath)')"
            && assignments.Contains(nearest, StringComparer.Ordinal);
        XElement task = element;
        if (protocol && !declarations.ContainsKey("%AnnotatedProjects.NearestTargetFramework"))
        {
            task = new XElement(element);
            task.SetAttributeValue("Properties", string.Join(';', assignments.Where(value => value != nearest)));
        }
        int variants = 0;
        foreach (Dictionary<string, string> child in FrozenTaskProperties(task, definingFile, declarations, inherited))
        {
            (++variants <= 256).ShouldBeTrue("Referenced framework contexts exceeded their bounded inspection budget.");
            yield return child;
            if (!protocol) continue;
            // Preserve all other effective properties. Removing the framework
            // selects the real outer context and existing traversal enumerates
            // every declared framework. Also inspect an explicitly empty value.
            if (child.ContainsKey("TargetFramework"))
            {
                var outer = new Dictionary<string, string>(child, StringComparer.OrdinalIgnoreCase);
                outer.Remove("TargetFramework");
                (++variants <= 256).ShouldBeTrue("Referenced framework contexts exceeded their bounded inspection budget.");
                yield return outer;
            }
            var empty = new Dictionary<string, string>(child, StringComparer.OrdinalIgnoreCase) { ["TargetFramework"] = string.Empty };
            (++variants <= 256).ShouldBeTrue("Referenced framework contexts exceeded their bounded inspection budget.");
            yield return empty;
        }
    }

    private static IEnumerable<Dictionary<string, string>> FrozenTaskProperties(
        XElement element, string definingFile, Dictionary<string, string[]> declarations, Dictionary<string, string> inherited)
    {
        // Installed SDK task bodies contain restore/publish self-invocations with
        // target-time batching and property functions. Their actual evaluated
        // references/frameworks are inspected below; do not simulate hypothetical
        // SDK target execution. Static destinations still receive the same path
        // checks above. Repository and custom imported task edges carry their
        // complete effective properties into every traversed bridge.
        if (IsFrozenSdkOwner(definingFile, declarations))
            return [new(inherited, StringComparer.OrdinalIgnoreCase)];
        IEnumerable<Dictionary<string, string>> variants = [new(inherited, StringComparer.OrdinalIgnoreCase)];
        foreach (string control in new[] { "EnvironmentVariables", "RemoveProperties", "GlobalPropertiesToRemove", "UndefineProperties", "Properties", "AdditionalProperties", "SetConfiguration", "SetPlatform", "SetTargetFramework" })
        {
            var controls = new List<string[]>();
            if (element.Attribute(control) is { } attribute) controls.Add([attribute.Value]);
            if (element.Name.LocalName == "ProjectReference")
                foreach (XElement metadataValue in element.Elements().Where(value => value.Name.LocalName == control)) controls.Add([metadataValue.Value]);
            // MSBuild carries effective item metadata into task invocations even
            // when no matching task attribute is present.
            string metadata = control == "RemoveProperties" ? "GlobalPropertiesToRemove" : control;
            foreach (Match item in Regex.Matches(element.Attribute("Projects")?.Value ?? string.Empty,
                @"@\((?<name>[A-Za-z_][A-Za-z0-9_.-]*)", RegexOptions.CultureInvariant))
                if (FrozenBuildMetadata(item.Groups["name"].Value, metadata, declarations,
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase)) is { } effective) controls.Add(effective);
            if (controls.Count == 0) continue;
            foreach (string[] inputs in controls)
            {
                string[] values = inputs.SelectMany(value => ExpandFrozenBuildExpressions(BindDefiningDirectory(value, definingFile), declarations,
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase))).Distinct().Take(257).ToArray();
                values.Length.ShouldBeLessThanOrEqualTo(256, "Executable task property expansion exceeded its bounded inspection budget.");
                var next = new List<Dictionary<string, string>>();
                foreach (Dictionary<string, string> variant in variants)
                {
                    foreach (string value in values)
                    {
                        value.Contains("%(", StringComparison.Ordinal).ShouldBeFalse("Unrecognized executable task batching metadata: "
                            + value + " (project " + declarations.GetValueOrDefault("$MSBuildProjectFullPath")?.FirstOrDefault() + ", owner " + definingFile
                                + ", control " + control + ", projects " + element.Attribute("Projects")?.Value + ")");
                        (value.Contains("$(", StringComparison.Ordinal) || value.Contains("@(", StringComparison.Ordinal))
                            .ShouldBeFalse("Unresolved executable task global-property metadata.");
                        var child = new Dictionary<string, string>(variant, StringComparer.OrdinalIgnoreCase);
                        foreach (string part in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        {
                            if (control is "RemoveProperties" or "GlobalPropertiesToRemove" or "UndefineProperties") child.Remove(DecodeFrozenBuildValue(part));
                            else
                            {
                                int separator = part.IndexOf('=');
                                (separator > 0).ShouldBeTrue("Unrecognized executable task global-property metadata: " + part);
                                child[DecodeFrozenBuildValue(part[..separator])] = DecodeFrozenBuildValue(part[(separator + 1)..]);
                            }
                        }
                        next.Add(child);
                        next.Count.ShouldBeLessThanOrEqualTo(256, "Executable task property expansion exceeded its bounded inspection budget.");
                    }
                }
                variants = next;
            }
        }
        return variants;
    }

    private static IEnumerable<string> ExpandFrozenBuildPaths(string expression, string directory)
    {
        string path = Path.GetFullPath(expression.Replace('\\', Path.DirectorySeparatorChar), directory);
        if (path.IndexOfAny(['*', '?']) < 0) { yield return path; yield break; }
        string anchor = Path.GetPathRoot(path)!;
        string[] parts = path[anchor.Length..].Split(Path.DirectorySeparatorChar);
        IEnumerable<string> paths = [anchor];
        int inspected = 0;
        foreach (string part in parts)
        {
            var next = new List<string>();
            foreach (string parent in paths)
            {
                IEnumerable<string> matches = part.IndexOfAny(['*', '?']) < 0 ? [Path.Combine(parent, part)]
                    : Directory.Exists(parent) ? Directory.EnumerateFileSystemEntries(parent, part) : [];
                foreach (string match in matches)
                {
                    (++inspected <= 4096).ShouldBeTrue("Executable project wildcard expansion exceeded its bounded inspection budget.");
                    next.Add(match);
                }
            }
            paths = next;
        }
        foreach (string result in paths) yield return result;
    }

    private static string[]? FrozenBuildMetadata(string item, string metadata,
        Dictionary<string, string[]> declarations, HashSet<string> visiting)
    {
        if (!visiting.Add(item)) return null;
        visiting.Count.ShouldBeLessThanOrEqualTo(64, "Executable item metadata alias expansion exceeded its bounded depth budget.");
        if (declarations.TryGetValue("%" + item + "." + metadata, out string[]? values)) return values;
        if (metadata.Equals("Identity", StringComparison.OrdinalIgnoreCase)
            && declarations.TryGetValue("@" + item, out string[]? identities))
        {
            // Qualified built-in Identity batches over individual declared items,
            // including each framework in an Include="$(TargetFrameworks)" list.
            // Unknown custom metadata still requires an explicit declaration.
            var expandedIdentities = new HashSet<string>(StringComparer.Ordinal);
            foreach (string identity in identities)
            {
                foreach (string expanded in ExpandFrozenBuildExpressions(identity, declarations,
                    new HashSet<string>(visiting, StringComparer.OrdinalIgnoreCase)))
                {
                    foreach (string value in expanded.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        value.IndexOfAny(['*', '?']).ShouldBe(-1, "Unproven executable item identity wildcard.");
                        expandedIdentities.Add(value);
                        expandedIdentities.Count.ShouldBeLessThanOrEqualTo(256, "Executable item identity expansion exceeded its bounded inspection budget.");
                    }
                }
            }
            return expandedIdentities.ToArray();
        }
        if (item.Equals("ProjectReference", StringComparison.OrdinalIgnoreCase))
        {
            // These are the already supported ProjectReference global-property controls.
            // Explicit source metadata above remains authoritative. Without an override,
            // preserve the current evaluated configuration/platform/framework; absence
            // of removal metadata removes nothing. Other custom metadata is unproven.
            string property = metadata.ToUpperInvariant() switch
            {
                "SETCONFIGURATION" => "Configuration",
                "SETPLATFORM" => "Platform",
                "SETTARGETFRAMEWORK" => "TargetFramework",
                "GLOBALPROPERTIESTOREMOVE" or "UNDEFINEPROPERTIES" => string.Empty,
                _ => "?",
            };
            if (property.Length == 0) return [string.Empty];
            if (property != "?") return declarations.TryGetValue("?$" + property, out string[]? inherited)
                ? inherited.Select(value => value.Length == 0 ? string.Empty : property + "=" + value).ToArray() : [string.Empty];
        }
        if (!declarations.TryGetValue("@" + item, out string[]? aliases)) return null;
        var result = new List<string>();
        foreach (string alias in aliases)
        {
            Match source = Regex.Match(alias, @"\A@\((?<name>[A-Za-z_][A-Za-z0-9_.-]*)(?:\s*->\s*WithMetadataValue\(\s*'[^']*'\s*,\s*'[^']*'\s*\))?\s*\)\z",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!source.Success) return null;
            string[]? inherited = FrozenBuildMetadata(source.Groups["name"].Value, metadata, declarations,
                new HashSet<string>(visiting, StringComparer.OrdinalIgnoreCase));
            if (inherited is null) return null;
            result.AddRange(inherited);
            result.Count.ShouldBeLessThanOrEqualTo(256, "Executable item metadata alias expansion exceeded its bounded inspection budget.");
        }
        return result.ToArray();
    }

    private static IEnumerable<string> ExpandFrozenBuildExpressions(
        string expression, Dictionary<string, string[]> declarations, HashSet<string> visiting)
    {
        visiting.Count.ShouldBeLessThanOrEqualTo(64, "Executable project expression expansion exceeded its bounded depth budget.");
        expression.Length.ShouldBeLessThanOrEqualTo(65536, "Executable project expression expansion exceeded its bounded size budget.");
        Match batch = Regex.Match(expression, @"%\((?<item>[A-Za-z_][A-Za-z0-9_-]*)\.(?<metadata>[A-Za-z_][A-Za-z0-9_-]*)\)", RegexOptions.CultureInvariant);
        if (batch.Success && FrozenBuildMetadata(batch.Groups["item"].Value, batch.Groups["metadata"].Value,
            declarations, new HashSet<string>(visiting, StringComparer.OrdinalIgnoreCase)) is { } metadataValues)
        {
            string key = "%" + batch.Groups["item"].Value + "." + batch.Groups["metadata"].Value;
            if (visiting.Contains(key)) { yield return expression; yield break; }
            var next = new HashSet<string>(visiting, StringComparer.OrdinalIgnoreCase) { key };
            foreach (string value in metadataValues)
            {
                foreach (string expanded in ExpandFrozenBuildExpressions(expression.Replace(batch.Value, value, StringComparison.Ordinal), declarations, next))
                    yield return expanded;
            }
            yield break;
        }
        Match filter = Regex.Match(expression, @"@\((?<name>[A-Za-z_][A-Za-z0-9_.-]*)\s*->\s*WithMetadataValue\(\s*'[^']*'\s*,\s*'[^']*'\s*\)\s*\)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (filter.Success)
        {
            // Ignoring this filter only adds candidate edges. Inspect every original
            // item so metadata cannot conceal an input that consumes frozen evidence.
            string unfiltered = expression.Replace(filter.Value, "@(" + filter.Groups["name"].Value + ")", StringComparison.Ordinal);
            foreach (string expanded in ExpandFrozenBuildExpressions(unfiltered, declarations, visiting)) yield return expanded;
            yield break;
        }
        Match transform = Regex.Match(expression, @"@\((?<name>[A-Za-z_][A-Za-z0-9_.-]*)\s*->\s*'(?<transform>[^']*)'\)", RegexOptions.CultureInvariant);
        if (transform.Success && declarations.TryGetValue("@" + transform.Groups["name"].Value, out string[]? items))
        {
            foreach (string item in items)
            {
                foreach (string expanded in ExpandFrozenBuildExpressions(item, declarations, new HashSet<string>(visiting, StringComparer.OrdinalIgnoreCase)))
                {
                    foreach (string identity in expanded.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        string value = Regex.Replace(transform.Groups["transform"].Value, @"%\((?<name>[A-Za-z_][A-Za-z0-9_.]*)\)", metadata =>
                            metadata.Groups["name"].Value.ToUpperInvariant() switch
                            {
                                "FILENAME" => Path.GetFileNameWithoutExtension(identity),
                                "EXTENSION" => Path.GetExtension(identity),
                                "IDENTITY" or "FULLPATH" => identity,
                                "ORIGINALITEMSPEC" => declarations.TryGetValue("%" + transform.Groups["name"].Value + "." + metadata.Groups["name"].Value,
                                    out string[]? original) ? string.Join(';', original) : identity,
                                _ => declarations.TryGetValue("%" + transform.Groups["name"].Value + "." + metadata.Groups["name"].Value,
                                    out string[]? values) ? string.Join(';', values) : metadata.Value,
                            }, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                        value.Contains("%(", StringComparison.Ordinal).ShouldBeFalse("Unresolved executable item transform metadata: " + value);
                        foreach (string result in ExpandFrozenBuildExpressions(expression.Replace(transform.Value, value, StringComparison.Ordinal), declarations, visiting)) yield return result;
                    }
                }
            }
            yield break;
        }
        foreach (Match match in Regex.Matches(expression, @"(?<kind>[$@])\((?<name>[A-Za-z_][A-Za-z0-9_.-]*)\)", RegexOptions.CultureInvariant))
        {
            string name = match.Groups["kind"].Value + match.Groups["name"].Value;
            string lookup = visiting.Contains(name) ? "?" + name : name;
            if (!declarations.TryGetValue(lookup, out string[]? values)) continue;
            foreach (string value in values)
            {
                var next = new HashSet<string>(visiting, StringComparer.OrdinalIgnoreCase) { name };
                string substituted = expression.Replace(match.Value, value, StringComparison.Ordinal);
                if (substituted == expression) { yield return expression; continue; }
                foreach (string expanded in ExpandFrozenBuildExpressions(substituted, declarations, next))
                {
                    yield return expanded;
                }
            }
            yield break;
        }
        yield return expression;
    }

    private static async Task<string> EvaluateFrozenBuildProjectAsync(
        string project, Dictionary<string, string> properties, params string[] operations)
    {
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = Path.GetDirectoryName(project)!,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            },
        };
        foreach (string argument in new[] { "msbuild", project, "-nologo" }
            .Concat(properties.Select(pair => "-p:" + pair.Key + "=" + pair.Value.Replace("%", "%25", StringComparison.Ordinal)
                .Replace(";", "%3B", StringComparison.Ordinal).Replace(",", "%2C", StringComparison.Ordinal))).Concat(operations))
        {
            process.StartInfo.ArgumentList.Add(argument);
        }
        process.Start().ShouldBeTrue();
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }
        string output = await outputTask.ConfigureAwait(true);
        string error = await errorTask.ConfigureAwait(true);
        process.ExitCode.ShouldBe(0, $"MSBuild input evaluation failed for {project}: {output}{error}");
        return output;
    }

    private static void AssertNotFrozenBuildInput(string root, string project, string input)
    {
        string evidence = Path.GetFullPath(Path.Combine(root, "_bmad-output/implementation-artifacts/evidence"))
            + Path.DirectorySeparatorChar;
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        // Check the lexical archive identity before following links in either direction.
        Path.GetFullPath(input).StartsWith(evidence, comparison)
            .ShouldBeFalse($"Active project {project} consumes frozen evidence as a build input: {input}");
        ResolvePhysicalBuildPath(input).StartsWith(ResolvePhysicalBuildPath(evidence) + Path.DirectorySeparatorChar, comparison)
            .ShouldBeFalse($"Active project {project} consumes frozen evidence as a build input: {input}");
    }

    private static string ResolvePhysicalBuildPath(string input)
    {
        string path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(input));
        FileSystemInfo info = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
        FileSystemInfo? target = File.Exists(path) || Directory.Exists(path)
            ? info.ResolveLinkTarget(returnFinalTarget: true) : null;
        if (target is not null) return ResolvePhysicalBuildPath(target.FullName);
        string? parent = Path.GetDirectoryName(path);
        return parent is null ? path : Path.Combine(ResolvePhysicalBuildPath(parent), Path.GetFileName(path));
    }
}
