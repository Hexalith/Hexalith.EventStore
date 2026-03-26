using System.CommandLine;

namespace Hexalith.EventStore.Admin.Cli.Commands.Config;

/// <summary>
/// Parent command for connection profile management and CLI configuration.
/// </summary>
public static class ConfigCommand
{
    /// <summary>
    /// Creates the config command with all subcommands.
    /// </summary>
    public static Command Create(GlobalOptionsBinding binding)
    {
        Command command = new("config", "Manage connection profiles and CLI configuration");
        command.Subcommands.Add(ProfileCommand.Create(binding));
        command.Subcommands.Add(ConfigUseCommand.Create());
        command.Subcommands.Add(ConfigCurrentCommand.Create(binding));
        command.Subcommands.Add(ConfigCompletionCommand.Create());
        return command;
    }
}
