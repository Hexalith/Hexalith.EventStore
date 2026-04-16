using Hexalith.EventStore.Admin.Cli.Commands.Config;
using Hexalith.EventStore.Admin.Cli.Profiles;

namespace Hexalith.EventStore.Admin.Cli.Tests.Commands.Config;

public class ConfigUseCommandTests : IDisposable {
    private readonly string _tempDir;
    private readonly string _profilePath;

    public ConfigUseCommandTests() {
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
    public void Use_ExistingProfile_SetsActive() {
        ProfileStore store = new(ProfileStore.CurrentVersion, null, new Dictionary<string, ConnectionProfile> {
            ["prod"] = new("http://prod:5002", null, null),
        });
        ProfileManager.Save(store, _profilePath);

        int exitCode = ConfigUseCommand.Execute("prod", false, _profilePath);

        exitCode.ShouldBe(ExitCodes.Success);
        ProfileStore loaded = ProfileManager.Load(_profilePath);
        loaded.ActiveProfile.ShouldBe("prod");
    }

    [Fact]
    public void Use_NonExistentProfile_ReturnsError() {
        ProfileStore store = new(ProfileStore.CurrentVersion, null, []);
        ProfileManager.Save(store, _profilePath);

        int exitCode = ConfigUseCommand.Execute("nonexistent", false, _profilePath);

        exitCode.ShouldBe(ExitCodes.Error);
    }

    [Fact]
    public void Use_ClearFlag_ClearsActive() {
        ProfileStore store = new(ProfileStore.CurrentVersion, "prod", new Dictionary<string, ConnectionProfile> {
            ["prod"] = new("http://prod:5002", null, null),
        });
        ProfileManager.Save(store, _profilePath);

        int exitCode = ConfigUseCommand.Execute(null, true, _profilePath);

        exitCode.ShouldBe(ExitCodes.Success);
        ProfileStore loaded = ProfileManager.Load(_profilePath);
        loaded.ActiveProfile.ShouldBeNull();
    }

    [Fact]
    public void Use_NoNameNoClear_ReturnsError() {
        int exitCode = ConfigUseCommand.Execute(null, false, _profilePath);

        exitCode.ShouldBe(ExitCodes.Error);
    }
}
