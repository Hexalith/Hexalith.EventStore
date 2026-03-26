using System.CommandLine;

namespace Hexalith.EventStore.Admin.Cli.Commands.Config;

/// <summary>
/// Parent command for profile CRUD subcommands.
/// </summary>
public static class ProfileCommand
{
    /// <summary>
    /// Creates the profile command with add, list, show, and remove subcommands.
    /// </summary>
    public static Command Create(GlobalOptionsBinding binding)
    {
        Command command = new("profile", "Manage named connection profiles");
        command.Subcommands.Add(ProfileAddCommand.Create());
        command.Subcommands.Add(ProfileListCommand.Create(binding));
        command.Subcommands.Add(ProfileShowCommand.Create(binding));
        command.Subcommands.Add(ProfileRemoveCommand.Create());
        return command;
    }
}
