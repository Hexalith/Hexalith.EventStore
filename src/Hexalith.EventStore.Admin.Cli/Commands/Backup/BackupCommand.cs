using System.CommandLine;

namespace Hexalith.EventStore.Admin.Cli.Commands.Backup;

/// <summary>
/// Parent command for backup operations.
/// </summary>
public static class BackupCommand
{
    /// <summary>
    /// Creates the "backup" command with stub subcommands.
    /// </summary>
    public static Command Create(GlobalOptionsBinding binding)
    {
        _ = binding;
        Command command = new("backup", "Backup and restore operations");
        command.Subcommands.Add(StubCommands.Create("create", "Create a backup of event streams"));
        command.Subcommands.Add(StubCommands.Create("restore", "Restore event streams from a backup"));
        command.Subcommands.Add(StubCommands.Create("list", "List available backups"));
        return command;
    }
}
