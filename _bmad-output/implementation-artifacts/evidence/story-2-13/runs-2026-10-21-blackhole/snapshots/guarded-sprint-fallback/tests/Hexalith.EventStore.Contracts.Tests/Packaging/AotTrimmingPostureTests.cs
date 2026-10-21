using System.Diagnostics;
using System.Text.Json;

namespace Hexalith.EventStore.Contracts.Tests.Packaging;

/// <summary>Guards the documented Native AOT and trimming posture of release packages.</summary>
public sealed class AotTrimmingPostureTests
{
    private const string PostureDocumentPath = "docs/reference/aot-and-trimming-posture.md";
    private const string PostureMarker = "**Posture:** Native AOT and IL trimming are not targets for Hexalith.EventStore release packages.";
    private static readonly TimeSpan MsBuildEvaluationTimeout = TimeSpan.FromSeconds(60);

    /// <summary>Requires the published reference page and its posture inventory.</summary>
    [Fact]
    public void PostureDocumentStatesTheCurrentContract()
    {
        string text = ReadPostureDocument(FindRepositoryRoot());
        AssertPostureMarker(text);
        AssertInventoryRows(text);
    }

    /// <summary>Checks every release package using its evaluated MSBuild properties.</summary>
    [Fact]
    public void ReleasePackagesDoNotClaimAotOrTrimmingCompatibility()
    {
        string root = FindRepositoryRoot();
        string[] projects = LoadReleasePackageProjects(root);
        AssertNoCompatibilityClaims(EvaluateViolations(root, ReadPostureDocument(root), projects, propertyOverride: null));
    }

    /// <summary>Proves explicit and SDK-implied compatibility claims fail the same guard.</summary>
    /// <param name="propertyName">The seeded MSBuild property.</param>
    /// <param name="propertyValue">The seeded MSBuild value.</param>
    /// <param name="expectImpliedTrimming">Whether the SDK should also report IsTrimmable as true.</param>
    [Theory]
    [InlineData("IsAotCompatible", "true", true)]
    [InlineData("IsTrimmable", "true", false)]
    [InlineData("IsTrimmable", "True", false)]
    public void SeededCompatibilityClaimsFailTheReleaseGuard(
        string propertyName,
        string propertyValue,
        bool expectImpliedTrimming)
    {
        string root = FindRepositoryRoot();
        string project = LoadReleasePackageProjects(root)[0];
        string[] violations = EvaluateViolations(root, ReadPostureDocument(root), [project], (propertyName, propertyValue));

        ShouldAssertException failure = Should.Throw<ShouldAssertException>(() => AssertNoCompatibilityClaims(violations));
        failure.Message.ShouldContain(project, Case.Sensitive);
        failure.Message.ShouldContain(propertyName, Case.Sensitive);
        if (expectImpliedTrimming)
        {
            failure.Message.ShouldContain("IsTrimmable", Case.Sensitive);
            violations.Length.ShouldBe(2);
        }
        else
        {
            violations.ShouldHaveSingleItem();
        }
    }

    /// <summary>Reports every violating release project and property in one failure.</summary>
    [Fact]
    public void MultipleReleasePackageClaimsAreReportedTogether()
    {
        string root = FindRepositoryRoot();
        string[] projects = LoadReleasePackageProjects(root);
        projects.ShouldContain("src/Hexalith.EventStore.Admin.Server/Hexalith.EventStore.Admin.Server.csproj");

        string[] violations = EvaluateViolations(root, ReadPostureDocument(root), projects, ("IsAotCompatible", "true"));
        ShouldAssertException failure = Should.Throw<ShouldAssertException>(() => AssertNoCompatibilityClaims(violations));

        violations.Length.ShouldBe(projects.Length * 2);
        foreach (string project in projects)
        {
            failure.Message.ShouldContain($"{project}: IsAotCompatible=true", Case.Sensitive);
            failure.Message.ShouldContain($"{project}: IsTrimmable=true", Case.Sensitive);
        }
    }

    /// <summary>Proves a missing or displaced marker stops the release guard before it evaluates projects.</summary>
    [Fact]
    public void MissingOrDisplacedPostureMarkerFailsClosed()
    {
        string root = FindRepositoryRoot();
        string[] projects = LoadReleasePackageProjects(root);
        string[] invalidPages =
        [
            "## Reflection Convention Inventory",
            $"## Previous Posture\n\n{PostureMarker}\n",
            $"## Current Posture\n\n```markdown\n{PostureMarker}\n```",
            $"```markdown\n## Current Posture\n\n{PostureMarker}\n```",
        ];
        foreach (string page in invalidPages)
        {
            ShouldAssertException failure = Should.Throw<ShouldAssertException>(
                () => EvaluateViolations(root, page, projects, propertyOverride: null));
            failure.Message.ShouldContain(PostureDocumentPath, Case.Sensitive);
        }
    }

