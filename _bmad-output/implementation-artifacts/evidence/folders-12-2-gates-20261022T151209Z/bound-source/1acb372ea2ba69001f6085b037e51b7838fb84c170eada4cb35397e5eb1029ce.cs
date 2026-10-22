using System.CommandLine;

using Hexalith.EventStore.Admin.Cli.Profiles;

namespace Hexalith.EventStore.Admin.Cli.Commands.Config;

/// <summary>
/// Creates or overwrites a named connection profile.
/// </summary>
public static class ProfileAddCommand {
    /// <summary>
    /// Creates the profile add subcommand.
    /// </summary>
    public static Command Create() {
        Argument<string> nameArg = new("name") { Description = "Profile name" };
        Option<string> apiUrlOption = new("--api-url") { Required = true, Description = "Admin API base URL" };
        Option<string?> apiTokenOption = new("--api-token") { Description = "JWT Bearer token or API key" };
        Option<string?> defaultFormatOption = new("--default-format") { Description = "Default output format" };
        _ = defaultFormatOption.AcceptOnlyFromAmong("json", "csv", "table");

        Command command = new("add", "Create or update a named connection profile");
        command.Arguments.Add(nameArg);
        command.Options.Add(apiUrlOption);
        command.Options.Add(apiTokenOption);
        command.Options.Add(defaultFormatOption);

        command.SetAction((parseResult, _) => {
            string name = parseResult.GetValue(nameArg)!;
            string apiUrl = parseResult.GetValue(apiUrlOption)!;
            string? apiToken = parseResult.GetValue(apiTokenOption);
            string? defaultFormat = parseResult.GetValue(defaultFormatOption);
            return Task.FromResult(Execute(name, apiUrl, apiToken, defaultFormat));
        });

        return command;
    }

    internal static int Execute(string name, string apiUrl, string? apiToken, string? defaultFormat, string? profilePath = null) {
        if (!ProfileManager.ValidateProfileName(name)) {
            Console.Error.WriteLine($"Invalid profile name '{name}'. Use alphanumeric characters, hyphens, and underscores (1-64 chars).");
            return ExitCodes.Error;
        }

        try {
            ProfileStore store = ProfileManager.Load(profilePath);
            Dictionary<string, ConnectionProfile> profiles = new(store.Profiles) {
                [name] = new ConnectionProfile(apiUrl, apiToken, defaultFormat),
            };
            ProfileManager.Save(store with { Profiles = profiles }, profilePath);

            if (apiToken is not null) {
                string path = profilePath ?? ProfileManager.GetDefaultProfilePath();
                Console.Error.WriteLine($"Note: Token stored in plaintext at {path}. Restrict file permissions for production use.");
            }

            Console.Error.WriteLine($"Profile '{name}' saved.");
            return ExitCodes.Success;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            Console.Error.WriteLine($"Error saving profile: {ex.Message}");
            return ExitCodes.Error;
        }
    }
}
