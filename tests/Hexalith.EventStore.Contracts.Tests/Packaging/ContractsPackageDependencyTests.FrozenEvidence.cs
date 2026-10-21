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
    [InlineData("target-reference")]
    [InlineData("comment")]
    [InlineData("cycle")]
    [InlineData("reverse-alias")]
    public async Task FrozenMsBuildEvidence_RejectsActivationAsync(string activation)
    {
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
            else if (activation is "task" or "task-property" or "task-composed" or "task-item" or "task-bridge" or "task-alias" or "target-reference")
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
                XElement action = activation == "target-reference"
                    ? new XElement("ItemGroup", new XElement("ProjectReference", new XAttribute("Include", expression)))
                    : new XElement("MSBuild", new XAttribute("Projects", expression), new XAttribute("Targets", "Build"));
                project.Add(new XElement("Target", new XAttribute("Name", "AddFrozen"),
                    new XAttribute("BeforeTargets", "PrepareProjectReferences"), action));
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
            foreach (XComment comment in document.DescendantNodes().OfType<XComment>())
            {
                // MSBuild boundary records start with a separator and put an absolute source
                // path immediately before their closing separator. Ordinary comments are data.
                string[] lines = comment.Value.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToArray();
                static bool Separator(string line) => line.Length >= 10 && line.All(character => character == '=');
                if (lines.Length >= 3 && Separator(lines[0]) && Separator(lines[^1]) && Path.IsPathFullyQualified(lines[^2]))
                {
                    AssertNotFrozenBuildInput(root, project, lines[^2]);
                }
            }

            // Also inspect executable target bodies: evaluation-time items omit references
            // added by targets and MSBuild task invocations. No historical paths are permitted
            // on these executable edges, even when execution would occur after evaluation.
            var declarations = document.Descendants().Where(element => element.Parent?.Name.LocalName == "PropertyGroup")
                .Select(element => (Name: "$" + element.Name.LocalName, Value: element.Value))
                .Concat(document.Descendants().Where(element => element.Parent?.Name.LocalName == "ItemGroup")
                    .SelectMany(element => element.Attributes().Where(attribute => attribute.Name.LocalName is "Include" or "Update")
                        .Select(attribute => (Name: "@" + element.Name.LocalName, Value: attribute.Value))))
                .GroupBy(declaration => declaration.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Select(declaration => declaration.Value).Distinct().ToArray(), StringComparer.OrdinalIgnoreCase);
            declarations["$MSBuildProjectFullPath"] = [project];
            declarations["$MSBuildProjectDirectory"] = [Path.GetDirectoryName(project)!];
            declarations["$MSBuildThisFileDirectory"] = [Path.GetDirectoryName(project)! + Path.DirectorySeparatorChar];
            foreach (XElement element in document.Descendants().Where(element => element.Name.LocalName is "ProjectReference" or "MSBuild"))
            {
                foreach (XAttribute attribute in element.Attributes().Where(attribute => attribute.Name.LocalName is "Include" or "Update" or "Projects"))
                {
                    string[] expressions = ExpandFrozenBuildExpressions(attribute.Value, declarations, new HashSet<string>(StringComparer.OrdinalIgnoreCase))
                        .Distinct().Take(257).ToArray();
                    expressions.Length.ShouldBeLessThanOrEqualTo(256, "Executable project input expansion exceeded its bounded inspection budget.");
                    foreach (string expression in expressions)
                    {
                        string normalized = expression.Replace('\\', '/');
                        const string marker = "_bmad-output/implementation-artifacts/evidence";
                        int index = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                        bool archived = index >= 0 && (index + marker.Length == normalized.Length || normalized[index + marker.Length] == '/');
                        archived.ShouldBeFalse($"Active project {project} consumes frozen evidence as a build input: {expression}");
                        if (expression.Contains("$(", StringComparison.Ordinal) || expression.Contains("@(", StringComparison.Ordinal)
                            || expression.Contains("%(", StringComparison.Ordinal)) continue;
                        foreach (string path in expression.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        {
                            if (path.IndexOfAny(['*', '?', '%']) >= 0) continue;
                            string referenced = Path.GetFullPath(path.Replace('\\', Path.DirectorySeparatorChar), Path.GetDirectoryName(project)!);
                            AssertNotFrozenBuildInput(root, project, referenced);
                            if (File.Exists(referenced))
                            {
                                await InspectFrozenBuildGraphAsync(root, referenced, new Dictionary<string, string>(properties, StringComparer.OrdinalIgnoreCase), visited)
                                    .ConfigureAwait(true);
                            }
                        }
                    }
                }
            }

            // -getItem supersedes -preprocess, so request evaluated properties/references separately.
            string output = await EvaluateFrozenBuildProjectAsync(project, properties,
                "-getItem:ProjectReference", "-getProperty:TargetFrameworks,TargetFramework").ConfigureAwait(true);
            using JsonDocument evaluation = JsonDocument.Parse(output);
            string frameworks = evaluation.RootElement.GetProperty("Properties").GetProperty("TargetFrameworks").GetString()!;
            if (!properties.ContainsKey("TargetFramework"))
            {
                foreach (string framework in frameworks.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var inner = new Dictionary<string, string>(properties, StringComparer.OrdinalIgnoreCase) { ["TargetFramework"] = framework };
                    await InspectFrozenBuildGraphAsync(root, project, inner, visited).ConfigureAwait(true);
                }
            }
            foreach (JsonElement reference in evaluation.RootElement.GetProperty("Items").GetProperty("ProjectReference").EnumerateArray())
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

    private static IEnumerable<string> ExpandFrozenBuildExpressions(
        string expression, Dictionary<string, string[]> declarations, HashSet<string> visiting)
    {
        foreach (Match match in Regex.Matches(expression, @"(?<kind>[$@])\((?<name>[A-Za-z_][A-Za-z0-9_.-]*)\)", RegexOptions.CultureInvariant))
        {
            string name = match.Groups["kind"].Value + match.Groups["name"].Value;
            if (visiting.Contains(name) || !declarations.TryGetValue(name, out string[]? values)) continue;
            foreach (string value in values)
            {
                var next = new HashSet<string>(visiting, StringComparer.OrdinalIgnoreCase) { name };
                foreach (string expanded in ExpandFrozenBuildExpressions(expression.Replace(match.Value, value, StringComparison.Ordinal), declarations, next))
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
            .Concat(properties.Select(pair => "-p:" + pair.Key + "=" + pair.Value)).Concat(operations))
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