    /// <summary>Proves an empty reflection inventory does not satisfy the document guard.</summary>
    [Fact]
    public void InventoryHeadingWithoutRowsFailsClosed()
    {
        const string page = "## Reflection Convention Inventory\n\n| Convention | Where | Reflection or dynamic behavior used |\n| --- | --- | --- |\n\n## Consumer Guidance";
        Should.Throw<ShouldAssertException>(() => AssertInventoryRows(page));
    }

    private static void AssertNoCompatibilityClaims(IReadOnlyCollection<string> violations)
        => violations.ShouldBeEmpty(
            "Release packages claim unsupported Native AOT or IL trimming compatibility:" + Environment.NewLine
                + string.Join(Environment.NewLine, violations));

    private static void AssertInventoryRows(string text)
    {
        text.ShouldContain("## Reflection Convention Inventory", Case.Sensitive);
        string inventory = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split("## Reflection Convention Inventory", 2, StringSplitOptions.None)[1]
            .Split("\n## ", 2, StringSplitOptions.None)[0];
        inventory.Split('\n').ShouldContain(
            line => line.StartsWith("| ", StringComparison.Ordinal)
                && !line.StartsWith("| Convention |", StringComparison.Ordinal)
                && !line.StartsWith("| --- |", StringComparison.Ordinal),
            $"The reflection convention inventory in {PostureDocumentPath} has no rows.");
    }

    private static void AssertPostureMarker(string text)
    {
        string[] lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        bool inCodeFence = false;
        bool markerFound = false;
        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index];
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal)
                || line.TrimStart().StartsWith("~~~", StringComparison.Ordinal))
            {
                inCodeFence = !inCodeFence;
                continue;
            }

            if (inCodeFence || line != "## Current Posture")
            {
                continue;
            }

            int paragraph = index + 1;
            while (paragraph < lines.Length && string.IsNullOrWhiteSpace(lines[paragraph]))
            {
                paragraph++;
            }

            markerFound = paragraph < lines.Length && lines[paragraph] == PostureMarker;
            break;
        }

        markerFound.ShouldBeTrue(
            $"The posture marker is missing from the Current Posture section of {PostureDocumentPath}.");
    }

    private static string ReadPostureDocument(string root)
    {
        string path = Path.Combine(root, PostureDocumentPath);
        File.Exists(path).ShouldBeTrue($"The posture document is missing: {PostureDocumentPath}.");
        return File.ReadAllText(path);
    }

    private static string[] EvaluateViolations(
        string root,
        string postureDocument,
        IReadOnlyCollection<string> projects,
        (string Name, string Value)? propertyOverride)
    {
        AssertPostureMarker(postureDocument);
        projects.ShouldNotBeEmpty();

        List<string> violations = [];
        foreach (string project in projects)
        {
            using JsonDocument evaluation = EvaluateProjectProperties(root, project, propertyOverride);
            JsonElement properties = evaluation.RootElement.GetProperty("Properties");
            foreach (string name in new[] { "IsAotCompatible", "IsTrimmable" })
            {
                string? value = properties.GetProperty(name).GetString();
                if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add($"{project}: {name}={value}");
                }
            }
        }

        return [.. violations];
    }

    private static JsonDocument EvaluateProjectProperties(
        string root,
        string project,
        (string Name, string Value)? propertyOverride)
    {
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = root,
            },
        };
        process.StartInfo.ArgumentList.Add("msbuild");
        process.StartInfo.ArgumentList.Add(Path.Combine(root, project));
        process.StartInfo.ArgumentList.Add("-nologo");
        process.StartInfo.ArgumentList.Add("-getProperty:IsAotCompatible,IsTrimmable");
        process.StartInfo.ArgumentList.Add("-p:Configuration=Release");
        process.StartInfo.ArgumentList.Add("-p:UseHexalithProjectReferences=false");
        if (propertyOverride is { } seeded)
        {
            process.StartInfo.ArgumentList.Add($"-p:{seeded.Name}={seeded.Value}");
        }

        process.Start().ShouldBeTrue($"Could not start dotnet msbuild for {project}.");
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit((int)MsBuildEvaluationTimeout.TotalMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"dotnet msbuild property evaluation timed out after {MsBuildEvaluationTimeout} for {project}.");
        }

        string output = outputTask.GetAwaiter().GetResult();
        string error = errorTask.GetAwaiter().GetResult();
        process.ExitCode.ShouldBe(0, $"dotnet msbuild property evaluation failed for {project}: {error}");
        return JsonDocument.Parse(output);
    }

    private static string[] LoadReleasePackageProjects(string root)
    {
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "tools", "release-packages.json")));
        string[] projects = manifest.RootElement.GetProperty("packages")
            .EnumerateArray()
            .Select(package => package.GetProperty("project").GetString()!)
            .ToArray();
        projects.ShouldNotBeEmpty();
        return projects;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Directory.Packages.props"))
                && File.Exists(Path.Combine(directory.FullName, "tools", "release-packages.json")))
            {
                return PackagingRepositoryPaths.VerifyRepositoryRoot(directory.FullName, "Hexalith.EventStore");
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from the test working directory.");
    }
}
