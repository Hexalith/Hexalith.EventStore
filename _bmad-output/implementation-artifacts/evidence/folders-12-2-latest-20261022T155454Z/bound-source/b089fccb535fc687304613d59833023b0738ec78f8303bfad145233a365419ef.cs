using System.CommandLine;

using Hexalith.EventStore.Admin.Cli.Commands.Config;
using Hexalith.EventStore.Admin.Cli.Profiles;

namespace Hexalith.EventStore.Admin.Cli.Tests.Commands.Config;

[Collection("ConsoleTests")]
public class ConfigCurrentCommandTests : IDisposable {
    private readonly string _profilePath;
    private readonly string _tempDir;

    public ConfigCurrentCommandTests() {
        _tempDir = Path.Combine(Path.GetTempPath(), "eventstore-test-" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(_tempDir);
        _profilePath = Path.Combine(_tempDir, "profiles.json");
    }

    [Fact]
    public void ConfigCurrentCommand_HasCreateMethod() {
        var binding = GlobalOptionsBinding.Create();
        Command command = ConfigCurrentCommand.Create(binding);

        _ = command.ShouldNotBeNull();
        command.Name.ShouldBe("current");
    }

    public void Dispose() {
        if (Directory.Exists(_tempDir)) {
            Directory.Delete(_tempDir, true);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Execute_JsonFormat_ReturnsJson() {
        CreateProfile("prod", "http://prod:5002", "secret-tok", null, "prod");
        GlobalOptionsBinding binding = GlobalOptionsBinding.Create() with { ProfilePath = _profilePath };

        StringWriter stdout = new();
        Console.SetOut(stdout);
        try {
            RootCommand root = CreateRoot(binding);
            ParseResult parseResult = root.Parse(["health"]);
            int exitCode = ConfigCurrentCommand.Execute(binding, parseResult, "json");

            exitCode.ShouldBe(ExitCodes.Success);
            string output = stdout.ToString();
            output.ShouldContain("\"activeProfile\"");
            output.ShouldContain("\"url\"");
            output.ShouldContain("\"source\"");
            output.ShouldContain("profile: prod");
        }
        finally {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        }
    }

    [Fact]
    public void Execute_NoProfile_ShowsDefaults() {
        GlobalOptionsBinding binding = GlobalOptionsBinding.Create() with { ProfilePath = _profilePath };

        StringWriter stdout = new();
        Console.SetOut(stdout);
        try {
            RootCommand root = CreateRoot(binding);
            ParseResult parseResult = root.Parse(["health"]);
            int exitCode = ConfigCurrentCommand.Execute(binding, parseResult, "table");

            exitCode.ShouldBe(ExitCodes.Success);
            string output = stdout.ToString();
            output.ShouldContain("(none)");
            output.ShouldContain("http://localhost:5002");
            output.ShouldContain("default");
        }
        finally {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        }
    }

    [Fact]
    public void Execute_TokenMaskedInTable() {
        CreateProfile("prod", "http://prod:5002", "secret-token", null, "prod");
        GlobalOptionsBinding binding = GlobalOptionsBinding.Create() with { ProfilePath = _profilePath };

        StringWriter stdout = new();
        Console.SetOut(stdout);
        try {
            RootCommand root = CreateRoot(binding);
            ParseResult parseResult = root.Parse(["health"]);
            int exitCode = ConfigCurrentCommand.Execute(binding, parseResult, "table");

            exitCode.ShouldBe(ExitCodes.Success);
            string output = stdout.ToString();
            output.ShouldContain("secr...");
            output.ShouldNotContain("secret-token");
        }
        finally {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        }
    }

    [Fact]
    public void Execute_WithActiveProfile_ShowsProfileSource() {
        CreateProfile("prod", "http://prod:5002", "tok", "csv", "prod");
        GlobalOptionsBinding binding = GlobalOptionsBinding.Create() with { ProfilePath = _profilePath };

        StringWriter stdout = new();
        Console.SetOut(stdout);
        try {
            RootCommand root = CreateRoot(binding);
            ParseResult parseResult = root.Parse(["health"]);
            int exitCode = ConfigCurrentCommand.Execute(binding, parseResult, "table");

            exitCode.ShouldBe(ExitCodes.Success);
            string output = stdout.ToString();
            output.ShouldContain("prod");
            output.ShouldContain("http://prod:5002");
            output.ShouldContain("profile: prod");
        }
        finally {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        }
    }

    private static RootCommand CreateRoot(GlobalOptionsBinding binding) {
        RootCommand root = new("test");
        root.Options.Add(binding.UrlOption);
        root.Options.Add(binding.TokenOption);
        root.Options.Add(binding.FormatOption);
        root.Options.Add(binding.OutputOption);
        root.Options.Add(binding.ProfileOption);
        root.Subcommands.Add(new Command("health", "test"));
        return root;
    }

    private void CreateProfile(string name, string url, string? token, string? format, string? activeProfile = null) {
        ProfileStore store = new(ProfileStore.CurrentVersion, activeProfile, new Dictionary<string, ConnectionProfile> {
            [name] = new(url, token, format),
        });
        ProfileManager.Save(store, _profilePath);
    }
}
