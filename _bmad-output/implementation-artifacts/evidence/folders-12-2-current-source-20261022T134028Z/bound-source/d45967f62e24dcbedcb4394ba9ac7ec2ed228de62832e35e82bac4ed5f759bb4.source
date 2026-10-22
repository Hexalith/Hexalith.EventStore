using System.CommandLine;

using Hexalith.EventStore.Admin.Cli.Profiles;

namespace Hexalith.EventStore.Admin.Cli.Commands.Config;

/// <summary>
/// Sets or clears the active connection profile.
/// </summary>
public static class ConfigUseCommand {
    /// <summary>
    /// Creates the config use subcommand.
    /// </summary>
    public static Command Create() {
        Argument<string?> nameArg = new("name") { Description = "Profile name to activate", Arity = ArgumentArity.ZeroOrOne };
        Option<bool> clearOption = new("--clear") { Description = "Clear active profile" };

        Command command = new("use", "Set or clear the active connection profile");
        command.Arguments.Add(nameArg);
        command.Options.Add(clearOption);

        command.SetAction((parseResult, _) => {
            string? name = parseResult.GetValue(nameArg);
            bool clear = parseResult.GetValue(clearOption);
            return Task.FromResult(Execute(name, clear));
        });

        return command;
    }

    internal static int Execute(string? name, bool clear, string? profilePath = null) {
        if (name is null && !clear) {
            Console.Error.WriteLine("Specify a profile name or use --clear to deactivate.");
            return ExitCodes.Error;
        }

        try {
            if (clear) {
                ProfileStore store = ProfileManager.Load(profilePath);
                ProfileManager.Save(store with { ActiveProfile = null }, profilePath);
                Console.Error.WriteLine("Active profile cleared.");
                return ExitCodes.Success;
            }

            ProfileStore profileStore = ProfileManager.Load(profilePath);
            if (!profileStore.Profiles.ContainsKey(name!)) {
                Console.Error.WriteLine($"Profile '{name}' not found. Run 'eventstore-admin config profile list' to see available profiles.");
                return ExitCodes.Error;
            }

            ProfileManager.Save(profileStore with { ActiveProfile = name }, profilePath);
            Console.Error.WriteLine($"Active profile set to '{name}'.");
            return ExitCodes.Success;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            Console.Error.WriteLine($"Error updating profile: {ex.Message}");
            return ExitCodes.Error;
        }
    }
}
