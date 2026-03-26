using Hexalith.EventStore.Admin.Cli.Commands.Config;
using Hexalith.EventStore.Admin.Cli.Profiles;

namespace Hexalith.EventStore.Admin.Cli.Tests.Commands.Config;

[Collection("ConsoleTests")]
public class ProfileListCommandTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _profilePath;

    public ProfileListCommandTests()
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
    public void List_NoProfiles_PrintsHelpMessage()
    {
        StringWriter stderr = new();
        Console.SetError(stderr);
        try
        {
            int exitCode = ProfileListCommand.Execute("table", _profilePath);

            exitCode.ShouldBe(ExitCodes.Success);
            stderr.ToString().ShouldContain("No profiles configured");
        }
        finally
        {
            Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
        }
    }

    [Fact]
    public void List_WithProfiles_ShowsTable()
    {
        ProfileStore store = new(ProfileStore.CurrentVersion, "prod", new Dictionary<string, ConnectionProfile>
        {
            ["prod"] = new("http://prod:5002", "secret-token", "json"),
            ["dev"] = new("http://dev:5002", null, null),
        });
        ProfileManager.Save(store, _profilePath);

        StringWriter stdout = new();
        Console.SetOut(stdout);
        try
        {
            int exitCode = ProfileListCommand.Execute("table", _profilePath);

            exitCode.ShouldBe(ExitCodes.Success);
            string output = stdout.ToString();
            output.ShouldContain("prod");
            output.ShouldContain("dev");
            output.ShouldContain("secr...");  // masked token
            output.ShouldContain("*");         // active marker
        }
        finally
        {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        }
    }

    [Fact]
    public void List_JsonFormat_ShowsFullTokens()
    {
        ProfileStore store = new(ProfileStore.CurrentVersion, null, new Dictionary<string, ConnectionProfile>
        {
            ["prod"] = new("http://prod:5002", "secret-token-value", null),
        });
        ProfileManager.Save(store, _profilePath);

        StringWriter stdout = new();
        Console.SetOut(stdout);
        try
        {
            int exitCode = ProfileListCommand.Execute("json", _profilePath);

            exitCode.ShouldBe(ExitCodes.Success);
            string output = stdout.ToString();
            output.ShouldContain("secret-token-value"); // full token in JSON
        }
        finally
        {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        }
    }
}
