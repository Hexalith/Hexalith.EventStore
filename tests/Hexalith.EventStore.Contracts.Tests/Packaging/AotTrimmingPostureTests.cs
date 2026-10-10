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
        string root = FindRepositoryRoot();
        string path = Path.Combine(root, PostureDocumentPath);
        Assert.True(File.Exists(path), $"The posture document is missing: {PostureDocumentPath}.");

        string text = File.ReadAllText(path);
        AssertPostureMarker(text);
        Assert.Contains("## Reflection Convention Inventory", text, StringComparison.Ordinal);
    }

    /// <summary>Checks every release package using its evaluated MSBuild properties.</summary>
    [Fact]
    public void ReleasePackagesDoNotClaimAotOrTrimmingCompatibility()
    {
        string root = FindRepositoryRoot();
        string[] projects = LoadReleasePackageProjects(root);
        AssertNoCompatibilityClaims(EvaluateViolations(root, projects, propertyOverride: null));
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
        string[] violations = EvaluateViolations(root, [project], (propertyName, propertyValue));

        Exception? failure = Record.Exception(() => AssertNoCompatibilityClaims(violations));
        Assert.NotNull(failure);
        Assert.Contains(project, failure.Message, StringComparison.Ordinal);
        Assert.Contains(propertyName, failure.Message, StringComparison.Ordinal);
        if (expectImpliedTrimming)
        {
            Assert.Contains("IsTrimmable", failure.Message, StringComparison.Ordinal);
            Assert.Equal(2, violations.Length);
        }
        else
        {
            Assert.Single(violations);
        }
    }

    /// <summary>Reports every violating project and property in one failure.</summary>
    [Fact]
    public void MultipleReleasePackageClaimsAreReportedTogether()
    {
        string root = FindRepositoryRoot();
        string[] projects = LoadReleasePackageProjects(root).Take(2).ToArray();
        Assert.Equal(2, projects.Length);

        string[] violations = EvaluateViolations(root, projects, ("IsAotCompatible", "true"));
        Exception? failure = Record.Exception(() => AssertNoCompatibilityClaims(violations));

        Assert.NotNull(failure);
        Assert.Equal(4, violations.Length);
        foreach (string project in projects)
        {
            Assert.Contains($"{project}: IsAotCompatible=true", failure.Message, StringComparison.Ordinal);
            Assert.Contains($"{project}: IsTrimmable=true", failure.Message, StringComparison.Ordinal);
        }
    }

    /// <summary>Proves a missing or displaced marker stops the guard before evaluating projects.</summary>
    [Fact]
    public void MissingOrDisplacedPostureMarkerFailsClosed()
    {
        string[] invalidPages =
        [
            "## Reflection Convention Inventory",
            $"## Previous Posture\n\n{PostureMarker}\n",
            $"## Current Posture\n\n```markdown\n{PostureMarker}\n```",
        ];
        foreach (string page in invalidPages)
        {
            Exception? failure = Record.Exception(() => AssertPostureMarker(page));
            Assert.NotNull(failure);
            Assert.Contains(PostureDocumentPath, failure.Message, StringComparison.Ordinal);
        }
    }

    private static void AssertNoCompatibilityClaims(IReadOnlyCollection<string> violations)
        => Assert.True(
            violations.Count == 0,
            "Release packages claim unsupported Native AOT or IL trimming compatibility:" + Environment.NewLine
                + string.Join(Environment.NewLine, violations));

    private static void AssertPostureMarker(string text)
        => Assert.True(text.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Contains($"\n## Current Posture\n\n{PostureMarker}\n", StringComparison.Ordinal),
            $"The posture marker is missing from the Current Posture section of {PostureDocumentPath}.");

    private static string[] EvaluateViolations(
        string root,
        IReadOnlyCollection<string> projects,
        (string Name, string Value)? propertyOverride)
    {
        string document = Path.Combine(root, PostureDocumentPath);
        Assert.True(File.Exists(document), $"The posture document is missing: {PostureDocumentPath}.");
        string text = File.ReadAllText(document);
        AssertPostureMarker(text);
        Assert.NotEmpty(projects);

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

        Assert.True(process.Start(), $"Could not start dotnet msbuild for {project}.");
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit((int)MsBuildEvaluationTimeout.TotalMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"dotnet msbuild property evaluation timed out after {MsBuildEvaluationTimeout} for {project}.");
        }

        string output = outputTask.GetAwaiter().GetResult();
        string error = errorTask.GetAwaiter().GetResult();
        Assert.True(process.ExitCode == 0, $"dotnet msbuild property evaluation failed for {project}: {error}");
        return JsonDocument.Parse(output);
    }

    private static string[] LoadReleasePackageProjects(string root)
    {
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "tools", "release-packages.json")));
        string[] projects = manifest.RootElement.GetProperty("packages")
            .EnumerateArray()
            .Select(package => package.GetProperty("project").GetString()!)
            .ToArray();
        Assert.NotEmpty(projects);
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
