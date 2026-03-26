using Hexalith.EventStore.Admin.Cli.Commands.Config;
using Hexalith.EventStore.Admin.Cli.Profiles;

namespace Hexalith.EventStore.Admin.Cli.Tests.Commands.Config;

[Collection("ConsoleTests")]
public class ProfileRemoveCommandTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _profilePath;

    public ProfileRemoveCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "eventstore-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _profilePath = Path.Combine(_tempDir, "profiles.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Remove_ExistingProfile_RemovesIt()
    {
        ProfileStore store = new(ProfileStore.CurrentVersion, null, new Dictionary<string, ConnectionProfile>
        {
            ["prod"] = new("http://prod:5002", null, null),
            ["dev"] = new("http://dev:5002", null, null),
        });
        ProfileManager.Save(store, _profilePath);

        int exitCode = ProfileRemoveCommand.Execute("prod", _profilePath);

        exitCode.ShouldBe(ExitCodes.Success);
        ProfileStore loaded = ProfileManager.Load(_profilePath);
        loaded.Profiles.ShouldNotContainKey("prod");
        loaded.Profiles.ShouldContainKey("dev");
    }

    [Fact]
    public void Remove_ActiveProfile_ClearsActive()
    {
        ProfileStore store = new(ProfileStore.CurrentVersion, "prod", new Dictionary<string, ConnectionProfile>
        {
            ["prod"] = new("http://prod:5002", null, null),
        });
        ProfileManager.Save(store, _profilePath);

        StringWriter stderr = new();
        Console.SetError(stderr);
        try
        {
            int exitCode = ProfileRemoveCommand.Execute("prod", _profilePath);

            exitCode.ShouldBe(ExitCodes.Success);
            ProfileStore loaded = ProfileManager.Load(_profilePath);
            loaded.ActiveProfile.ShouldBeNull();
            stderr.ToString().ShouldContain("Active profile cleared");
        }
        finally
        {
            Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
        }
    }

    [Fact]
    public void Remove_NonExistentProfile_ReturnsError()
    {
        ProfileStore store = new(ProfileStore.CurrentVersion, null, new Dictionary<string, ConnectionProfile>());
        ProfileManager.Save(store, _profilePath);

        int exitCode = ProfileRemoveCommand.Execute("nonexistent", _profilePath);

        exitCode.ShouldBe(ExitCodes.Error);
    }
}
