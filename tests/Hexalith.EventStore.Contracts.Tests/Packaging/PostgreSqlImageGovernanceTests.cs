using System.Text.RegularExpressions;

namespace Hexalith.EventStore.Contracts.Tests.Packaging;

/// <summary>
/// Verifies the PostgreSQL image used by integration preparation, the OQ8 runtime fixture,
/// and the OQ8 evidence validator remains synchronized.
/// </summary>
public sealed class PostgreSqlImageGovernanceTests
{
    private const string WorkflowRelativePath = ".github/workflows/integration.yml";
    private const string FixtureRelativePath =
        "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8PostgresqlFixture.cs";
    private const string ValidatorRelativePath = "tools/validate-oq8-platform-evidence.py";
    private static readonly TimeSpan _regexTimeout = TimeSpan.FromSeconds(1);
    private static readonly Regex _workflowStepPattern = new(
        """(?ms)^(?<indent>[ \t]*)-[ \t]+name:[ \t]*(?:"|')?Pull PostgreSQL container image(?:"|')?[ \t]*(?:#[^\r\n]*)?\r?\n(?<body>.*?)(?=^\k<indent>-[ \t]+|\z)""",
        RegexOptions.CultureInvariant,
        _regexTimeout);
    private static readonly Regex _workflowPullPattern = new(
        """(?m)^[ \t]*command:[ \t]*(?<quote>["']?)docker[ \t]+pull[ \t]+(?<image>[^\s"'#]+)\k<quote>[ \t]*(?:#[^\r\n]*)?\r?$""",
        RegexOptions.CultureInvariant,
        _regexTimeout);
    private static readonly Regex _fixtureImagePattern = new(
        """(?m)^[ \t]*private[ \t]+const[ \t]+string[ \t]+PostgresImage[ \t]*=[ \t]*"(?<image>[^"\r\n]+)"[ \t]*;[ \t]*(?://[^\r\n]*)?\r?$""",
        RegexOptions.CultureInvariant,
        _regexTimeout);
    private static readonly Regex _validatorImagePattern = new(
        """(?m)^[ \t]*POSTGRES_IMAGE[ \t]*=[ \t]*(?<quote>["'])(?<image>[^"'\r\n]+)\k<quote>[ \t]*(?:#[^\r\n]*)?\r?$""",
        RegexOptions.CultureInvariant,
        _regexTimeout);

    /// <summary>
    /// Verifies all three authoritative repository surfaces declare the same PostgreSQL image.
    /// </summary>
    [Fact]
    public void PostgreSqlImageAuthoritiesRemainSynchronized()
    {
        string root = FindRepositoryRoot();
        string workflowImage = ExtractWorkflowImage(File.ReadAllText(Path.Combine(root, WorkflowRelativePath)));
        string fixtureImage = ExtractFixtureImage(File.ReadAllText(Path.Combine(root, FixtureRelativePath)));
        string validatorImage = ExtractValidatorImage(File.ReadAllText(Path.Combine(root, ValidatorRelativePath)));

        AssertImagesSynchronized(workflowImage, fixtureImage, validatorImage);
    }

    /// <summary>
    /// Verifies missing, malformed, and duplicated authority declarations fail closed.
    /// </summary>
    /// <param name="authority">The structural authority to mutate.</param>
    /// <param name="mutation">The mutation applied to the authority.</param>
    [Theory]
    [InlineData("workflow-step", "missing")]
    [InlineData("workflow-step", "malformed")]
    [InlineData("workflow-step", "duplicated")]
    [InlineData("workflow-pull", "missing")]
    [InlineData("workflow-pull", "malformed")]
    [InlineData("workflow-pull", "duplicated")]
    [InlineData("fixture", "missing")]
    [InlineData("fixture", "malformed")]
    [InlineData("fixture", "duplicated")]
    [InlineData("validator", "missing")]
    [InlineData("validator", "malformed")]
    [InlineData("validator", "duplicated")]
    public void InvalidAuthorityDeclarationsFailClosed(string authority, string mutation)
    {
        string image = nameof(PostgreSqlImageGovernanceTests);
        string source = CreateAuthoritySource(authority, mutation, image);

        Shouldly.ShouldAssertException exception = Should.Throw<Shouldly.ShouldAssertException>(() =>
        {
            _ = authority switch
            {
                "workflow-step" or "workflow-pull" => ExtractWorkflowImage(source),
                "fixture" => ExtractFixtureImage(source),
                "validator" => ExtractValidatorImage(source),
                _ => throw new ArgumentOutOfRangeException(nameof(authority), authority, "Unknown authority."),
            };
        });

        string diagnostic = authority switch
        {
            "workflow-step" => "integration workflow named step",
            "workflow-pull" => "integration workflow pull command",
            "fixture" => "OQ8 fixture PostgresImage declaration",
            "validator" => "OQ8 evidence validator POSTGRES_IMAGE declaration",
            _ => throw new ArgumentOutOfRangeException(nameof(authority), authority, "Unknown authority."),
        };
        exception.Message.ShouldContain(diagnostic);
        exception.Message.ShouldContain(mutation == "duplicated" ? "found 2" : "found 0");
    }

