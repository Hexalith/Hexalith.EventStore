using Hexalith.EventStore.Admin.Cli.Commands.Config;
using Hexalith.EventStore.Admin.Cli.Profiles;

namespace Hexalith.EventStore.Admin.Cli.Tests.Commands.Config;

[Collection("ConsoleTests")]
public class ProfileShowCommandTests : IDisposable {
    private readonly string _tempDir;
    private readonly string _profilePath;

    public ProfileShowCommandTests() {
        _tempDir = Path.Combine(Path.GetTempPath(), "eventstore-test-" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(_tempDir);
        _profilePath = Path.Combine(_tempDir, "profiles.json");
    }

    public void Dispose() {
        if (Directory.Exists(_tempDir)) {
            Directory.Delete(_tempDir, true);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Show_ExistingProfile_ShowsDetails() {
        ProfileStore store = new(ProfileStore.CurrentVersion, "prod", new Dictionary<string, ConnectionProfile> {
            ["prod"] = new("http://prod:5002", "secret-token-value", "json"),
        });
        ProfileManager.Save(store, _profilePath);

        StringWriter stdout = new();
        Console.SetOut(stdout);
        try {
            int exitCode = ProfileShowCommand.Execute("prod", "table", _profilePath);

            exitCode.ShouldBe(ExitCodes.Success);
            string output = stdout.ToString();
            output.ShouldContain("prod");
            output.ShouldContain("http://prod:5002");
            output.ShouldContain("secr...");  // masked token
            output.ShouldContain("json");
            output.ShouldContain("yes");       // active
        }
        finally {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        }
    }

    [Fact]
    public void Show_NonExistentProfile_ReturnsError() {
        ProfileStore store = new(ProfileStore.CurrentVersion, null, []);
        ProfileManager.Save(store, _profilePath);

        StringWriter stderr = new();
        Console.SetError(stderr);
        try {
            int exitCode = ProfileShowCommand.Execute("nonexistent", "table", _profilePath);

            exitCode.ShouldBe(ExitCodes.Error);
            stderr.ToString().ShouldContain("Profile 'nonexistent' not found.");
        }
        finally {
            Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
        }
    }

    [Fact]
    public void Show_InactiveProfile_ShowsNo() {
        ProfileStore store = new(ProfileStore.CurrentVersion, null, new Dictionary<string, ConnectionProfile> {
            ["dev"] = new("http://dev:5002", null, null),
        });
        ProfileManager.Save(store, _profilePath);

        StringWriter stdout = new();
        Console.SetOut(stdout);
        try {
            int exitCode = ProfileShowCommand.Execute("dev", "table", _profilePath);

            exitCode.ShouldBe(ExitCodes.Success);
            string output = stdout.ToString();
            output.ShouldContain("no");
            output.ShouldContain("(none)");    // null token
            output.ShouldContain("(default)"); // null format
        }
        finally {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        }
    }

    [Fact]
    public void Show_JsonFormat_ShowsFullToken() {
        ProfileStore store = new(ProfileStore.CurrentVersion, null, new Dictionary<string, ConnectionProfile> {
            ["prod"] = new("http://prod:5002", "secret-token-value", null),
        });
        ProfileManager.Save(store, _profilePath);

        StringWriter stdout = new();
        Console.SetOut(stdout);
        try {
            int exitCode = ProfileShowCommand.Execute("prod", "json", _profilePath);

            exitCode.ShouldBe(ExitCodes.Success);
            string output = stdout.ToString();
            output.ShouldContain("secret-token-value"); // full token in JSON
            output.ShouldContain("http://prod:5002");
        }
        finally {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        }
    }
}
