using System.Text.RegularExpressions;

namespace Hexalith.EventStore.Contracts.Tests.Packaging;

/// <summary>Verifies the PostgreSQL product fixture retains its reviewed immutable image.</summary>
public sealed class PostgreSqlImageGovernanceTests
{
    private const string ReviewedPostgresImage =
        "postgres@sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636";
    private static readonly Regex FixtureImagePattern = new(
        """(?m)^[ \t]*private[ \t]+const[ \t]+string[ \t]+PostgresImage[ \t]*=[ \t]*"(?<image>[^"\r\n]+)"[ \t]*;[ \t]*(?://[^\r\n]*)?\r?$""",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>Verifies the actual fixture uses the reviewed PostgreSQL multi-platform index.</summary>
    [Fact]
    public void PostgreSqlFixtureRemainsDigestPinned()
    {
        DirectoryInfo? directory = new(Directory.GetCurrentDirectory());
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Hexalith.EventStore.slnx")))
        {
            directory = directory.Parent;
        }

        string root = directory?.FullName ?? throw new DirectoryNotFoundException("EventStore root was not found.");
        string source = File.ReadAllText(Path.Combine(root,
            "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8PostgresqlFixture.cs"));
        AssertReviewedImage(ExtractFixtureImage(source));
    }

    /// <summary>Rejects mutable, malformed, architecture-specific and unreviewed product images.</summary>
    /// <param name="image">The invalid image identity.</param>
    [Theory]
    [InlineData("postgres:18.4")]
    [InlineData("postgres@sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea3163")]
    [InlineData("postgres@sha256:A02DB8CAC496F15B094798A38254F14D6E00741F709360E5E00BB6668EA31636")]
    [InlineData("postgres@sha256:4cc13dede823cab4e05290c7fb3350fb4e599ecabd9b07e6706b5d5e8f5bc929")]
    public void MutableMalformedOrUnreviewedImagesFailClosed(string image)
    {
        Should.Throw<Shouldly.ShouldAssertException>(() => AssertReviewedImage(image));
    }

    /// <summary>Rejects missing, malformed or duplicate fixture declarations.</summary>
    /// <param name="source">The invalid fixture source.</param>
    [Theory]
    [InlineData("")]
    [InlineData("private static readonly string PostgresImage = \"image\";")]
    [InlineData("private const string PostgresImage = \"image\";\nprivate const string PostgresImage = \"image\";")]
    public void InvalidFixtureDeclarationsFailClosed(string source)
    {
        Should.Throw<Shouldly.ShouldAssertException>(() => ExtractFixtureImage(source));
    }

    private static string ExtractFixtureImage(string source)
    {
        MatchCollection declarations = FixtureImagePattern.Matches(source);
        declarations.Count.ShouldBe(1, "The product fixture must declare exactly one PostgresImage.");
        return declarations[0].Groups["image"].Value;
    }

    private static void AssertReviewedImage(string image)
    {
        Regex.IsMatch(image, "^postgres@sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1)).ShouldBeTrue();
        image.ShouldBe(ReviewedPostgresImage);
    }
}
