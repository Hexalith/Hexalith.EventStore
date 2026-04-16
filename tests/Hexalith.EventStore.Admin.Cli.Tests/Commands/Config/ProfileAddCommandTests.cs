using Hexalith.EventStore.Admin.Cli.Commands.Config;
using Hexalith.EventStore.Admin.Cli.Profiles;

namespace Hexalith.EventStore.Admin.Cli.Tests.Commands.Config;

[Collection("ConsoleTests")]
public class ProfileAddCommandTests : IDisposable {
    private readonly string _tempDir;
    private readonly string _profilePath;

    public ProfileAddCommandTests() {
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
    public void AddNewProfile_CreatesFileAndStoresProfile() {
        int exitCode = ProfileAddCommand.Execute("prod", "http://prod:5002", null, null, _profilePath);

        exitCode.ShouldBe(ExitCodes.Success);
        ProfileStore store = ProfileManager.Load(_profilePath);
        store.Profiles.ShouldContainKey("prod");
        store.Profiles["prod"].Url.ShouldBe("http://prod:5002");
    }

    [Fact]
    public void OverwriteExistingProfile_UpdatesProfile() {
        _ = ProfileAddCommand.Execute("prod", "http://old:5002", "old-token", null, _profilePath);
        int exitCode = ProfileAddCommand.Execute("prod", "http://new:5002", "new-token", "json", _profilePath);

        exitCode.ShouldBe(ExitCodes.Success);
        ProfileStore store = ProfileManager.Load(_profilePath);
        store.Profiles["prod"].Url.ShouldBe("http://new:5002");
        store.Profiles["prod"].Token.ShouldBe("new-token");
        store.Profiles["prod"].Format.ShouldBe("json");
    }

    [Fact]
    public void InvalidProfileName_ReturnsError() {
        int exitCode = ProfileAddCommand.Execute("../hack", "http://evil:5002", null, null, _profilePath);

        exitCode.ShouldBe(ExitCodes.Error);
        File.Exists(_profilePath).ShouldBeFalse();
    }

    [Fact]
    public void WithToken_PrintsPlaintextWarning() {
        StringWriter stderr = new();
        Console.SetError(stderr);
        try {
            _ = ProfileAddCommand.Execute("prod", "http://prod:5002", "secret-token", null, _profilePath);

            stderr.ToString().ShouldContain("plaintext");
        }
        finally {
            Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
        }
    }

    [Fact]
    public void WithoutToken_NoPlaintextWarning() {
        StringWriter stderr = new();
        Console.SetError(stderr);
        try {
            _ = ProfileAddCommand.Execute("prod", "http://prod:5002", null, null, _profilePath);

            stderr.ToString().ShouldNotContain("plaintext");
        }
        finally {
            Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
        }
    }

    [Fact]
    public void WithFormat_StoresFormat() {
        int exitCode = ProfileAddCommand.Execute("dev", "http://dev:5002", null, "csv", _profilePath);

        exitCode.ShouldBe(ExitCodes.Success);
        ProfileStore store = ProfileManager.Load(_profilePath);
        store.Profiles["dev"].Format.ShouldBe("csv");
    }
}
