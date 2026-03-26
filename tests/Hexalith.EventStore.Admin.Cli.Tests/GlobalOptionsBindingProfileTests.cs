using System.CommandLine;

using Hexalith.EventStore.Admin.Cli.Profiles;

namespace Hexalith.EventStore.Admin.Cli.Tests;

[Collection("EnvironmentVariableTests")]
public class GlobalOptionsBindingProfileTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _profilePath;
    private readonly string? _origUrl;
    private readonly string? _origToken;
    private readonly string? _origFormat;

    public GlobalOptionsBindingProfileTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "eventstore-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _profilePath = Path.Combine(_tempDir, "profiles.json");

        // Save original env vars for cleanup
        _origUrl = Environment.GetEnvironmentVariable("EVENTSTORE_ADMIN_URL");
        _origToken = Environment.GetEnvironmentVariable("EVENTSTORE_ADMIN_TOKEN");
        _origFormat = Environment.GetEnvironmentVariable("EVENTSTORE_ADMIN_FORMAT");

        // Clear env vars to get clean baseline
        Environment.SetEnvironmentVariable("EVENTSTORE_ADMIN_URL", null);
        Environment.SetEnvironmentVariable("EVENTSTORE_ADMIN_TOKEN", null);
        Environment.SetEnvironmentVariable("EVENTSTORE_ADMIN_FORMAT", null);
    }

    public void Dispose()
    {
        // Restore env vars
        Environment.SetEnvironmentVariable("EVENTSTORE_ADMIN_URL", _origUrl);
        Environment.SetEnvironmentVariable("EVENTSTORE_ADMIN_TOKEN", _origToken);
        Environment.SetEnvironmentVariable("EVENTSTORE_ADMIN_FORMAT", _origFormat);

        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Resolve_CliExplicitUrl_WinsOverProfile()
    {
        // Arrange — profile has url, CLI explicit --url
        CreateProfile("prod", "http://profile:5002", null, null, "prod");
        GlobalOptionsBinding binding = GlobalOptionsBinding.Create() with { ProfilePath = _profilePath };
        RootCommand root = CreateRoot(binding);

        // Act — parse with explicit --url
        ParseResult parseResult = root.Parse(["--url", "http://cli:5002", "health"]);
        GlobalOptions options = binding.Resolve(parseResult);

        // Assert
        options.Url.ShouldBe("http://cli:5002");
    }

    [Fact]
    public void Resolve_EnvVarUrl_WinsOverProfile()
    {
        // Arrange — env var set + profile has url
        CreateProfile("prod", "http://profile:5002", null, null, "prod");
        Environment.SetEnvironmentVariable("EVENTSTORE_ADMIN_URL", "http://env:5002");
        GlobalOptionsBinding binding = GlobalOptionsBinding.Create() with { ProfilePath = _profilePath };
        RootCommand root = CreateRoot(binding);

        // Act
        ParseResult parseResult = root.Parse(["health"]);
        GlobalOptions options = binding.Resolve(parseResult);

        // Assert — env wins over profile
        options.Url.ShouldBe("http://env:5002");
    }

    [Fact]
    public void Resolve_ProfileUrl_UsedWhenNoCliNoEnv()
    {
        // Arrange — profile has url, no CLI, no env
        CreateProfile("prod", "http://profile:5002", null, null, "prod");
        GlobalOptionsBinding binding = GlobalOptionsBinding.Create() with { ProfilePath = _profilePath };
        RootCommand root = CreateRoot(binding);

        // Act
        ParseResult parseResult = root.Parse(["health"]);
        GlobalOptions options = binding.Resolve(parseResult);

        // Assert
        options.Url.ShouldBe("http://profile:5002");
    }

    [Fact]
    public void Resolve_NoProfileNoEnv_UsesDefault()
    {
        // No profile file exists, no env
        GlobalOptionsBinding binding = GlobalOptionsBinding.Create() with { ProfilePath = _profilePath };
        RootCommand root = CreateRoot(binding);

        // Act
        ParseResult parseResult = root.Parse(["health"]);
        GlobalOptions options = binding.Resolve(parseResult);

        // Assert
        options.Url.ShouldBe("http://localhost:5002");
        options.Format.ShouldBe("table");
    }

    [Fact]
    public void Resolve_ExplicitProfileFlag_WinsOverActiveProfile()
    {
        // Arrange — active profile is "dev", explicit --profile prod
        CreateProfile("prod", "http://prod:5002", null, null);
        AddProfile("dev", "http://dev:5002", null, null, "dev");
        GlobalOptionsBinding binding = GlobalOptionsBinding.Create() with { ProfilePath = _profilePath };
        RootCommand root = CreateRoot(binding);

        // Act
        ParseResult parseResult = root.Parse(["--profile", "prod", "health"]);
        GlobalOptions options = binding.Resolve(parseResult);

        // Assert
        options.Url.ShouldBe("http://prod:5002");
    }

    [Fact]
    public void Resolve_ProfileNotFound_ThrowsException()
    {
        // Arrange
        CreateProfile("prod", "http://prod:5002", null, null);
        GlobalOptionsBinding binding = GlobalOptionsBinding.Create() with { ProfilePath = _profilePath };
        RootCommand root = CreateRoot(binding);

        // Act + Assert
        ParseResult parseResult = root.Parse(["--profile", "nonexistent", "health"]);
        Should.Throw<ProfileNotFoundException>(() => binding.Resolve(parseResult));
    }

    [Fact]
    public void Resolve_ActiveProfileSet_UsedImplicitly()
    {
        // Arrange
        CreateProfile("prod", "http://prod:5002", "tok", "csv", "prod");
        GlobalOptionsBinding binding = GlobalOptionsBinding.Create() with { ProfilePath = _profilePath };
        RootCommand root = CreateRoot(binding);

        // Act
        ParseResult parseResult = root.Parse(["health"]);
        GlobalOptions options = binding.Resolve(parseResult);

        // Assert
        options.Url.ShouldBe("http://prod:5002");
        options.Token.ShouldBe("tok");
        options.Format.ShouldBe("csv");
    }

    [Fact]
    public void Resolve_NoActiveProfileNoFlag_DefaultsUsed()
    {
        // Arrange — profile file exists but no active profile
        CreateProfile("prod", "http://prod:5002", null, null);
        GlobalOptionsBinding binding = GlobalOptionsBinding.Create() with { ProfilePath = _profilePath };
        RootCommand root = CreateRoot(binding);

        // Act
        ParseResult parseResult = root.Parse(["health"]);
        GlobalOptions options = binding.Resolve(parseResult);

        // Assert — no profile applied, use defaults
        options.Url.ShouldBe("http://localhost:5002");
    }

    [Fact]
    public void Resolve_MixedSources_ProfileUrlEnvToken()
    {
        // Arrange — profile has url but not token; env has token
        CreateProfile("prod", "http://profile:5002", null, null, "prod");
        Environment.SetEnvironmentVariable("EVENTSTORE_ADMIN_TOKEN", "env-token");
        GlobalOptionsBinding binding = GlobalOptionsBinding.Create() with { ProfilePath = _profilePath };
        RootCommand root = CreateRoot(binding);

        // Act
        ParseResult parseResult = root.Parse(["health"]);
        GlobalOptions options = binding.Resolve(parseResult);

        // Assert
        options.Url.ShouldBe("http://profile:5002");
        options.Token.ShouldBe("env-token");
    }

    [Fact]
    public void Resolve_ProfileFormat_OverriddenByCli()
    {
        // Arrange
        CreateProfile("prod", "http://prod:5002", null, "csv", "prod");
        GlobalOptionsBinding binding = GlobalOptionsBinding.Create() with { ProfilePath = _profilePath };
        RootCommand root = CreateRoot(binding);

        // Act
        ParseResult parseResult = root.Parse(["--format", "json", "health"]);
        GlobalOptions options = binding.Resolve(parseResult);

        // Assert — CLI wins
        options.Format.ShouldBe("json");
    }

    [Fact]
    public void Resolve_MissingProfilesJson_SilentNoop()
    {
        // Arrange — no profile file at all
        GlobalOptionsBinding binding = GlobalOptionsBinding.Create() with { ProfilePath = _profilePath };
        RootCommand root = CreateRoot(binding);

        // Act — should not throw, just use defaults
        ParseResult parseResult = root.Parse(["health"]);
        GlobalOptions options = binding.Resolve(parseResult);

        // Assert
        options.Url.ShouldBe("http://localhost:5002");
        options.Token.ShouldBeNull();
        options.Format.ShouldBe("table");
    }

    [Fact]
    public void ResolveWithSources_ReturnsCorrectLabels()
    {
        // Arrange
        CreateProfile("prod", "http://profile:5002", "tok", null, "prod");
        GlobalOptionsBinding binding = GlobalOptionsBinding.Create() with { ProfilePath = _profilePath };
        RootCommand root = CreateRoot(binding);

        // Act
        ParseResult parseResult = root.Parse(["health"]);
        (GlobalOptions options, Dictionary<string, string> sources) = binding.ResolveWithSources(parseResult);

        // Assert
        options.Url.ShouldBe("http://profile:5002");
        sources["url"].ShouldBe("profile: prod");
        sources["token"].ShouldBe("profile: prod");
        sources["format"].ShouldBe("default");
    }

    [Fact]
    public void ResolveWithSources_EnvVarSource_CorrectLabel()
    {
        // Arrange
        Environment.SetEnvironmentVariable("EVENTSTORE_ADMIN_URL", "http://env:5002");
        GlobalOptionsBinding binding = GlobalOptionsBinding.Create() with { ProfilePath = _profilePath };
        RootCommand root = CreateRoot(binding);

        // Act
        ParseResult parseResult = root.Parse(["health"]);
        (_, Dictionary<string, string> sources) = binding.ResolveWithSources(parseResult);

        // Assert
        sources["url"].ShouldBe("env: EVENTSTORE_ADMIN_URL");
    }

    [Fact]
    public void ResolveWithSources_CliSource_CorrectLabel()
    {
        // Arrange
        GlobalOptionsBinding binding = GlobalOptionsBinding.Create() with { ProfilePath = _profilePath };
        RootCommand root = CreateRoot(binding);

        // Act
        ParseResult parseResult = root.Parse(["--url", "http://explicit:5002", "health"]);
        (_, Dictionary<string, string> sources) = binding.ResolveWithSources(parseResult);

        // Assert
        sources["url"].ShouldBe("cli");
    }

    [Fact]
    public void Resolve_CorruptProfilesJson_GracefulFallbackToDefaults()
    {
        // Arrange — write corrupt JSON to profiles.json
        File.WriteAllText(_profilePath, "this is not valid json {{{");
        GlobalOptionsBinding binding = GlobalOptionsBinding.Create() with { ProfilePath = _profilePath };
        RootCommand root = CreateRoot(binding);

        // Act — should not throw, falls back to defaults
        ParseResult parseResult = root.Parse(["health"]);
        GlobalOptions options = binding.Resolve(parseResult);

        // Assert
        options.Url.ShouldBe("http://localhost:5002");
        options.Format.ShouldBe("table");
    }

    [Fact]
    public void Resolve_CorruptProfilesJson_WithExplicitProfile_GracefulFallback()
    {
        // Arrange — corrupt JSON + --profile flag
        File.WriteAllText(_profilePath, "this is not valid json {{{");
        GlobalOptionsBinding binding = GlobalOptionsBinding.Create() with { ProfilePath = _profilePath };
        RootCommand root = CreateRoot(binding);

        // Act — corrupt file means profile can't be found; falls back to defaults (corrupt file treated as empty store by ProfileManager.Load)
        ParseResult parseResult = root.Parse(["--profile", "prod", "health"]);
        Should.Throw<ProfileNotFoundException>(() => binding.Resolve(parseResult));
    }

    [Fact]
    public void Resolve_EmptyEnvVar_IgnoredFallsToProfileOrDefault()
    {
        // Arrange — env var is set but empty; profile has a URL
        CreateProfile("prod", "http://profile:5002", null, null, "prod");
        Environment.SetEnvironmentVariable("EVENTSTORE_ADMIN_URL", "");
        GlobalOptionsBinding binding = GlobalOptionsBinding.Create() with { ProfilePath = _profilePath };
        RootCommand root = CreateRoot(binding);

        // Act
        ParseResult parseResult = root.Parse(["health"]);
        GlobalOptions options = binding.Resolve(parseResult);

        // Assert — empty env var should be ignored, profile value wins
        options.Url.ShouldBe("http://profile:5002");
    }

    private void CreateProfile(string name, string url, string? token, string? format, string? activeProfile = null)
    {
        ProfileStore store = new(ProfileStore.CurrentVersion, activeProfile, new Dictionary<string, ConnectionProfile>
        {
            [name] = new(url, token, format),
        });
        ProfileManager.Save(store, _profilePath);
    }

    private void AddProfile(string name, string url, string? token, string? format, string? activeProfile = null)
    {
        ProfileStore existing = ProfileManager.Load(_profilePath);
        Dictionary<string, ConnectionProfile> profiles = new(existing.Profiles)
        {
            [name] = new(url, token, format),
        };
        ProfileManager.Save(existing with { Profiles = profiles, ActiveProfile = activeProfile ?? existing.ActiveProfile }, _profilePath);
    }

    private static RootCommand CreateRoot(GlobalOptionsBinding binding)
    {
        RootCommand root = new("test");
        root.Options.Add(binding.UrlOption);
        root.Options.Add(binding.TokenOption);
        root.Options.Add(binding.FormatOption);
        root.Options.Add(binding.OutputOption);
        root.Options.Add(binding.ProfileOption);
        root.Subcommands.Add(new Command("health", "test"));
        return root;
    }
}
