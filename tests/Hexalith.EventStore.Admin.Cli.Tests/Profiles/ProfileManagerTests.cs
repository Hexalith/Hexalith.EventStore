using System.Text.Json;

using Hexalith.EventStore.Admin.Cli.Formatting;
using Hexalith.EventStore.Admin.Cli.Profiles;

namespace Hexalith.EventStore.Admin.Cli.Tests.Profiles;

[Collection("ConsoleTests")]
public class ProfileManagerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _profilePath;

    public ProfileManagerTests()
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
    public void Load_MissingFile_ReturnsEmptyStore()
    {
        ProfileStore store = ProfileManager.Load(_profilePath);

        store.Version.ShouldBe(ProfileStore.CurrentVersion);
        store.ActiveProfile.ShouldBeNull();
        store.Profiles.ShouldBeEmpty();
    }

    [Fact]
    public void Load_ValidFile_ReturnsParsedProfiles()
    {
        var data = new { version = 1, activeProfile = "prod", profiles = new { prod = new { url = "http://prod:5002", token = "tok123", format = "json" } } };
        File.WriteAllText(_profilePath, JsonSerializer.Serialize(data, JsonDefaults.Options));

        ProfileStore store = ProfileManager.Load(_profilePath);

        store.Version.ShouldBe(1);
        store.ActiveProfile.ShouldBe("prod");
        store.Profiles.ShouldContainKey("prod");
        store.Profiles["prod"].Url.ShouldBe("http://prod:5002");
        store.Profiles["prod"].Token.ShouldBe("tok123");
        store.Profiles["prod"].Format.ShouldBe("json");
    }

    [Fact]
    public void Load_CorruptJson_ReturnsEmptyStore()
    {
        File.WriteAllText(_profilePath, "{ not valid json }}}");

        StringWriter stderr = new();
        Console.SetError(stderr);
        try
        {
            ProfileStore store = ProfileManager.Load(_profilePath);

            store.Profiles.ShouldBeEmpty();
            stderr.ToString().ShouldContain("invalid JSON");
        }
        finally
        {
            Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
        }
    }

    [Fact]
    public void Load_Version1_ProceedsNormally()
    {
        var data = new { version = 1, profiles = new Dictionary<string, object>() };
        File.WriteAllText(_profilePath, JsonSerializer.Serialize(data, JsonDefaults.Options));

        ProfileStore store = ProfileManager.Load(_profilePath);

        store.Version.ShouldBe(1);
    }

    [Fact]
    public void Load_MissingVersion_TreatsAsVersion1()
    {
        File.WriteAllText(_profilePath, """{"profiles": {}}""");

        ProfileStore store = ProfileManager.Load(_profilePath);

        store.Version.ShouldBe(0); // default int
        store.Profiles.ShouldBeEmpty();
    }

    [Fact]
    public void Load_Version2_ThrowsVersionException()
    {
        var data = new { version = 2, profiles = new Dictionary<string, object>() };
        File.WriteAllText(_profilePath, JsonSerializer.Serialize(data, JsonDefaults.Options));

        ProfileStoreVersionException ex = Should.Throw<ProfileStoreVersionException>(() => ProfileManager.Load(_profilePath));
        ex.Message.ShouldContain("upgrade");
    }

    [Fact]
    public void Load_EmptyFile_ReturnsEmptyStore()
    {
        File.WriteAllText(_profilePath, string.Empty);

        ProfileStore store = ProfileManager.Load(_profilePath);
        store.Profiles.ShouldBeEmpty();
    }

    [Fact]
    public void Save_CreatesDirectoryAndFile()
    {
        string subPath = Path.Combine(_tempDir, "sub", "profiles.json");

        ProfileStore store = new(ProfileStore.CurrentVersion, null, new Dictionary<string, ConnectionProfile>
        {
            ["dev"] = new("http://dev:5002", null, null),
        });

        ProfileManager.Save(store, subPath);

        File.Exists(subPath).ShouldBeTrue();
        string json = File.ReadAllText(subPath);
        json.ShouldContain("dev");
    }

    [Fact]
    public void Save_OverwritesExistingFile()
    {
        ProfileStore store1 = new(ProfileStore.CurrentVersion, null, new Dictionary<string, ConnectionProfile>
        {
            ["first"] = new("http://first:5002", null, null),
        });
        ProfileManager.Save(store1, _profilePath);

        ProfileStore store2 = new(ProfileStore.CurrentVersion, "second", new Dictionary<string, ConnectionProfile>
        {
            ["second"] = new("http://second:5002", "tok", "csv"),
        });
        ProfileManager.Save(store2, _profilePath);

        ProfileStore loaded = ProfileManager.Load(_profilePath);
        loaded.Profiles.ShouldContainKey("second");
        loaded.Profiles.ShouldNotContainKey("first");
        loaded.ActiveProfile.ShouldBe("second");
    }

    [Fact]
    public void Save_WritesCurrentVersion()
    {
        ProfileStore store = new(0, null, new Dictionary<string, ConnectionProfile>());
        ProfileManager.Save(store, _profilePath);

        ProfileStore loaded = ProfileManager.Load(_profilePath);
        loaded.Version.ShouldBe(ProfileStore.CurrentVersion);
    }

    [Theory]
    [InlineData("prod", true)]
    [InlineData("my-env", true)]
    [InlineData("test_1", true)]
    [InlineData("a", true)]
    [InlineData("UPPER-case_123", true)]
    [InlineData("", false)]
    [InlineData(" ", false)]
    [InlineData("../hack", false)]
    [InlineData("has space", false)]
    [InlineData("special!char", false)]
    public void ValidateProfileName_ReturnsExpected(string name, bool expected)
    {
        ProfileManager.ValidateProfileName(name).ShouldBe(expected);
    }

    [Fact]
    public void ValidateProfileName_TooLong_ReturnsFalse()
    {
        string longName = new('a', 65);
        ProfileManager.ValidateProfileName(longName).ShouldBeFalse();
    }

    [Fact]
    public void ValidateProfileName_MaxLength_ReturnsTrue()
    {
        string maxName = new('a', 64);
        ProfileManager.ValidateProfileName(maxName).ShouldBeTrue();
    }

    [Theory]
    [InlineData("version")]
    [InlineData("activeProfile")]
    [InlineData("profiles")]
    [InlineData("url")]
    [InlineData("token")]
    [InlineData("format")]
    [InlineData("VERSION")]
    public void ValidateProfileName_ReservedNames_ReturnsFalse(string name)
    {
        ProfileManager.ValidateProfileName(name).ShouldBeFalse();
    }

    [Theory]
    [InlineData(null, "(none)")]
    [InlineData("", "(none)")]
    [InlineData("abc", "abc...")]
    [InlineData("abcd", "abcd...")]
    [InlineData("abcde", "abcd...")]
    [InlineData("eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9", "eyJh...")]
    public void MaskToken_ReturnsExpected(string? token, string expected)
    {
        ProfileManager.MaskToken(token).ShouldBe(expected);
    }
}
