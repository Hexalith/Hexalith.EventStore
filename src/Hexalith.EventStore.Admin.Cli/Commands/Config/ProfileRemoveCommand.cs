using System.CommandLine;

using Hexalith.EventStore.Admin.Cli.Profiles;

namespace Hexalith.EventStore.Admin.Cli.Commands.Config;

/// <summary>
/// Removes a named connection profile.
/// </summary>
public static class ProfileRemoveCommand {

    /// <summary>
    /// Creates the profile remove subcommand.
    /// </summary>
    public static Command Create() {
        Argument<string> nameArg = new("name") { Description = "Profile name" };

        Command command = new("remove", "Remove a named connection profile");
        command.Arguments.Add(nameArg);

        command.SetAction((parseResult, _) => {
            string name = parseResult.GetValue(nameArg)!;
            return Task.FromResult(Execute(name));
        });

        return command;
    }

    internal static int Execute(string name, string? profilePath = null) {
        try {
            ProfileStore store = ProfileManager.Load(profilePath);

            if (!store.Profiles.ContainsKey(name)) {
                Console.Error.WriteLine($"Profile '{name}' not found.");
                return ExitCodes.Error;
            }

#pragma warning disable IDE0028 // Simplify collection initialization
            Dictionary<string, ConnectionProfile> profiles = new(store.Profiles);
#pragma warning restore IDE0028 // Simplify collection initialization
            _ = profiles.Remove(name);

            string? activeProfile = store.ActiveProfile;
            if (string.Equals(activeProfile, name, StringComparison.Ordinal)) {
                activeProfile = null;
                Console.Error.WriteLine("Active profile cleared.");
            }

            ProfileManager.Save(store with { Profiles = profiles, ActiveProfile = activeProfile }, profilePath);
            Console.Error.WriteLine($"Profile '{name}' removed.");
            return ExitCodes.Success;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            Console.Error.WriteLine($"Error removing profile: {ex.Message}");
            return ExitCodes.Error;
        }
    }
}