    /// <summary>
    /// Verifies image drift reports every compared authority value.
    /// </summary>
    [Fact]
    public void ImageDriftReportsComparedAuthorityValues()
    {
        string workflowImage = nameof(PostgreSqlImageGovernanceTests);
        string fixtureImage = workflowImage + "-fixture-drift";
        string validatorImage = workflowImage + "-validator-drift";

        Shouldly.ShouldAssertException exception = Should.Throw<Shouldly.ShouldAssertException>(
            () => AssertImagesSynchronized(workflowImage, fixtureImage, validatorImage));

        exception.Message.ShouldContain($"integration workflow='{workflowImage}'");
        exception.Message.ShouldContain($"OQ8 fixture='{fixtureImage}'");
        exception.Message.ShouldContain($"OQ8 evidence validator='{validatorImage}'");
    }

    private static string ExtractWorkflowImage(string source)
    {
        MatchCollection steps = _workflowStepPattern.Matches(source);
        steps.Count.ShouldBe(
            1,
            $"The integration workflow named step 'Pull PostgreSQL container image' must occur exactly once; found {steps.Count}.");

        MatchCollection pulls = _workflowPullPattern.Matches(steps[0].Groups["body"].Value);
        pulls.Count.ShouldBe(
            1,
            $"The integration workflow pull command must occur exactly once inside the named PostgreSQL step; found {pulls.Count}.");
        return pulls[0].Groups["image"].Value;
    }

    private static string ExtractFixtureImage(string source)
    {
        MatchCollection declarations = _fixtureImagePattern.Matches(source);
        declarations.Count.ShouldBe(
            1,
            $"The OQ8 fixture PostgresImage declaration must occur exactly once; found {declarations.Count}.");
        return declarations[0].Groups["image"].Value;
    }

    private static string ExtractValidatorImage(string source)
    {
        MatchCollection declarations = _validatorImagePattern.Matches(source);
        declarations.Count.ShouldBe(
            1,
            $"The OQ8 evidence validator POSTGRES_IMAGE declaration must occur exactly once; found {declarations.Count}.");
        return declarations[0].Groups["image"].Value;
    }

    private static void AssertImagesSynchronized(string workflowImage, string fixtureImage, string validatorImage)
        => (workflowImage == fixtureImage && workflowImage == validatorImage).ShouldBeTrue(
            "PostgreSQL image authorities differ: " +
            $"integration workflow='{workflowImage}', " +
            $"OQ8 fixture='{fixtureImage}', " +
            $"OQ8 evidence validator='{validatorImage}'.");

    private static string CreateAuthoritySource(string authority, string mutation, string image)
    {
        string workflow = $$"""
            jobs:
              integration:
                steps:
                  - name: Pull PostgreSQL container image
                    with:
                      command: docker pull {{image}}
                  - name: Continue
                    run: true
            """;
        string fixture = $$"""
            public sealed class Fixture
            {
                private const string PostgresImage = "{{image}}";
            }
            """;
        string validator = $$"""
            PROFILE = "candidate"
            POSTGRES_IMAGE = "{{image}}"
            VERSION = "candidate"
            """;

        return (authority, mutation) switch
        {
            ("workflow-step", "missing") => workflow.Replace(
                "name: Pull PostgreSQL container image",
                "name: Different step",
                StringComparison.Ordinal),
            ("workflow-step", "malformed") => workflow.Replace(
                "name: Pull PostgreSQL container image",
                "title: Pull PostgreSQL container image",
                StringComparison.Ordinal),
            ("workflow-step", "duplicated") => workflow + Environment.NewLine + workflow,
            ("workflow-pull", "missing") => workflow.Replace(
                $"command: docker pull {image}",
                "run: true",
                StringComparison.Ordinal),
            ("workflow-pull", "malformed") => workflow.Replace("docker pull", "docker push", StringComparison.Ordinal),
            ("workflow-pull", "duplicated") => workflow.Replace(
                $"command: docker pull {image}",
                $"command: docker pull {image}{Environment.NewLine}          command: docker pull {image}",
                StringComparison.Ordinal),
            ("fixture", "missing") => fixture.Replace(
                $"private const string PostgresImage = \"{image}\";",
                $"private const string AlternateImage = \"{image}\";",
                StringComparison.Ordinal),
            ("fixture", "malformed") => fixture.Replace("const string", "static readonly string", StringComparison.Ordinal),
            ("fixture", "duplicated") => fixture.Replace(
                $"    private const string PostgresImage = \"{image}\";",
                $"    private const string PostgresImage = \"{image}\";{Environment.NewLine}    private const string PostgresImage = \"{image}\";",
                StringComparison.Ordinal),
            ("validator", "missing") => validator.Replace(
                $"POSTGRES_IMAGE = \"{image}\"",
                $"ALTERNATE_IMAGE = \"{image}\"",
                StringComparison.Ordinal),
            ("validator", "malformed") => validator.Replace("POSTGRES_IMAGE =", "POSTGRES_IMAGE :=", StringComparison.Ordinal),
            ("validator", "duplicated") => validator.Replace(
                $"POSTGRES_IMAGE = \"{image}\"",
                $"POSTGRES_IMAGE = \"{image}\"{Environment.NewLine}POSTGRES_IMAGE = \"{image}\"",
                StringComparison.Ordinal),
            _ => throw new ArgumentOutOfRangeException(
                nameof(mutation),
                $"{authority}:{mutation}",
                "Unknown authority mutation."),
        };
    }

    private static string FindRepositoryRoot()
    {
        string[] startPaths = [Directory.GetCurrentDirectory(), AppContext.BaseDirectory];
        foreach (string startPath in startPaths.Distinct(StringComparer.Ordinal))
        {
            DirectoryInfo? directory = new(startPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))
                    && Directory.Exists(Path.Combine(directory.FullName, "src", "Hexalith.EventStore.Contracts")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate repository root from the test working directory.");
    }
}
