using System.CommandLine;

using Hexalith.EventStore.Admin.Cli;

namespace Hexalith.EventStore.Admin.Cli.Tests;

/// <summary>
/// Tests for global option parsing, defaults, and environment variable fallback.
/// </summary>
[Collection("EnvironmentVariableTests")]
public class GlobalOptionsTests : IDisposable
{
    private readonly string[] _envVarsToClean =
    [
        "EVENTSTORE_ADMIN_URL",
        "EVENTSTORE_ADMIN_TOKEN",
        "EVENTSTORE_ADMIN_FORMAT",
    ];

    public GlobalOptionsTests()
    {
        // Clean env vars before each test
        foreach (string v in _envVarsToClean)
        {
            Environment.SetEnvironmentVariable(v, null);
        }
    }

    public void Dispose()
    {
        foreach (string v in _envVarsToClean)
        {
            Environment.SetEnvironmentVariable(v, null);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void GlobalOptions_DefaultValues_AreCorrect()
    {
        // Arrange
        GlobalOptionsBinding binding = GlobalOptionsBinding.Create();
        RootCommand root = BuildRootCommand(binding);

        // Act
        ParseResult result = root.Parse(Array.Empty<string>());
        GlobalOptions options = binding.Resolve(result);

        // Assert
        options.Url.ShouldBe("http://localhost:5002");
        options.Format.ShouldBe("table");
        options.Token.ShouldBeNull();
        options.OutputFile.ShouldBeNull();
    }

    [Fact]
    public void GlobalOptions_ExplicitArguments_OverrideDefaults()
    {
        // Arrange
        GlobalOptionsBinding binding = GlobalOptionsBinding.Create();
        RootCommand root = BuildRootCommand(binding);

        // Act
        ParseResult result = root.Parse(["--url", "https://prod:8080", "--token", "abc", "--format", "json"]);
        GlobalOptions options = binding.Resolve(result);

        // Assert
        options.Url.ShouldBe("https://prod:8080");
        options.Token.ShouldBe("abc");
        options.Format.ShouldBe("json");
    }

    [Fact]
    public void GlobalOptions_EnvironmentVariables_FallbackWhenNoArgument()
    {
        // Arrange
        Environment.SetEnvironmentVariable("EVENTSTORE_ADMIN_URL", "https://env-url:9090");
        Environment.SetEnvironmentVariable("EVENTSTORE_ADMIN_TOKEN", "env-token");
        Environment.SetEnvironmentVariable("EVENTSTORE_ADMIN_FORMAT", "csv");

        // Must create binding AFTER setting env vars (factory reads them)
        GlobalOptionsBinding binding = GlobalOptionsBinding.Create();
        RootCommand root = BuildRootCommand(binding);

        // Act
        ParseResult result = root.Parse(Array.Empty<string>());
        GlobalOptions options = binding.Resolve(result);

        // Assert
        options.Url.ShouldBe("https://env-url:9090");
        options.Token.ShouldBe("env-token");
        options.Format.ShouldBe("csv");
    }

    [Fact]
    public void GlobalOptions_ExplicitArgument_OverridesEnvironmentVariable()
    {
        // Arrange
        Environment.SetEnvironmentVariable("EVENTSTORE_ADMIN_URL", "https://env-url:9090");

        GlobalOptionsBinding binding = GlobalOptionsBinding.Create();
        RootCommand root = BuildRootCommand(binding);

        // Act
        ParseResult result = root.Parse(["--url", "https://explicit:1234"]);
        GlobalOptions options = binding.Resolve(result);

        // Assert
        options.Url.ShouldBe("https://explicit:1234");
    }

    [Fact]
    public void GlobalOptions_InvalidFormat_ReturnsError()
    {
        // Arrange
        GlobalOptionsBinding binding = GlobalOptionsBinding.Create();
        RootCommand root = BuildRootCommand(binding);

        // Act
        ParseResult result = root.Parse(["--format", "xml"]);

        // Assert
        result.Errors.ShouldNotBeEmpty();
    }

    private static RootCommand BuildRootCommand(GlobalOptionsBinding binding)
    {
        RootCommand root = new("test");
        root.Options.Add(binding.UrlOption);
        root.Options.Add(binding.TokenOption);
        root.Options.Add(binding.FormatOption);
        root.Options.Add(binding.OutputOption);
        return root;
    }
}
